using Godot;
using Vespera.Core.Coop;

namespace Vespera.Game.UI;

/// <summary>
/// Ícone desenhado do dispositivo de um jogador: teclado (com a metade usada acesa quando ele é dividido) ou controle.
/// Linhas creme sobre o índigo, destaque na cor do jogador.
/// </summary>
public partial class DeviceGlyph : Control
{
    private DeviceKind _kind = DeviceKind.Keyboard;
    private bool _split;
    private Color _accent = Palette.Amber;

    public DeviceGlyph(Vector2 size)
    {
        CustomMinimumSize = size;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Qual dispositivo desenhar; <paramref name="split"/> acende só a metade do teclado deste jogador.</summary>
    public void Set(DeviceKind kind, bool split, Color accent)
    {
        _kind = kind;
        _split = split;
        _accent = accent;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_kind == DeviceKind.Gamepad)
        {
            DrawPad();
            return;
        }

        DrawKeyboard();
    }

    private void DrawKeyboard()
    {
        var s = Size;
        var body = new Rect2(new Vector2(4, s.Y * 0.18f), new Vector2(s.X - 8, s.Y * 0.64f));
        DrawRect(body, new Color(Palette.Indigo, 0.9f));
        DrawRect(body, Palette.Cream, filled: false, width: 2f);
        const int cols = 12;
        const int rows = 4;
        var key = new Vector2((body.Size.X - 12) / cols, (body.Size.Y - 12) / rows);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var right = c >= cols / 2;
                var lit = _kind == DeviceKind.KeyboardRight ? right : !_split || !right;
                var rect = new Rect2(body.Position + new Vector2(6 + (c * key.X) + 1.5f, 6 + (r * key.Y) + 1.5f), key - new Vector2(3, 3));
                DrawRect(rect, lit ? new Color(_accent, 0.85f) : new Color(Palette.CreamDim, 0.25f));
            }
        }
    }

    private void DrawPad()
    {
        var s = Size;
        var c = s / 2f;
        var r = s.Y * 0.26f;
        var fill = new Color(Palette.Indigo, 0.9f);
        DrawCircle(c + new Vector2(-s.X * 0.24f, s.Y * 0.04f), r, fill);
        DrawCircle(c + new Vector2(s.X * 0.24f, s.Y * 0.04f), r, fill);
        DrawRect(new Rect2(c - new Vector2(s.X * 0.24f, r * 0.95f), new Vector2(s.X * 0.48f, r * 1.7f)), fill);
        DrawArc(c + new Vector2(-s.X * 0.24f, s.Y * 0.04f), r, Mathf.Pi * 0.45f, Mathf.Pi * 1.55f, 24, Palette.Cream, 2f, true);
        DrawArc(c + new Vector2(s.X * 0.24f, s.Y * 0.04f), r, -Mathf.Pi * 0.55f, Mathf.Pi * 0.55f, 24, Palette.Cream, 2f, true);
        DrawLine(c + new Vector2(-s.X * 0.24f, -r * 0.95f), c + new Vector2(s.X * 0.24f, -r * 0.95f), Palette.Cream, 2f);

        // Direcional à esquerda, quatro botões à direita (o de confirmar aceso).
        var d = c + new Vector2(-s.X * 0.24f, s.Y * 0.02f);
        var arm = r * 0.42f;
        DrawRect(new Rect2(d - new Vector2(arm, arm * 0.33f), new Vector2(arm * 2f, arm * 0.66f)), Palette.Cream);
        DrawRect(new Rect2(d - new Vector2(arm * 0.33f, arm), new Vector2(arm * 0.66f, arm * 2f)), Palette.Cream);
        var b = c + new Vector2(s.X * 0.24f, s.Y * 0.02f);
        var o = r * 0.4f;
        DrawCircle(b + new Vector2(0, o), r * 0.17f, _accent);
        DrawCircle(b + new Vector2(o, 0), r * 0.17f, new Color(Palette.Cream, 0.8f));
        DrawCircle(b + new Vector2(-o, 0), r * 0.17f, new Color(Palette.Cream, 0.8f));
        DrawCircle(b + new Vector2(0, -o), r * 0.17f, new Color(Palette.Cream, 0.8f));
    }
}
