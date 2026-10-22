using Godot;
using Vespera.Game.Actors;

namespace Vespera.Game.World;

/// <summary>
/// Camada invisível de colisão: um <see cref="TileMapLayer"/> com um único tile físico de <see cref="LevelGrid.Cell"/> px,
/// pintado nas células '#' da grade autorada e num anel em volta do mapa (ninguém sai pela borda).
/// </summary>
public static class CollisionLayer
{
    private static readonly Color DebugColor = new(1f, 0.15f, 0.15f, 0.35f);

    public static TileMapLayer Build(LevelGrid grid, bool debugVisible)
    {
        var cell = grid.Cell;
        var image = Image.CreateEmpty(cell, cell, false, Image.Format.Rgba8);
        image.Fill(debugVisible ? DebugColor : new Color(0, 0, 0, 0));
        var tileSet = new TileSet { TileSize = new Vector2I(cell, cell) };
        tileSet.AddPhysicsLayer();
        tileSet.SetPhysicsLayerCollisionLayer(0, Player.WorldLayer);
        tileSet.SetPhysicsLayerCollisionMask(0, 0);

        var source = new TileSetAtlasSource { Texture = ImageTexture.CreateFromImage(image), TextureRegionSize = new Vector2I(cell, cell) };
        var sourceId = tileSet.AddSource(source);
        source.CreateTile(Vector2I.Zero);
        var data = source.GetTileData(Vector2I.Zero, 0);
        var h = cell / 2f;
        data.AddCollisionPolygon(0);
        data.SetCollisionPolygonPoints(0, 0, [new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h)]);

        var layer = new TileMapLayer { Name = "Collision", TileSet = tileSet, ZIndex = 50 };
        for (var r = -1; r <= grid.Rows; r++)
        {
            for (var c = -1; c <= grid.Cols; c++)
            {
                if (grid.IsBlocked(c, r))
                {
                    layer.SetCell(new Vector2I(c, r), sourceId, Vector2I.Zero);
                }
            }
        }

        return layer;
    }
}
