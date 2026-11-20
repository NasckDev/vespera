using Godot;
using Vespera.Game.Combat;

namespace Vespera.Game.Net;

/// <summary>Flecha desenhada na tela do convidado online (só o desenho: quem voa e acerta é o host).</summary>
public partial class ArrowGhost : Node2D
{
    private readonly float _scale;
    private bool _pierce;

    public ArrowGhost(float scale)
    {
        _scale = scale;
        ZIndex = 15;
        Name = "Arrow";
    }

    public bool Pierce
    {
        get => _pierce;
        set
        {
            if (_pierce != value)
            {
                _pierce = value;
                QueueRedraw();
            }
        }
    }

    public override void _Draw() => Arrow.DrawShape(this, _scale, _pierce);
}
