using Godot;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Forma de água do layout desenhada de novo por cima do mapa com o shader de brilho (<c>water.gdshader</c>): reflexos
/// tremendo em px inteiros e cintilas. Só nas formas <c>water</c>; o custo é um polígono por forma.
/// </summary>
public partial class WaterSurface : Polygon2D
{
    private static Shader? _shader;

    public void Setup(LayoutShape shape, Texture2D mapTexture)
    {
        var points = new Vector2[shape.Polygon.Count];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(shape.Polygon[i].X, shape.Polygon[i].Y);
        }

        Name = $"Water_{(int)shape.Bounds.X}_{(int)shape.Bounds.Y}";
        Texture = mapTexture;
        Polygon = points;
        UV = points;
        TextureFilter = TextureFilterEnum.Nearest;
        _shader ??= GD.Load<Shader>("res://src/World/Shaders/water.gdshader");
        Material = new ShaderMaterial { Shader = _shader };
    }
}
