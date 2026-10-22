namespace Vespera.Core.World;

/// <summary>
/// A* de quem não é o jogador (NPCs com rotina, caçadores ambiente) sobre qualquer <see cref="IWalkArea"/>, calculado
/// em tempo de execução sobre a área do mapa carregado (máscara de 4 ou 8 u, ou formas). DECISIONS B2-D48.
/// <para>
/// A malha é global, de <see cref="Step"/> u, com os nós em x ≡ 2 e y ≡ 0 (mod 4): com raio 4, as sondas do
/// <see cref="WalkAreaExtensions.CanStand"/> ficam a 2 u (em x) e 1,6 u (em y) de qualquer borda de célula de 4 ou 8 u, então
/// quem anda pelo caminho nunca fica "no fio" de uma célula bloqueada (o NPC preso em x = 27,999996), e todo vão por onde
/// um corpo de raio 4 passa tem um nó dentro. Diagonais só entre nós cujos vizinhos ortogonais também estão livres
/// (não cortam quina). O caminho sai encurtado ("puxar o fio") com segmentos testados a cada 1 u.
/// </para>
/// <para>
/// As regiões ligadas da malha são rotuladas uma vez (<see cref="Prepare"/>): pedir caminho entre regiões diferentes
/// falha na hora, sem varrer o mapa, e os waypoints podem ser levados para a região certa (<see cref="NearestPerRegion"/>).
/// </para>
/// Os buffers são reaproveitados entre chamadas (nada é alocado depois da primeira) e a resposta "dá para ficar de pé"
/// de cada nó fica guardada. Uma instância por thread.
/// </summary>
public sealed class WalkPlanner
{
    /// <summary>Distância entre nós da malha (u).</summary>
    public const float Step = 4f;

    /// <summary>x do nó de coluna 0.</summary>
    public const float OffsetX = 2f;

    /// <summary>y do nó de linha 0.</summary>
    public const float OffsetY = 0f;

    /// <summary>Amostragem dos segmentos retos (u).</summary>
    public const float SampleStep = 1f;

    /// <summary>Peso da heurística do A* (1 = ótimo; mais = mais rápido).</summary>
    public const float HeuristicWeight = 1.6f;

    private const byte Unknown = 0;
    private const byte Free = 1;
    private const byte Blocked = 2;
    private const float Diagonal = Step * 1.41421356f;

    private static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private readonly byte[] _stand;
    private readonly int[] _seen;
    private readonly int[] _closed;
    private readonly float[] _g;
    private readonly int[] _parent;
    private readonly int[] _label;
    private readonly ushort[] _links;
    private readonly List<int> _labelSizes = [0];
    private readonly Queue<int> _flood = new();
    private readonly int[] _goalLabels = new int[32];
    private readonly PriorityQueue<int, float> _open = new();
    private readonly List<int> _chain = [];
    private readonly List<MapPoint> _raw = [];

    /// <summary>Distâncias na malha de cada marco a todos os nós (heurística ALT), feitas no <see cref="Prepare"/>.</summary>
    private float[][] _landmarks = [];
    private readonly float[] _goalLandmark = new float[LandmarkCount];
    private bool _altActive;

    /// <summary>Marcos da heurística ALT: corta muito os nós abertos em mapas com canal e pontes.</summary>
    public const int LandmarkCount = 8;
    private int _generation;

    public WalkPlanner(IWalkArea area, float radius = WalkMover.NpcRadius)
    {
        ArgumentNullException.ThrowIfNull(area);
        Area = area;
        Radius = radius;
        Columns = Math.Max(1, (int)MathF.Floor((area.Width - OffsetX) / Step) + 1);
        Rows = Math.Max(1, (int)MathF.Floor((area.Height - OffsetY) / Step) + 1);
        var n = Columns * Rows;
        _stand = new byte[n];
        _seen = new int[n];
        _closed = new int[n];
        _g = new float[n];
        _parent = new int[n];
        _label = new int[n];
        _links = new ushort[n];
    }

    public IWalkArea Area { get; }

    public float Radius { get; }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Limite de nós abertos por busca (o pior caso fica em poucos ms).</summary>
    public int MaxExpansions { get; set; } = 60000;

    /// <summary>Nós abertos na última busca (testes e medição).</summary>
    public int LastExpansions { get; private set; }

    /// <summary>As regiões ligadas já foram rotuladas.</summary>
    public bool IsPrepared { get; private set; }

    public MapPoint NodePoint(int column, int row) => new(OffsetX + (column * Step), OffsetY + (row * Step));

    public bool CanStand(float x, float y) => Area.CanStand(x, y, Radius);

    /// <summary>
    /// Dá para ficar de pé em <paramref name="p"/> e também a 0,5 u dele nos dois eixos: um ponto assim não vira
    /// armadilha por erro de float.
    /// </summary>
    public bool CanStandFirmly(MapPoint p) =>
        CanStand(p.X, p.Y)
        && CanStand(p.X - 0.5f, p.Y) && CanStand(p.X + 0.5f, p.Y)
        && CanStand(p.X, p.Y - 0.5f) && CanStand(p.X, p.Y + 0.5f);

    /// <summary>Reta livre de <paramref name="a"/> a <paramref name="b"/>, amostrada a cada <see cref="SampleStep"/> u.</summary>
    public bool IsClear(MapPoint a, MapPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        var samples = Math.Max(1, (int)MathF.Ceiling(length / SampleStep));
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            if (!CanStand(a.X + (dx * t), a.Y + (dy * t)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Rotula as regiões ligadas da malha (uma busca em largura no mapa inteiro, poucos ms). Feito sob demanda; chame
    /// antes (na troca de mapa ou numa thread) para não pagar no meio do jogo.
    /// </summary>
    public void Prepare()
    {
        if (IsPrepared)
        {
            return;
        }

        for (var start = 0; start < _label.Length; start++)
        {
            if (_label[start] != 0 || !Stand(start))
            {
                continue;
            }

            var id = _labelSizes.Count;
            var size = 0;
            _label[start] = id;
            _flood.Clear();
            _flood.Enqueue(start);
            while (_flood.TryDequeue(out var node))
            {
                size++;
                var ni = node % Columns;
                var nj = node / Columns;
                foreach (var (dx, dy) in Neighbours)
                {
                    var n = Link(ni, nj, dx, dy);
                    if (n >= 0 && _label[n] == 0)
                    {
                        _label[n] = id;
                        _flood.Enqueue(n);
                    }
                }
            }

            _labelSizes.Add(size);
        }

        BuildLandmarks();
        IsPrepared = true;
        WarmUp();
    }

    /// <summary>
    /// Um pedido de ida e volta entre dois marcos, ainda na troca de mapa: compila o laço do A* e cresce a fila e as listas
    /// para o primeiro pedido do jogo não pagar isso no meio de um quadro.
    /// </summary>
    private void WarmUp()
    {
        if (_landmarks.Length < 2)
        {
            return;
        }

        var a = Array.IndexOf(_landmarks[0], 0f);
        var b = Array.IndexOf(_landmarks[1], 0f);
        if (a < 0 || b < 0)
        {
            return;
        }

        var path = new List<MapPoint>();
        TryPlan(NodePoint(a % Columns, a / Columns), NodePoint(b % Columns, b / Columns), path);
        TryPlan(NodePoint(b % Columns, b / Columns), NodePoint(a % Columns, a / Columns), path);
    }

    /// <summary>
    /// Heurística ALT (A*, marcos e desigualdade triangular): escolhe marcos afastados na maior região e guarda a
    /// distância na malha de cada um a todos os nós. h(n) = máx |d(L, alvo) − d(L, n)|, nunca maior que a distância real.
    /// </summary>
    private void BuildLandmarks()
    {
        var biggest = 0;
        for (var id = 1; id < _labelSizes.Count; id++)
        {
            if (_labelSizes[id] > _labelSizes[biggest])
            {
                biggest = id;
            }
        }

        if (biggest == 0)
        {
            _landmarks = [];
            return;
        }

        var seed = Array.IndexOf(_label, biggest);
        var marks = new List<float[]>(LandmarkCount);
        var probe = Dijkstra(seed);
        for (var m = 0; m < LandmarkCount; m++)
        {
            // o nó mais longe dos marcos já escolhidos (no primeiro: do nó semente)
            var far = seed;
            var best = -1f;
            for (var n = 0; n < _label.Length; n++)
            {
                if (_label[n] != biggest)
                {
                    continue;
                }

                var d = float.MaxValue;
                if (marks.Count == 0)
                {
                    d = probe[n];
                }
                else
                {
                    foreach (var mk in marks)
                    {
                        d = MathF.Min(d, mk[n]);
                    }
                }

                if (d > best && !float.IsPositiveInfinity(d))
                {
                    best = d;
                    far = n;
                }
            }

            marks.Add(Dijkstra(far));
        }

        _landmarks = [.. marks];
    }

    private float[] Dijkstra(int source)
    {
        var dist = new float[_label.Length];
        Array.Fill(dist, float.PositiveInfinity);
        var queue = new PriorityQueue<int, float>();
        dist[source] = 0;
        queue.Enqueue(source, 0);
        while (queue.TryDequeue(out var node, out var d))
        {
            if (d > dist[node])
            {
                continue;
            }

            var ni = node % Columns;
            var nj = node / Columns;
            var links = LinksOf(ni, nj, node);
            for (var k = 0; k < Neighbours.Length; k++)
            {
                if ((links & (1 << k)) == 0)
                {
                    continue;
                }

                var n = node + (Neighbours[k].Dy * Columns) + Neighbours[k].Dx;
                var nd = d + (k >= 4 ? Diagonal : Step);
                if (nd < dist[n])
                {
                    dist[n] = nd;
                    queue.Enqueue(n, nd);
                }
            }
        }

        return dist;
    }

    /// <summary>Prepara a heurística ALT para o alvo: distância de cada marco ao nó da malha mais perto do alvo.</summary>
    private void SetAltGoal(MapPoint to)
    {
        _altActive = false;
        if (_landmarks.Length == 0)
        {
            return;
        }

        var ci = (int)MathF.Round((to.X - OffsetX) / Step);
        var cj = (int)MathF.Round((to.Y - OffsetY) / Step);
        var bestNode = -1;
        var bestD = float.MaxValue;
        for (var dj = -3; dj <= 3; dj++)
        {
            for (var di = -3; di <= 3; di++)
            {
                var i = ci + di;
                var j = cj + dj;
                if (i < 0 || j < 0 || i >= Columns || j >= Rows)
                {
                    continue;
                }

                var n = (j * Columns) + i;
                if (!Stand(n) || float.IsPositiveInfinity(_landmarks[0][n]))
                {
                    continue;
                }

                var d = Distance(NodePoint(i, j), to);
                if (d < bestD)
                {
                    bestD = d;
                    bestNode = n;
                }
            }
        }

        if (bestNode < 0)
        {
            return;
        }

        for (var m = 0; m < _landmarks.Length; m++)
        {
            _goalLandmark[m] = _landmarks[m][bestNode];
        }

        // o nó do alvo está até ~3 nós do ponto: tira essa folga para a heurística não passar da distância real
        _altSlack = bestD + (3 * Diagonal);
        _altActive = true;
    }

    private float _altSlack;

    private float AltBound(int node)
    {
        var best = 0f;
        for (var m = 0; m < _landmarks.Length; m++)
        {
            var dn = _landmarks[m][node];
            if (float.IsPositiveInfinity(dn))
            {
                return 0f;
            }

            best = MathF.Max(best, MathF.Abs(_goalLandmark[m] - dn));
        }

        return MathF.Max(0f, best - _altSlack);
    }

    /// <summary>Região ligada de um ponto (a do nó mais perto que ele alcança em linha reta), ou 0 se nenhuma.</summary>
    public int RegionOf(MapPoint p)
    {
        Prepare();
        var ci = (int)MathF.Round((p.X - OffsetX) / Step);
        var cj = (int)MathF.Round((p.Y - OffsetY) / Step);
        for (var ring = 0; ring <= 3; ring++)
        {
            for (var dj = -ring; dj <= ring; dj++)
            {
                for (var di = -ring; di <= ring; di++)
                {
                    if (Math.Max(Math.Abs(di), Math.Abs(dj)) != ring)
                    {
                        continue;
                    }

                    var i = ci + di;
                    var j = cj + dj;
                    if (i >= 0 && j >= 0 && i < Columns && j < Rows && Stand((j * Columns) + i) && IsClear(p, NodePoint(i, j)))
                    {
                        return _label[(j * Columns) + i];
                    }
                }
            }
        }

        return 0;
    }

    /// <summary>Quantos nós tem a região (0 para a região 0, "nenhuma").</summary>
    public int RegionSize(int region)
    {
        Prepare();
        return region > 0 && region < _labelSizes.Count ? _labelSizes[region] : 0;
    }

    /// <summary>
    /// Para cada região com algum nó livre a até <paramref name="maxDistance"/> u de <paramref name="p"/>, o nó mais perto
    /// (distância ao quadrado e ponto), em <paramref name="into"/> (limpo antes).
    /// </summary>
    public void NearestPerRegion(MapPoint p, float maxDistance, Dictionary<int, (float DistanceSq, MapPoint Point)> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        Prepare();
        into.Clear();
        var r = (int)MathF.Ceiling(maxDistance / Step) + 1;
        var ci = (int)MathF.Round((p.X - OffsetX) / Step);
        var cj = (int)MathF.Round((p.Y - OffsetY) / Step);
        var max = maxDistance * maxDistance;
        for (var j = Math.Max(0, cj - r); j <= Math.Min(Rows - 1, cj + r); j++)
        {
            for (var i = Math.Max(0, ci - r); i <= Math.Min(Columns - 1, ci + r); i++)
            {
                var n = (j * Columns) + i;
                if (!Stand(n))
                {
                    continue;
                }

                var q = NodePoint(i, j);
                var d = ((q.X - p.X) * (q.X - p.X)) + ((q.Y - p.Y) * (q.Y - p.Y));
                if (d > max || (into.TryGetValue(_label[n], out var best) && best.DistanceSq <= d))
                {
                    continue;
                }

                into[_label[n]] = (d, q);
            }
        }
    }

    /// <summary>
    /// Ponto "firme" (<see cref="CanStandFirmly"/>) mais perto de <paramref name="p"/>: ele mesmo, senão o nó livre da
    /// malha mais próximo (até <paramref name="maxDistance"/> u). Null se não houver nenhum.
    /// </summary>
    public MapPoint? Settle(MapPoint p, float maxDistance = 160f)
    {
        if (CanStandFirmly(p))
        {
            return p;
        }

        var ci = (int)MathF.Round((p.X - OffsetX) / Step);
        var cj = (int)MathF.Round((p.Y - OffsetY) / Step);
        var maxRing = (int)MathF.Ceiling(maxDistance / Step) + 1;
        var best = -1;
        var bestD = maxDistance * maxDistance;
        for (var ring = 0; ring <= maxRing; ring++)
        {
            // Um nó do anel r está a pelo menos (r - 1)·Step: dá para parar quando nem ele bate o melhor.
            var near = Math.Max(0, ring - 1) * Step;
            if (best >= 0 && near * near > bestD)
            {
                break;
            }

            for (var dj = -ring; dj <= ring; dj++)
            {
                for (var di = -ring; di <= ring; di++)
                {
                    if (Math.Max(Math.Abs(di), Math.Abs(dj)) != ring)
                    {
                        continue;
                    }

                    var i = ci + di;
                    var j = cj + dj;
                    if (i < 0 || j < 0 || i >= Columns || j >= Rows)
                    {
                        continue;
                    }

                    var q = NodePoint(i, j);
                    var d = ((q.X - p.X) * (q.X - p.X)) + ((q.Y - p.Y) * (q.Y - p.Y));
                    if (d < bestD && Stand((j * Columns) + i))
                    {
                        bestD = d;
                        best = (j * Columns) + i;
                    }
                }
            }
        }

        return best < 0 ? null : NodePoint(best % Columns, best / Columns);
    }

    /// <summary>
    /// Caminho de <paramref name="from"/> até <paramref name="to"/> (os dois inclusos, já encurtado) em
    /// <paramref name="result"/>, que é limpo antes. False se não há caminho (regiões diferentes, ou a busca passou de
    /// <see cref="MaxExpansions"/>), e aí <paramref name="result"/> fica vazio.
    /// </summary>
    public bool TryPlan(MapPoint from, MapPoint to, List<MapPoint> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Clear();
        LastExpansions = 0;
        if (!CanStand(from.X, from.Y) || !CanStand(to.X, to.Y))
        {
            return false;
        }

        if (IsClear(from, to))
        {
            result.Add(from);
            result.Add(to);
            return true;
        }

        // Só vale começar por nós de uma região que chega ao destino: entre regiões diferentes falha sem varrer o mapa.
        var goals = GoalRegions(to);
        if (goals == 0)
        {
            return false;
        }

        NextGeneration();
        _open.Clear();
        SetAltGoal(to);
        var gen = _generation;

        // Sementes: nós livres em volta do início a que se chega em linha reta.
        var ci = (int)MathF.Round((from.X - OffsetX) / Step);
        var cj = (int)MathF.Round((from.Y - OffsetY) / Step);
        for (var ring = 1; ring <= 3 && _open.Count == 0; ring++)
        {
            for (var dj = -ring; dj <= ring; dj++)
            {
                for (var di = -ring; di <= ring; di++)
                {
                    var i = ci + di;
                    var j = cj + dj;
                    if (i < 0 || j < 0 || i >= Columns || j >= Rows)
                    {
                        continue;
                    }

                    var n = (j * Columns) + i;
                    if (_seen[n] == gen || !Stand(n) || !HasGoalRegion(_label[n], goals))
                    {
                        continue;
                    }

                    var q = NodePoint(i, j);
                    if (!IsClear(from, q))
                    {
                        continue;
                    }

                    var g = Distance(from, q);
                    _seen[n] = gen;
                    _g[n] = g;
                    _parent[n] = -1;
                    _open.Enqueue(n, g + Estimate(n, q.X, q.Y, to));
                }
            }
        }

        var goalRange = 3.5f * Step;
        while (_open.TryDequeue(out var node, out _))
        {
            if (_closed[node] == gen)
            {
                continue;
            }

            _closed[node] = gen;
            if (++LastExpansions > MaxExpansions)
            {
                return false;
            }

            var ni = node % Columns;
            var nj = node / Columns;
            var p = NodePoint(ni, nj);
            if (Distance(p, to) <= goalRange && IsClear(p, to))
            {
                Build(from, to, node, result);
                return true;
            }

            // Laço quente: bits de ligação já guardados por nó (LinksOf), vizinho por deslocamento no vetor.
            var links = LinksOf(ni, nj, node);
            var gNode = _g[node];
            for (var k = 0; k < Neighbours.Length; k++)
            {
                if ((links & (1 << k)) == 0)
                {
                    continue;
                }

                var (dx, dy) = Neighbours[k];
                var n = node + (dy * Columns) + dx;
                if (_closed[n] == gen)
                {
                    continue;
                }

                var g = gNode + (k >= 4 ? Diagonal : Step);
                if (_seen[n] == gen && g >= _g[n])
                {
                    continue;
                }

                _seen[n] = gen;
                _g[n] = g;
                _parent[n] = node;
                _open.Enqueue(n, g + Estimate(n, OffsetX + ((ni + dx) * Step), OffsetY + ((nj + dy) * Step), to));
            }
        }

        return false;
    }

    /// <summary>
    /// "Puxa o fio": de cada ponto, avança enquanto a reta até o ponto seguinte continua livre (<see cref="IsClear"/>).
    /// </summary>
    public void Smooth(IReadOnlyList<MapPoint> points, List<MapPoint> result)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(result);
        result.Clear();
        if (points.Count == 0)
        {
            return;
        }

        var i = 0;
        result.Add(points[0]);
        while (i < points.Count - 1)
        {
            var next = i + 1;
            while (next + 1 < points.Count && IsClear(points[i], points[next + 1]))
            {
                next++;
            }

            result.Add(points[next]);
            i = next;
        }
    }

    private void Build(MapPoint from, MapPoint to, int goal, List<MapPoint> result)
    {
        _chain.Clear();
        for (var n = goal; n >= 0; n = _parent[n])
        {
            _chain.Add(n);
        }

        // Só as curvas da malha: nós no meio de uma reta (mesma direção antes e depois) não mudam o caminho e deixariam
        // o "puxar o fio" quadrático.
        _raw.Clear();
        _raw.Add(from);
        for (var k = _chain.Count - 1; k >= 0; k--)
        {
            var n = _chain[k];
            if (k > 0 && k < _chain.Count - 1)
            {
                var before = _chain[k + 1];
                var after = _chain[k - 1];
                if (n - before == after - n)
                {
                    continue;
                }
            }

            _raw.Add(NodePoint(n % Columns, n / Columns));
        }

        _raw.Add(to);
        Smooth(_raw, result);
    }

    /// <summary>Regiões dos nós perto de <paramref name="to"/> que o alcançam em linha reta (em <c>_goalLabels</c>).</summary>
    private int GoalRegions(MapPoint to)
    {
        Prepare();
        var count = 0;
        var ci = (int)MathF.Round((to.X - OffsetX) / Step);
        var cj = (int)MathF.Round((to.Y - OffsetY) / Step);
        for (var dj = -3; dj <= 3; dj++)
        {
            for (var di = -3; di <= 3; di++)
            {
                var i = ci + di;
                var j = cj + dj;
                if (i < 0 || j < 0 || i >= Columns || j >= Rows || count >= _goalLabels.Length)
                {
                    continue;
                }

                var n = (j * Columns) + i;
                if (!Stand(n) || HasGoalRegion(_label[n], count) || !IsClear(NodePoint(i, j), to))
                {
                    continue;
                }

                _goalLabels[count++] = _label[n];
            }
        }

        return count;
    }

    private bool HasGoalRegion(int label, int count)
    {
        for (var k = 0; k < count; k++)
        {
            if (_goalLabels[k] == label)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vizinho (dx, dy) do nó (i, j) quando dá para ir de um ao outro: o vizinho e o ponto do meio livres e, na diagonal,
    /// os dois vizinhos ortogonais também (não corta quina). −1 se não dá. As 8 respostas de cada nó ficam guardadas.
    /// </summary>
    private int Link(int i, int j, int dx, int dy)
    {
        var node = (j * Columns) + i;
        var links = LinksOf(i, j, node);
        var bit = NeighbourIndex(dx, dy);
        return (links & (1 << bit)) != 0 ? ((j + dy) * Columns) + i + dx : -1;
    }

    /// <summary>As 8 ligações do nó (bit k = <see cref="Neighbours"/>[k]), calculadas uma vez.</summary>
    private int LinksOf(int i, int j, int node)
    {
        var links = _links[node];
        if (links == 0)
        {
            links = 0x100;
            for (var k = 0; k < Neighbours.Length; k++)
            {
                if (ComputeLink(i, j, Neighbours[k].Dx, Neighbours[k].Dy) >= 0)
                {
                    links |= (ushort)(1 << k);
                }
            }

            _links[node] = links;
        }

        return links;
    }

    private static int NeighbourIndex(int dx, int dy) => (dx, dy) switch
    {
        (1, 0) => 0,
        (-1, 0) => 1,
        (0, 1) => 2,
        (0, -1) => 3,
        (1, 1) => 4,
        (1, -1) => 5,
        (-1, 1) => 6,
        _ => 7,
    };

    private int ComputeLink(int i, int j, int dx, int dy)
    {
        var ti = i + dx;
        var tj = j + dy;
        if (ti < 0 || tj < 0 || ti >= Columns || tj >= Rows)
        {
            return -1;
        }

        var n = (tj * Columns) + ti;
        if (!Stand(n))
        {
            return -1;
        }

        if (dx != 0 && dy != 0 && (!Stand((j * Columns) + ti) || !Stand((tj * Columns) + i)))
        {
            return -1;
        }

        var p = NodePoint(i, j);
        var q = NodePoint(ti, tj);
        return CanStand((p.X + q.X) / 2, (p.Y + q.Y) / 2) ? n : -1;
    }

    private bool Stand(int n)
    {
        var s = _stand[n];
        if (s == Unknown)
        {
            var p = NodePoint(n % Columns, n / Columns);
            s = CanStand(p.X, p.Y) ? Free : Blocked;
            _stand[n] = s;
        }

        return s == Free;
    }

    private void NextGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Array.Clear(_seen);
            Array.Clear(_closed);
            _generation = 0;
        }

        _generation++;
    }

    private static float Distance(MapPoint a, MapPoint b) => WalkMover.Hypot(b.X - a.X, b.Y - a.Y);

    /// <summary>
    /// Distância octil × <see cref="HeuristicWeight"/>: A* "ganancioso" abre bem menos nós; o caminho pode sair um pouco
    /// mais longo que o ótimo, mas é encurtado depois e ninguém nota num passeio pela rua.
    /// </summary>
    private static float Heuristic(MapPoint a, MapPoint b) => Heuristic(a.X, a.Y, b);

    /// <summary>Heurística do nó: a maior entre a octil e a ALT, com o mesmo peso.</summary>
    private float Estimate(int node, float x, float y, MapPoint to)
    {
        var h = Heuristic(x, y, to);
        return _altActive ? MathF.Max(h, HeuristicWeight * AltBound(node)) : h;
    }

    private static float Heuristic(float ax, float ay, MapPoint b)
    {
        var dx = MathF.Abs(b.X - ax);
        var dy = MathF.Abs(b.Y - ay);
        return HeuristicWeight * (MathF.Max(dx, dy) + ((1.41421356f - 1f) * MathF.Min(dx, dy)));
    }
}
