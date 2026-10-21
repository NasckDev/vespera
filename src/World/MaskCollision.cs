using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Gera a colisão de um mapa pintado a partir da máscara (branco = andável, preto = bloqueado).
/// Cada pixel da máscara cobre <see cref="CellSize"/> pixels do mapa. As células bloqueadas são
/// mescladas em retângulos (<see cref="WalkGrid.BlockedRects"/>), então o mapa inteiro vira poucas
/// dezenas/centenas de colisores estáticos. Uma moldura em volta da grade bloqueia o lado de fora
/// (fora da máscara = bloqueado, DECISIONS D1).
/// </summary>
[GlobalClass]
public partial class MaskCollision : StaticBody2D
{
    [Export]
    public Texture2D Mask { get; set; } = null!;

    /// <summary>Grade pronta (layout sem PNG, D40). Se definida antes do _Ready, a <see cref="Mask"/> é ignorada.</summary>
    public WalkGrid? PresetGrid { get; set; }

    [Export]
    public int CellSize { get; set; } = 8;

    /// <summary>Limiar de brilho (0–1) acima do qual a célula é andável.</summary>
    [Export]
    public float WalkableThreshold { get; set; } = 0.5f;

    public WalkGrid Grid { get; private set; } = null!;

    /// <summary>A mesma máscara como área em u (teste de ponto, <c>CanStand</c>, <c>NearestWalkable</c>).</summary>
    public MaskWalkArea WalkArea { get; private set; } = null!;

    public override void _Ready()
    {
        CollisionLayer = 1; // "world"
        CollisionMask = 0;
        Grid = PresetGrid ?? ReadGrid(Mask.GetImage(), WalkableThreshold);
        WalkArea = new MaskWalkArea(Grid, CellSize);

        var count = ShapeCollision.AddRectColliders(this, WalkArea.BlockedRects());
        Log.Debug($"{Name}: {Grid.Width}x{Grid.Height} células → {count} colisores (com moldura).");
    }

    /// <summary>Posição de mundo ajustada para a célula andável mais próxima.</summary>
    public Vector2 SnapToWalkable(Vector2 localPosition)
    {
        var cellX = (int)(localPosition.X / CellSize);
        var cellY = (int)(localPosition.Y / CellSize);
        var nearest = Grid.NearestWalkable(cellX, cellY);
        return nearest is { } cell
            ? (new Vector2(cell.X, cell.Y) * CellSize) + (Vector2.One * CellSize / 2)
            : localPosition;
    }

    public bool IsWalkableAt(Vector2 localPosition) => WalkArea.IsWalkable(localPosition.X, localPosition.Y);

    /// <summary>Lê a máscara: andável = alpha &gt; 0,5 e brilho ≥ <paramref name="threshold"/>.</summary>
    public static WalkGrid ReadGrid(Image image, float threshold)
    {
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        var width = image.GetWidth();
        var height = image.GetHeight();
        var cells = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = image.GetPixel(x, y);
                cells[(y * width) + x] = pixel.A > 0.5f && pixel.V >= threshold;
            }
        }

        return new WalkGrid(width, height, cells);
    }
}
