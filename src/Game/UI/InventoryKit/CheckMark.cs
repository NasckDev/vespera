using Godot;

namespace Vespera.Game.UI;

/// <summary>Marca de pendência (anel vazio) ou cumprida (círculo verde com visto), desenhada em código.</summary>
public partial class CheckMark : Control
{
    private static readonly Color Done = new("7CC47F");
    private bool _checked;

    public CheckMark(bool isChecked, int size = 34)
    {
        _checked = isChecked;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var c = Size / 2f;
        var r = Mathf.Min(Size.X, Size.Y) * 0.45f;
        if (_checked)
        {
            DrawCircle(c, r, new Color(Done, 0.25f));
            DrawArc(c, r, 0, Mathf.Tau, 28, Done, 3f, antialiased: true);
            DrawPolyline([c + (new Vector2(-0.45f, 0.02f) * r), c + (new Vector2(-0.12f, 0.38f) * r), c + (new Vector2(0.5f, -0.34f) * r)], Done, 4f, antialiased: true);
        }
        else
        {
            DrawArc(c, r, 0, Mathf.Tau, 28, new Color(Palette.CreamDim, 0.9f), 3f, antialiased: true);
        }
    }
}
