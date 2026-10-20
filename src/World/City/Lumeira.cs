using Godot;
using Vespera.Autoload;

namespace Vespera.World.City;

/// <summary>
/// Hub da cidade: mapa pintado + colisão gerada da máscara. Corrige o spawn se cair em área bloqueada
/// e limita a câmera ao tamanho do mapa.
/// </summary>
public partial class Lumeira : Node2D
{
    private const string LocationName = "Lumeira";

    [Export]
    public Sprite2D Map { get; set; } = null!;

    [Export]
    public MaskCollision Collision { get; set; } = null!;

    [Export]
    public Player.Player Player { get; set; } = null!;

    public override void _Ready()
    {
        Player.Position = Collision.SnapToWalkable(Player.Position);

        var size = Map.Texture.GetSize();
        Player.Camera.LimitLeft = 0;
        Player.Camera.LimitTop = 0;
        Player.Camera.LimitRight = (int)size.X;
        Player.Camera.LimitBottom = (int)size.Y;
        Player.Camera.ResetSmoothing();

        EventBus.Instance.EmitSignal(EventBus.SignalName.LocationChanged, LocationName);
    }
}
