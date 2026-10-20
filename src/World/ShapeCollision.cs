using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Colisão de mapas sem máscara (Guilda, Estalagem, Núcleo, lojas): retângulos estáticos que cobrem o complemento da
/// área andável de um <see cref="ShapeWalkArea"/> (formas <c>walk</c>/<c>block</c> do maps.json, DECISIONS D2).
/// Defina <see cref="WalkArea"/> antes de adicionar à árvore.
/// </summary>
[GlobalClass]
public partial class ShapeCollision : StaticBody2D
{
    public ShapeWalkArea WalkArea { get; set; } = null!;

    public override void _Ready()
    {
        CollisionLayer = 1; // "world"
        CollisionMask = 0;
        var count = AddRectColliders(this, WalkArea.BlockedRects());
        Log.Debug($"{Name}: {WalkArea.Walk.Count} formas andáveis, {WalkArea.Block.Count} bloqueios → {count} colisores.");
    }

    /// <summary>Um <see cref="RectangleShape2D"/> por retângulo (em u, coordenadas locais do corpo).</summary>
    public static int AddRectColliders(StaticBody2D body, IReadOnlyList<MapRect> rects)
    {
        foreach (var r in rects)
        {
            var size = new Vector2(r.Width, r.Height);
            body.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = size },
                Position = new Vector2(r.X, r.Y) + (size / 2),
            });
        }

        return rects.Count;
    }
}
