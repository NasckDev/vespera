namespace Vespera.Core.World;

/// <summary>O que aconteceu num passo do <see cref="WalkFollower"/>.</summary>
public enum WalkStatus
{
    /// <summary>Andou (ou desviou) em direção ao próximo ponto.</summary>
    Moving,

    /// <summary>Chegou ao fim do caminho (ou não tem caminho).</summary>
    Arrived,

    /// <summary>Tem alguém parado na frente e não dá para desviar: espere <see cref="WalkFollower.YieldSeconds"/>.</summary>
    Yielding,

    /// <summary>Não sai do lugar há <see cref="WalkFollower.StuckSeconds"/>: replaneje.</summary>
    Stuck,
}

/// <summary>
/// Segue um caminho de pontos (do <see cref="WalkPlanner"/> ou do <see cref="WalkerRoute"/>) na velocidade pedida, sem
/// nunca entrar em área bloqueada (B2-D48):
/// <list type="bullet">
/// <item>chega exatamente em cada ponto (sem erro de float acumulado) e passa para o seguinte no mesmo passo;</item>
/// <item>bloqueado por um fio, desliza de lado até <see cref="NudgeMax"/> u (a correção de quina do jogador) e, se nem
/// isso, escorrega na parede como o <c>moveEnt</c> da v2 (<see cref="WalkMover.Move"/>);</item>
/// <item>desvia de outros moradores e do jogador à frente; sem espaço, cede a vez; depois de
/// <see cref="GhostAfterSeconds"/> cedendo, passa por eles como a v2 (nunca trava esperando);</item>
/// <item>sem progresso por <see cref="StuckSeconds"/>, avisa <see cref="WalkStatus.Stuck"/> para o dono replanejar.</item>
/// </list>
/// Nada é alocado por passo.
/// </summary>
public sealed class WalkFollower
{
    /// <summary>Maior deslize de lado para contornar uma quina (u).</summary>
    public const float NudgeMax = 3f;

    /// <summary>Distância lateral abaixo da qual outro corpo à frente atrapalha (u).</summary>
    public const float PersonalSpace = 10f;

    /// <summary>Até onde olha à frente procurando quem está no caminho (u).</summary>
    public const float LookAhead = 18f;

    /// <summary>Mais perto que isto, à frente e quase na mesma linha, não dá para desviar: cede a vez.</summary>
    public const float TooClose = 8f;

    /// <summary>Quanto tempo cede a vez de cada vez.</summary>
    public const double YieldSeconds = 0.35;

    /// <summary>Cedendo por tanto tempo seguido, passa por quem está na frente.</summary>
    public const double GhostAfterSeconds = 2.0;

    /// <summary>Por quanto tempo ignora os outros depois de desistir de esperar.</summary>
    public const double GhostSeconds = 2.5;

    /// <summary>Sem progresso por tanto tempo = travado.</summary>
    public const double StuckSeconds = 0.3;

    /// <summary>
    /// Andando sem chegar mais perto do próximo ponto (ir e voltar numa quina, escorregar de um lado para o outro) por
    /// tanto tempo também é travado.
    /// </summary>
    public const double NoApproachSeconds = 1.5;

    private readonly List<MapPoint> _path = [];
    private int _index;
    private double _noProgress;
    private double _yielded;
    private double _ghost;
    private int _watchIndex = -1;
    private float _bestDistance;
    private double _sinceBest;

    public WalkFollower(float x, float y, float radius = WalkMover.NpcRadius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float Radius { get; }

    /// <summary>Para onde o sprite olha. Definir de fora (falar com o jogador, parar) reinicia a direção suavizada.</summary>
    public TownFacing Facing
    {
        get => _facing;
        set
        {
            _facing = value;
            (_hx, _hy) = value switch
            {
                TownFacing.Up => (0f, -1f),
                TownFacing.Left => (-1f, 0f),
                TownFacing.Right => (1f, 0f),
                _ => (0f, 1f),
            };
        }
    }

    /// <summary>Tempo andando (anima o passo).</summary>
    public double WalkT { get; private set; }

    /// <summary>Quanto andou no último passo (u).</summary>
    public float LastMoved { get; private set; }

    /// <summary>Pontos do caminho atual (o primeiro costuma ser onde começou).</summary>
    public IReadOnlyList<MapPoint> Path => _path;

    /// <summary>Índice do próximo ponto a alcançar.</summary>
    public int NextIndex => _index;

    public bool HasPath => _index < _path.Count;

    /// <summary>Último ponto do caminho atual (null sem caminho).</summary>
    public MapPoint? Destination => _path.Count > 0 ? _path[^1] : null;

    /// <summary>Ignorando os outros corpos agora (depois de esperar demais).</summary>
    public bool IsGhosting => _ghost > 0;

    /// <summary>Põe o corpo num ponto sem animar (spawn, correção).</summary>
    public void Place(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void ClearPath()
    {
        _path.Clear();
        _index = 0;
        _noProgress = 0;
        _watchIndex = -1;
    }

    /// <summary>Copia <paramref name="points"/> a partir de <paramref name="startIndex"/> como caminho atual.</summary>
    public void SetPath(IReadOnlyList<MapPoint> points, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(points);
        _path.Clear();
        for (var i = Math.Max(0, startIndex); i < points.Count; i++)
        {
            _path.Add(points[i]);
        }

        _index = 0;
        _noProgress = 0;
        _watchIndex = -1;
    }

    /// <summary>Planeja daqui até <paramref name="to"/>. False = não há caminho (o atual é descartado).</summary>
    public bool Plan(WalkPlanner planner, MapPoint to)
    {
        ArgumentNullException.ThrowIfNull(planner);

        // Nasceu (ou foi empurrado) onde o corpo não cabe, ex.: sorteado com sondas de 8 u ao lado de um poste de uma
        // célula. O A* recusa essa partida e o morador ficaria parado para sempre: desencaixa para o ponto de pé mais
        // perto (até 16 u) antes de planejar.
        if (!planner.CanStand(X, Y) && planner.Area.NearestWalkable(X, Y, planner.Radius, maxDistance: 16f) is { } free)
        {
            Place(free.X, free.Y);
        }

        if (!planner.TryPlan(new MapPoint(X, Y), to, _path))
        {
            ClearPath();
            return false;
        }

        _index = _path.Count > 1 ? 1 : 0;
        _noProgress = 0;
        _watchIndex = -1;
        return true;
    }

    /// <summary>
    /// Um passo de <paramref name="dt"/> s a <paramref name="speed"/> u/s. <paramref name="others"/> são as posições de
    /// outros corpos (moradores, jogador); <paramref name="self"/> é o índice deste corpo nela (ou −1).
    /// </summary>
    public WalkStatus Advance(IWalkArea area, float speed, double dt, IReadOnlyList<MapPoint>? others = null, int self = -1, float arriveDistance = 3f)
    {
        ArgumentNullException.ThrowIfNull(area);
        LastMoved = 0;
        if (_ghost > 0)
        {
            _ghost -= dt;
        }

        if (!HasPath)
        {
            return WalkStatus.Arrived;
        }

        var budget = (float)(speed * dt);
        var full = budget;
        var moved = 0f;
        float fx = 0, fy = 0;
        for (var guard = 0; guard < 8 && budget > 1e-5f && HasPath; guard++)
        {
            var t = _path[_index];
            var dx = t.X - X;
            var dy = t.Y - Y;
            var d = WalkMover.Hypot(dx, dy);
            if (d <= budget && area.CanStand(t.X, t.Y, Radius))
            {
                X = t.X;
                Y = t.Y;
                budget -= d;
                moved += d;
                if (d > 1e-4f)
                {
                    fx = dx;
                    fy = dy;
                }

                _index++;
                continue;
            }

            if (d <= 1e-4f)
            {
                _index++;
                continue;
            }

            var ux = dx / d;
            var uy = dy / d;
            var s = MathF.Min(budget, d);
            if (others is not null && _ghost <= 0 && Avoid(area, others, self, ref ux, ref uy, s))
            {
                _yielded += YieldSeconds;
                if (_yielded >= GhostAfterSeconds)
                {
                    _yielded = 0;
                    _ghost = GhostSeconds;
                }

                return WalkStatus.Yielding; // parado esperando: o sprite não vira para o desvio
            }

            moved += Step(area, ux * s, uy * s, s);
            fx = ux;
            fy = uy;
            break;
        }

        LastMoved = moved;
        if (moved > 1e-4f)
        {
            WalkT += dt;
            Face(fx, fy, dt);
        }

        if (!HasPath)
        {
            _yielded = 0;
            return WalkStatus.Arrived;
        }

        _noProgress = moved < 0.25f * full ? _noProgress + dt : 0;
        if (moved >= 0.25f * full)
        {
            _yielded = Math.Max(0, _yielded - dt);
        }

        // Vigia: a distância até o ponto da vez tem que cair de vez em quando.
        var next = _path[_index];
        var distance = WalkMover.Hypot(next.X - X, next.Y - Y);
        if (_watchIndex != _index || distance < _bestDistance - 0.5f)
        {
            _watchIndex = _index;
            _bestDistance = distance;
            _sinceBest = 0;
        }
        else
        {
            _sinceBest += dt;
        }

        if (_noProgress < StuckSeconds && _sinceBest < NoApproachSeconds)
        {
            return WalkStatus.Moving;
        }

        _noProgress = 0;
        _watchIndex = -1;
        if (_index == _path.Count - 1 && WalkMover.Hypot(_path[^1].X - X, _path[^1].Y - Y) < arriveDistance)
        {
            _index = _path.Count; // perto o bastante do fim (d < 3 da v2)
            return WalkStatus.Arrived;
        }

        return WalkStatus.Stuck;
    }

    /// <summary>Anda (sx, sy); bloqueado, desliza de lado até <see cref="NudgeMax"/> u ou escorrega na parede. Devolve quanto andou.</summary>
    private float Step(IWalkArea area, float sx, float sy, float s)
    {
        if (area.CanStand(X + sx, Y + sy, Radius))
        {
            X += sx;
            Y += sy;
            return s;
        }

        // Correção de quina: o menor deslize de lado que deixa o passo inteiro livre.
        var ux = sx / s;
        var uy = sy / s;
        var px = -uy;
        var py = ux;
        for (var off = 0.25f; off <= NudgeMax; off += 0.25f)
        {
            for (var sign = 1; sign >= -1; sign -= 2)
            {
                var ox = px * off * sign;
                var oy = py * off * sign;
                if (!area.CanStand(X + ox, Y + oy, Radius) || !area.CanStand(X + ox + sx, Y + oy + sy, Radius))
                {
                    continue;
                }

                // Só o que cabe no passo: nunca anda mais rápido que a velocidade.
                var lateral = MathF.Min(off, s);
                var nx = X + (px * lateral * sign);
                var ny = Y + (py * lateral * sign);
                if (lateral < off && !area.CanStand(nx, ny, Radius))
                {
                    continue; // o deslize parcial (limitado ao passo) cai entre dois pontos livres, mas dentro da quina
                }
                var forward = MathF.Sqrt(MathF.Max(0f, (s * s) - (lateral * lateral)));
                if (forward > 1e-4f && area.CanStand(nx + (ux * forward), ny + (uy * forward), Radius))
                {
                    nx += ux * forward;
                    ny += uy * forward;
                }

                X = nx;
                Y = ny;
                return s;
            }
        }

        var (mx, my) = WalkMover.Move(area, X, Y, sx, sy, Radius);
        var moved = WalkMover.Hypot(mx - X, my - Y);
        X = mx;
        Y = my;
        return moved;
    }

    /// <summary>
    /// Desvio de quem está à frente: inclina a direção para o lado oposto ao do outro corpo. Devolve true quando não dá
    /// para desviar (é para ceder a vez).
    /// </summary>
    private bool Avoid(IWalkArea area, IReadOnlyList<MapPoint> others, int self, ref float ux, ref float uy, float s)
    {
        var best = -1;
        var bestAlong = float.MaxValue;
        var bestLateral = 0f;
        for (var k = 0; k < others.Count; k++)
        {
            if (k == self)
            {
                continue;
            }

            var o = others[k];
            var rx = o.X - X;
            var ry = o.Y - Y;
            var along = (rx * ux) + (ry * uy);
            if (along <= 0.5f || along > LookAhead)
            {
                continue;
            }

            var lateral = (rx * -uy) + (ry * ux);
            if (MathF.Abs(lateral) >= PersonalSpace || along >= bestAlong)
            {
                continue;
            }

            best = k;
            bestAlong = along;
            bestLateral = lateral;
        }

        if (best < 0)
        {
            return false;
        }

        // Para o lado oposto ao do outro; de frente (lateral ~0) todo mundo vai pela própria direita.
        var side = bestLateral > 0.5f ? -1f : 1f;
        var k2 = (PersonalSpace - MathF.Abs(bestLateral)) / PersonalSpace * 1.4f;
        var nx = ux + (-uy * side * k2);
        var ny = uy + (ux * side * k2);
        var len = WalkMover.Hypot(nx, ny);
        nx /= len;
        ny /= len;
        if (area.CanStand(X + (nx * s), Y + (ny * s), Radius))
        {
            ux = nx;
            uy = ny;
            return false;
        }

        return bestAlong < TooClose;
    }

    /// <summary>
    /// Parado (esperando num ponto da rotina, cedendo a vez): se outro corpo está a menos de <see cref="TooClose"/>, dá
    /// um passinho para longe (até 12 u/s, sem entrar em parede). Dois moradores que esperam no mesmo ponto da rotina
    /// ficavam um dentro do outro por segundos.
    /// </summary>
    public void Separate(IWalkArea area, IReadOnlyList<MapPoint>? others, int self, double dt)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (others is null)
        {
            return;
        }

        float px = 0, py = 0;
        for (var k = 0; k < others.Count; k++)
        {
            if (k == self)
            {
                continue;
            }

            var rx = X - others[k].X;
            var ry = Y - others[k].Y;
            var d = WalkMover.Hypot(rx, ry);
            if (d >= TooClose)
            {
                continue;
            }

            if (d < 1e-3f)
            {
                // mesmo ponto: cada um sai para um lado (pela ordem na lista)
                (rx, ry, d) = (k < self ? 1f : -1f, 0f, 1f);
            }

            var w = (TooClose - d) / TooClose;
            px += rx / d * w;
            py += ry / d * w;
        }

        var len = WalkMover.Hypot(px, py);
        if (len < 1e-4f)
        {
            return;
        }

        var step = SeparateSpeed * (float)dt;
        var nx = X + (px / len * step);
        var ny = Y + (py / len * step);
        if (area.CanStand(nx, ny, Radius))
        {
            X = nx;
            Y = ny;
        }
    }

    private const float SeparateSpeed = 12f;

    /// <summary>
    /// Direção do sprite a partir do passo, suavizada (constante de 0,1 s) e com histerese entre os eixos: o desvio de
    /// outros corpos entorta o passo frame a frame, e sem isso o sprite piscava entre frente/costas e lado.
    /// </summary>
    private void Face(float dx, float dy, double dt)
    {
        var len = WalkMover.Hypot(dx, dy);
        if (len <= 1e-5f)
        {
            return;
        }

        var k = 1f - MathF.Exp(-(float)dt / FacingSmoothSeconds);
        _hx += ((dx / len) - _hx) * k;
        _hy += ((dy / len) - _hy) * k;
        var ax = MathF.Abs(_hx);
        var ay = MathF.Abs(_hy);
        var horizontal = _facing is TownFacing.Left or TownFacing.Right;
        if (horizontal ? ay > ax * FacingHysteresis : ax <= ay * FacingHysteresis)
        {
            if (ay > 1e-3f)
            {
                _facing = _hy < 0 ? TownFacing.Up : TownFacing.Down;
            }
        }
        else if (ax > 1e-3f)
        {
            _facing = _hx < 0 ? TownFacing.Left : TownFacing.Right;
        }
    }

    private const float FacingSmoothSeconds = 0.1f;
    private const float FacingHysteresis = 1.3f;
    private TownFacing _facing = TownFacing.Down;
    private float _hx;
    private float _hy = 1f;
}
