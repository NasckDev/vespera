namespace Vespera.Core.World;

/// <summary>
/// Área andável do mapa mais os obstáculos sólidos postos pelo motor (quebráveis, baús, objetos do catálogo com
/// footprint). A colisão física dos obstáculos é dos próprios corpos; esta área só faz o teste de ponto (e quem planeja
/// caminho: NPCs, bots do smoke, <see cref="WalkPathfinder"/>) enxergar os mesmos corpos que o jogador encontra.
/// O teste de ponto é exato nos polígonos (índice espacial só para achar os candidatos); desligar um obstáculo (quebrável
/// quebrado) libera o lugar. Os polígonos só são registrados antes de a área ser publicada no World; depois, só muda o
/// liga/desliga (um bool), então o planejador em thread pode ler junto.
/// </summary>
public sealed class ObstacleWalkArea : IWalkArea
{
    private readonly IWalkArea _inner;
    private readonly float _bucket;
    private readonly int _cols;
    private readonly int _rows;
    private readonly List<int>?[] _buckets;
    private readonly List<MapPoint[]> _polygons = [];
    private readonly List<MapRect> _bounds = [];
    private bool[] _active = [];

    /// <param name="inner">Área do mapa (máscara ou formas).</param>
    /// <param name="bucket">Lado das células do índice espacial, em u (o teste de ponto é exato no polígono).</param>
    public ObstacleWalkArea(IWalkArea inner, float bucket = 16f)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucket, 0f);
        _inner = inner;
        _bucket = bucket;
        _cols = Math.Max(1, (int)MathF.Ceiling(inner.Width / bucket));
        _rows = Math.Max(1, (int)MathF.Ceiling(inner.Height / bucket));
        _buckets = new List<int>?[_cols * _rows];
    }

    /// <summary>Área sem obstáculos (máscara/formas do mapa).</summary>
    public IWalkArea Inner => _inner;

    public float Width => _inner.Width;

    public float Height => _inner.Height;

    public int ObstacleCount => _polygons.Count;

    public bool IsWalkable(float x, float y) => _inner.IsWalkable(x, y) && !IsObstacle(x, y);

    /// <summary>Colisores estáticos continuam sendo só os do mapa: cada obstáculo já tem o próprio corpo.</summary>
    public IReadOnlyList<MapRect> BlockedRects() => _inner.BlockedRects();

    /// <summary>Ponto dentro de um obstáculo ativo (teste exato no polígono).</summary>
    public bool IsObstacle(float x, float y)
    {
        if (x < 0 || y < 0)
        {
            return false;
        }

        var cx = (int)(x / _bucket);
        var cy = (int)(y / _bucket);
        if (cx >= _cols || cy >= _rows || _buckets[(cy * _cols) + cx] is not { } list)
        {
            return false;
        }

        var active = _active;
        foreach (var id in list)
        {
            if (active[id] && _bounds[id].Contains(x, y) && PolygonMath.Contains(_polygons[id], x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Registra um polígono em u (coordenadas do mapa), já ativo, e devolve o identificador do obstáculo.</summary>
    public int Add(IReadOnlyList<MapPoint> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var id = _polygons.Count;
        var points = polygon.ToArray();
        var b = points.Length >= 3 ? PolygonMath.Bounds(points) : new MapRect(0, 0, 0, 0);
        _polygons.Add(points);
        _bounds.Add(b);
        var active = new bool[id + 1];
        Array.Copy(_active, active, _active.Length);
        active[id] = points.Length >= 3;
        _active = active;
        if (points.Length < 3)
        {
            return id;
        }

        var x0 = Math.Max(0, (int)MathF.Floor(b.X / _bucket));
        var y0 = Math.Max(0, (int)MathF.Floor(b.Y / _bucket));
        var x1 = Math.Min(_cols - 1, (int)MathF.Floor(b.Right / _bucket));
        var y1 = Math.Min(_rows - 1, (int)MathF.Floor(b.Bottom / _bucket));
        for (var cy = y0; cy <= y1; cy++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                (_buckets[(cy * _cols) + cx] ??= []).Add(id);
            }
        }

        return id;
    }

    /// <summary>Liga ou desliga um obstáculo (quebrável quebrado ou restaurado no dia seguinte).</summary>
    public void SetActive(int id, bool active)
    {
        if (id >= 0 && id < _active.Length && _polygons[id].Length >= 3)
        {
            _active[id] = active;
        }
    }

    public bool IsActive(int id) => id >= 0 && id < _active.Length && _active[id];
}
