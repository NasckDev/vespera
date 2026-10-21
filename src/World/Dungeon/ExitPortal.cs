using Godot;
using Vespera.Items;

namespace Vespera.World.Dungeon;

/// <summary>
/// Portal de saída depois do chefe (<c>G.exitPortal</c>, <c>drawPortal</c>, <c>render.js:182-188</c>): abre em
/// <c>por0..por2</c> (0,25 s cada) e pulsa entre <c>por3</c>/<c>por4</c> a cada 0,28 s; [E] "Voltar a Lumeira" (raio 26).
/// </summary>
public partial class ExitPortal : Node2D
{
    private static readonly string[] OpenFrames = ["por0", "por1", "por2"];

    private AtlasSprite _sprite = null!;
    private float _age;

    public CallbackInteractable Interaction { get; private set; } = null!;

    public static ExitPortal Create(Vector2 at)
    {
        var portal = new ExitPortal { Name = "ExitPortal", Position = at };
        portal._sprite = new AtlasSprite { Key = "por0", Position = new Vector2(0, 3) };
        portal.AddChild(portal._sprite);
        portal.Interaction = new CallbackInteractable { Prompt = "Voltar a Lumeira", Radius = 26 };
        portal.AddChild(portal.Interaction);
        return portal;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        _sprite.Key = _age < 0.75f ? OpenFrames[(int)(_age / 0.25f)] : ((int)(_age / 0.28f) % 2 == 1 ? "por3" : "por4");
        var s = 1 + (0.03f * Mathf.Sin(_age * 2.6f));
        _sprite.Scale = Vector2.One * (s / _sprite.Density);
    }
}
