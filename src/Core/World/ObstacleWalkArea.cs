namespace Vespera.Core.World;

/// <summary>
/// Área andável do mapa mais os obstáculos sólidos postos pelo motor (quebráveis, baús, objetos do catálogo com
/// footprint). A colisão física dos obstáculos é dos próprios corpos; esta área só faz o teste de ponto (e quem planeja
/// caminho: NPCs, bots do smoke, <see cref="WalkPathfinder"/>) enxergar os mesmos corpos que o jogador encontra.
/// Os obstáculos são carimbados numa grade fina; desligar um (quebrável quebrado) libera as células de novo.
/// Leitura concorrente (planejador em thread) é segura: cada célula é um byte e só muda no thread principal.
/// </summary>
public sealed class ObstacleWalkArea : IWalkArea
{
    private readonly IWalkArea _inner;
    private readonly float _cell;
    private readonly int _cols;
    private readonly int _rows;
    private readonly byte[] _count;
    private readonly List<int[]> _cells = [];
    private readonly List<bool> _active = [];

    public ObstacleWalkArea(IWalkArea inner, float cell = 2f)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cell, 0f);
        _inner = inner;
        _cell = cell;
        _cols = Math.Max(1, (int)MathF.Ceiling(inner.Width / cell));
        _rows = Math.Max(1, (int)MathF.Ceiling(inner.Height / cell));
        _count = new byte[_cols * _rows];
    }

    /// <summary>Área sem obstáculos (máscara/formas do mapa).</summary>
    public IWalkArea Inner => _inner;

    public float Width => _inner.Width;

    public float Height => _inner.Height;

    public int ObstacleCount => _cells.Count;

    public bool IsWalkable(float x, float y) => _inner.IsWalkable(x, y) && !IsObstacle(x, y);

    /// <summary>Colisores estáticos continuam sendo só os do mapa: cada obstáculo já tem o próprio corpo.</summary>
    public IReadOnlyList<MapRect> BlockedRects() => _inner.BlockedRects();

    /// <summary>Ponto dentro de um obstáculo ativo.</summary>
    public bool IsObstacle(float x, float y)
    {
        if (x < 0 || y < 0)
        {
            return false;
        }

        var cx = (int)(x / _cell);
        var cy = (int)(y / _cell);
        return cx < _cols && cy < _rows && _count[(cy * _cols) + cx] > 0;
    }

    /// <summary>Carimba um polígono em u (coordenadas do mapa) e devolve o identificador do obstáculo.</summary>
    public int Add(IReadOnlyList<MapPoint> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var cells = new List<int>();
        if (polygon.Count >= 3)
        {
            var b = PolygonMath.Bounds(polygon);
            var x0 = Math.Max(0, (int)MathF.Floor(b.X / _cell));
            var y0 = Math.Max(0, (int)MathF.Floor(b.Y / _cell));
            var x1 = Math.Min(_cols - 1, (int)MathF.Floor(b.Right / _cell));
            var y1 = Math.Min(_rows - 1, (int)MathF.Floor(b.Bottom / _cell));
            for (var cy = y0; cy <= y1; cy++)
            {
                for (var cx = x0; cx <= x1; cx++)
                {
                    if (PolygonMath.Contains(polygon, (cx + 0.5f) * _cell, (cy + 0.5f) * _cell))
                    {
                        cells.Add((cy * _cols) + cx);
                    }
                }
            }
        }

        _cells.Add([.. cells]);
        _active.Add(false);
        var id = _cells.Count - 1;
        SetActive(id, true);
        return id;
    }

    /// <summary>Liga ou desliga um obstáculo (quebrável quebrado ou restaurado no dia seguinte).</summary>
    public void SetActive(int id, bool active)
    {
        if (id < 0 || id >= _cells.Count || _active[id] == active)
        {
            return;
        }

        _active[id] = active;
        foreach (var c in _cells[id])
        {
            _count[c] = (byte)Math.Clamp(_count[c] + (active ? 1 : -1), 0, 255);
        }
    }

    public bool IsActive(int id) => id >= 0 && id < _active.Count && _active[id];
}
