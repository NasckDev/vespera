using Godot;

namespace Vespera.Game.World;

/// <summary>
/// Moldura do mapa: tudo fora do chão pintado vira índigo quase preto e a borda do chão escurece num degradê curto.
/// Quando o mapa é menor que a tela no zoom da régua (interior da forja), ele aparece centralizado nessa moldura
/// (letterbox com vinheta) em vez de mostrar o cinza vazio do viewport.
/// </summary>
public partial class LevelVignette : Node2D
{
    private const float Outside = 6000f;
    private const int Steps = 10;

    private static readonly Color Dark = new(0.035f, 0.03f, 0.075f);

    private readonly Rect2 _bounds;
    private readonly float _edge;

    /// <param name="edge">Largura do degradê na borda do chão, em px de mundo.</param>
    public LevelVignette(Rect2 bounds, float edge)
    {
        _bounds = bounds;
        _edge = edge;
        Name = "Vignette";
    }

    public override void _Draw()
    {
        var b = _bounds;
        DrawRect(new Rect2(b.Position.X - Outside, b.Position.Y - Outside, b.Size.X + (2 * Outside), Outside), Dark);
        DrawRect(new Rect2(b.Position.X - Outside, b.End.Y, b.Size.X + (2 * Outside), Outside), Dark);
        DrawRect(new Rect2(b.Position.X - Outside, b.Position.Y, Outside, b.Size.Y), Dark);
        DrawRect(new Rect2(b.End.X, b.Position.Y, Outside, b.Size.Y), Dark);
        for (var i = 0; i < Steps; i++)
        {
            var inset = _edge * i / Steps;
            var alpha = 0.55f * Mathf.Pow(1f - ((float)i / Steps), 2f) / Steps * 2.2f;
            var band = _edge / Steps;
            var color = Dark with { A = alpha };
            DrawRect(new Rect2(b.Position.X + inset, b.Position.Y + inset, b.Size.X - (2 * inset), band), color);
            DrawRect(new Rect2(b.Position.X + inset, b.End.Y - inset - band, b.Size.X - (2 * inset), band), color);
            DrawRect(new Rect2(b.Position.X + inset, b.Position.Y + inset + band, band, b.Size.Y - (2 * (inset + band))), color);
            DrawRect(new Rect2(b.End.X - inset - band, b.Position.Y + inset + band, band, b.Size.Y - (2 * (inset + band))), color);
        }
    }
}
