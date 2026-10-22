using Vespera.Core.World;

namespace Vespera.Core.Combat.Ai;

/// <summary>
/// Campo de perseguição (DECISIONS B1-D43): BFS em células de 8 u a partir do jogador, para os monstros contornarem
/// paredes, água e bancas em vez de empacar em linha reta (na v2 o <c>moveEnt</c> só desliza eixo a eixo e monstros
/// nascidos atrás de um obstáculo nunca chegam, o que trava a onda). Só é usado quando a linha reta até o jogador
/// está bloqueada. O BFS cobre o mapa todo, fatiado em alguns frames (<see cref="DefaultBudget"/>) com buffer duplo;
/// as "ilhas" da máscara são rotuladas uma vez por mapa (<see cref="Reachable"/>) para os spawns nunca caírem onde
/// não se chega a pé. Arrays fixos por mapa, grade com borda fechada (sem testes de limite no laço): não aloca.
/// </summary>
public sealed class ChaseField
{
    public const float CellSize = 8f;

    /// <summary>Células processadas por frame (o mapa hd inteiro tem ~25 mil: o campo atualiza em ~4 frames).</summary>
    public const int DefaultBudget = 6000;

    /// <summary>
    /// Raio de passagem das células: o do jogador (4 u). Era o maior raio de movimento de monstro (<c>min(6, r/2)</c>,
    /// <c>core.js:250</c>), mas com as máscaras de 4 u das Brechas redesenhadas (folga de 12 u nas passagens) o centro
    /// de muitas células de 8 u não cabe 6 u, e monstros nascidos ali ficavam fora do campo (Espectro preso na cripta).
    /// Integração v0.1, DECISIONS INT-D6: o campo liga tudo o que o jogador alcança; o monstro maior desliza na quina.
    /// </summary>
    public const float PassRadius = 4f;

    private const ushort Unreached = ushort.MaxValue;

    private readonly IWalkArea _area;
    private readonly int _stride;
    private readonly bool[] _baseOpen;
    private readonly bool[] _open;
    private readonly byte[] _props;
    private readonly int[] _component;
    private readonly int[] _queue;
    private readonly int[] _offsets;
    private ushort[] _dist;
    private ushort[] _build;
    private int _originCell = -1;
    private int _wantedOrigin = -1;
    private int _buildOrigin = -1;
    private int _head;
    private int _tail;
    private bool _building;

    public ChaseField(IWalkArea area)
    {
        _area = area ?? throw new ArgumentNullException(nameof(area));
        Columns = Math.Max(1, (int)MathF.Ceiling(area.Width / CellSize));
        Rows = Math.Max(1, (int)MathF.Ceiling(area.Height / CellSize));
        _stride = Columns + 2;
        var total = _stride * (Rows + 2);
        _baseOpen = new bool[total];
        _open = new bool[total];
        _props = new byte[total];
        _dist = new ushort[total];
        _build = new ushort[total];
        _component = new int[total];
        _queue = new int[total];
        _offsets = [1, -1, _stride, -_stride, _stride + 1, _stride - 1, -_stride + 1, -_stride - 1];
        for (var cy = 0; cy < Rows; cy++)
        {
            for (var cx = 0; cx < Columns; cx++)
            {
                var i = Index(cx, cy);
                _baseOpen[i] = area.CanStand((cx + 0.5f) * CellSize, (cy + 0.5f) * CellSize, PassRadius);
                _open[i] = _baseOpen[i];
            }
        }

        Array.Fill(_dist, Unreached);
        Array.Fill(_build, Unreached);
        LabelComponents();
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>
    /// Segue o jogador: se ele mudou de célula, (re)começa um BFS no buffer de trás e avança até
    /// <paramref name="budget"/> células por chamada; quando termina, o campo novo passa a valer (alguns frames de
    /// atraso, sem picos). Chame uma vez por frame.
    /// </summary>
    public void Update(float x, float y, int budget = DefaultBudget)
    {
        var origin = CellOf(x, y);
        if (origin >= 0)
        {
            _wantedOrigin = origin;
        }

        if (!_building)
        {
            if (_wantedOrigin < 0 || _wantedOrigin == _originCell)
            {
                return;
            }

            StartBuild(_wantedOrigin);
        }

        Advance(budget);
    }

    /// <summary>Calcula o campo inteiro agora (testes e troca de mapa).</summary>
    public void UpdateNow(float x, float y)
    {
        _building = false;
        _originCell = -1;
        Update(x, y, int.MaxValue);
    }

    private void StartBuild(int origin)
    {
        Array.Fill(_build, Unreached);
        _buildOrigin = origin;
        _head = 0;
        _tail = 0;
        _build[origin] = 0; // a célula do jogador pode estar "fechada" (colado na parede): semeia mesmo assim
        _queue[_tail++] = origin;
        _building = true;
    }

    private void Advance(int budget)
    {
        var open = _open;
        var dist = _build;
        var offsets = _offsets;
        var stride = _stride;
        var done = 0;
        while (_head < _tail && done < budget)
        {
            var c = _queue[_head++];
            done++;
            var next = (ushort)(dist[c] + 1);
            for (var k = 0; k < 8; k++)
            {
                var n = c + offsets[k];
                if (!open[n] || dist[n] != Unreached)
                {
                    continue;
                }

                // Diagonal só se os dois lados também abrem (não corta quina).
                if (k >= 4)
                {
                    var dx = (k == 4 || k == 6) ? 1 : -1;
                    var dy = k < 6 ? stride : -stride;
                    if (!open[c + dx] || !open[c + dy])
                    {
                        continue;
                    }
                }

                dist[n] = next;
                _queue[_tail++] = n;
            }
        }

        if (_head < _tail)
        {
            return;
        }

        (_dist, _build) = (_build, _dist);
        _originCell = _buildOrigin;
        _building = false;
    }

    /// <summary>Passos (células) até o jogador, ou −1 se fora do alcance do BFS ou sem caminho.</summary>
    public int Distance(float x, float y)
    {
        var c = CellOf(x, y);
        return c < 0 || _dist[c] == Unreached ? -1 : _dist[c];
    }

    /// <summary>Os dois pontos estão na mesma "ilha" andável (dá para ir a pé de um ao outro).</summary>
    public bool Reachable(float x0, float y0, float x1, float y1)
    {
        var a = ComponentNear(CellOf(x0, y0));
        return a != 0 && a == ComponentNear(CellOf(x1, y1));
    }

    /// <summary>Rótulo da ilha da célula, ou de uma vizinha a até 2 células (ponto colado na parede).</summary>
    private int ComponentNear(int c)
    {
        if (c < 0)
        {
            return 0;
        }

        if (_component[c] != 0)
        {
            return _component[c];
        }

        for (var r = 1; r <= 2; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var n = c + (dy * _stride) + dx;
                    if (n >= 0 && n < _component.Length && _component[n] != 0)
                    {
                        return _component[n];
                    }
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Direção unitária para seguir o campo a partir de (x, y): o vizinho com menor distância. False se a célula não
    /// alcança o jogador (use a linha reta).
    /// </summary>
    public bool TryDirection(float x, float y, out float ux, out float uy)
    {
        ux = uy = 0;
        var c = CellOf(x, y);
        if (c < 0 || _dist[c] == 0)
        {
            return false;
        }

        // O melhor vizinho; colado na parede (célula fechada, sem distância), procura até 3 células em volta.
        var best = _dist[c];
        var bn = -1;
        for (var k = 0; k < 8; k++)
        {
            var n = c + _offsets[k];
            if (_dist[n] < best)
            {
                best = _dist[n];
                bn = n;
            }
        }

        if (bn < 0 && _dist[c] == Unreached)
        {
            for (var r = 2; r <= 3 && bn < 0; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var n = c + (dy * _stride) + dx;
                        if (n >= 0 && n < _dist.Length && _dist[n] < best)
                        {
                            best = _dist[n];
                            bn = n;
                        }
                    }
                }
            }
        }

        if (bn < 0)
        {
            return false;
        }

        var tx = (((bn % _stride) - 1 + 0.5f) * CellSize) - x;
        var ty = (((bn / _stride) - 1 + 0.5f) * CellSize) - y;
        var len = MathF.Sqrt((tx * tx) + (ty * ty));
        if (len < 0.001f)
        {
            return false;
        }

        ux = tx / len;
        uy = ty / len;
        return true;
    }

    /// <summary>
    /// Fecha (ou reabre) as células ocupadas por um prop sólido em (x, y) de raio <paramref name="radius"/>
    /// (barris e baús da sala da Brecha). O próximo <see cref="Update"/> recalcula.
    /// </summary>
    public void SetProp(float x, float y, float radius, bool solid)
    {
        var reach = radius + PassRadius;
        var x0 = Math.Max(0, (int)MathF.Floor((x - reach) / CellSize));
        var x1 = Math.Min(Columns - 1, (int)MathF.Floor((x + reach) / CellSize));
        var y0 = Math.Max(0, (int)MathF.Floor((y - reach) / CellSize));
        var y1 = Math.Min(Rows - 1, (int)MathF.Floor((y + reach) / CellSize));
        for (var cy = y0; cy <= y1; cy++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                var dx = ((cx + 0.5f) * CellSize) - x;
                var dy = ((cy + 0.5f) * CellSize) - y;
                if ((dx * dx) + (dy * dy * 2.2f) >= reach * reach)
                {
                    continue;
                }

                var i = Index(cx, cy);
                _props[i] = (byte)Math.Max(0, _props[i] + (solid ? 1 : -1));
                _open[i] = _baseOpen[i] && _props[i] == 0;
            }
        }

        _originCell = -1;
        _building = false;
    }

    /// <summary>Tira todos os props (troca de sala).</summary>
    public void ClearProps()
    {
        Array.Clear(_props);
        Array.Copy(_baseOpen, _open, _open.Length);
        _originCell = -1;
        _building = false;
    }

    /// <summary>A reta de (x0, y0) a (x1, y1) passa com folga de <see cref="PassRadius"/> (amostras a cada 6 u).</summary>
    public bool LineClear(float x0, float y0, float x1, float y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = MathF.Sqrt((dx * dx) + (dy * dy));
        var steps = (int)(len / 6f);
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)(steps + 1);
            if (!_area.CanStand(x0 + (dx * t), y0 + (dy * t), PassRadius))
            {
                return false;
            }
        }

        return true;
    }

    private void LabelComponents()
    {
        var label = 0;
        for (var start = 0; start < _open.Length; start++)
        {
            if (!_baseOpen[start] || _component[start] != 0)
            {
                continue;
            }

            label++;
            var head = 0;
            var tail = 0;
            _component[start] = label;
            _queue[tail++] = start;
            while (head < tail)
            {
                var c = _queue[head++];
                for (var k = 0; k < 4; k++)
                {
                    var n = c + _offsets[k];
                    if (_baseOpen[n] && _component[n] == 0)
                    {
                        _component[n] = label;
                        _queue[tail++] = n;
                    }
                }
            }
        }
    }

    private int Index(int cx, int cy) => ((cy + 1) * _stride) + cx + 1;

    private int CellOf(float x, float y)
    {
        var cx = (int)MathF.Floor(x / CellSize);
        var cy = (int)MathF.Floor(y / CellSize);
        return cx < 0 || cy < 0 || cx >= Columns || cy >= Rows ? -1 : Index(cx, cy);
    }
}
