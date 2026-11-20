using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.UI;
using Vespera.Game.World;

namespace Vespera.Game.Coop;

/// <summary>
/// Camada de tela do co-op (por cima do mundo, abaixo da HUD): etiqueta "J1"/"J2" na cor de cada jogador sobre a cabeça e,
/// no caçador caído, o anel de reanimação com a dica de quem pode levantá-lo e com que botão. Só desenha com 2+ caçadores.
/// </summary>
public partial class CoopOverlay(Level level) : Control
{
    private const string FontPath = "res://assets/v2/fonts/VT323-Regular.ttf";
    private const int TagFontSize = 30;
    private const int HintFontSize = 28;
    private const float HeadLift = 1.08f;
    private const float RingRadius = 30f;

    private Font _font = null!;
    private float _time;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _font = GD.Load<Font>(FontPath);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var world = level.World;
        if (world is null || !IsInstanceValid(world) || world.Players.Count < 2 || level.DialogueOpen)
        {
            return;
        }

        foreach (var player in world.Players)
        {
            if (!IsInstanceValid(player) || !player.Sprite.Visible)
            {
                continue;
            }

            var color = CoopSession.ColorOf(player.Slot);
            var head = level.WorldToScreen(player.GlobalPosition - new Vector2(0, HeadLift * world.Data.HumanHeight));
            if (player.Combat.IsDowned)
            {
                DrawDowned(world, player, color);
                continue;
            }

            DrawTag(head, CoopSession.TagOf(player.Slot), color);
        }
    }

    private void DrawTag(Vector2 at, string text, Color color)
    {
        var size = _font.GetStringSize(text, HorizontalAlignment.Center, -1, HudText.Px(TagFontSize));
        var box = new Rect2(at - new Vector2((size.X / 2f) + 10f, size.Y + 4f), new Vector2(size.X + 20f, size.Y + 4f));
        DrawRect(box, new Color(Palette.Indigo, 0.82f));
        DrawRect(box, color, filled: false, width: 2f);

        // Pontinha apontando para a cabeça do caçador.
        var tip = new Vector2(at.X, at.Y + 7f);
        DrawColoredPolygon([new Vector2(at.X - 6f, box.End.Y), new Vector2(at.X + 6f, box.End.Y), tip], color);
        DrawString(_font, new Vector2(box.Position.X + 10f, box.End.Y - 7f), text, HorizontalAlignment.Left, -1, HudText.Px(TagFontSize), color);
    }

    private void DrawDowned(LevelWorld world, Player downed, Color color)
    {
        // O anel abraça o caçador caído sem tampá-lo (só o contorno), a etiqueta fica em cima e a dica embaixo dos pés.
        var feet = level.WorldToScreen(downed.GlobalPosition);
        var center = level.WorldToScreen(downed.GlobalPosition - new Vector2(0, 0.38f * world.Data.HumanHeight));
        var ring = (level.WorldToScreen(downed.GlobalPosition) - level.WorldToScreen(downed.GlobalPosition - new Vector2(0, world.Data.HumanHeight))).Length() * 0.55f;
        var beat = 0.5f + (0.5f * Mathf.Sin(_time * 5f));

        // Mancha pulsando no chão: dá para achar o caído no meio da luta.
        DrawSetTransform(feet, 0f, new Vector2(1f, 0.38f));
        DrawArc(Vector2.Zero, (RingRadius * 1.6f) + (6f * beat), 0f, Mathf.Tau, 40, color with { A = 0.35f + (0.3f * beat) }, 4f, true);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        var fraction = world.Party.ReviveFraction(downed);
        DrawArc(center, ring + 5f, 0f, Mathf.Tau, 56, new Color(Palette.Indigo, 0.75f), 4f, true);
        DrawArc(center, ring, 0f, Mathf.Tau, 56, new Color(color, 0.3f), 7f, true);
        if (fraction > 0f)
        {
            DrawArc(center, ring, -Mathf.Pi / 2f, (-Mathf.Pi / 2f) + (Mathf.Tau * fraction), 56, color, 7f, true);
        }

        var tag = CoopSession.TagOf(downed.Slot);
        DrawTag(center - new Vector2(0, ring + 10f), $"{tag} caído", color);

        var helper = world.Party.HelperFor(downed);
        var text = helper is null
            ? $"{tag} caiu! Chegue perto para reanimar"
            : $"{CoopSession.TagOf(helper.Slot)}: segure {InputGlyphs.Describe(helper.Input, InputActions.Interact)} para reanimar";
        var hintColor = helper is null ? Palette.Cream : CoopSession.ColorOf(helper.Slot);
        var size = _font.GetStringSize(text, HorizontalAlignment.Center, -1, HudText.Px(HintFontSize));
        var at = feet + new Vector2(-size.X / 2f, size.Y + 18f);
        DrawRect(new Rect2(at - new Vector2(10f, size.Y - 2f), new Vector2(size.X + 20f, size.Y + 8f)), new Color(Palette.Indigo, 0.78f));
        DrawString(_font, at, text, HorizontalAlignment.Left, -1, HudText.Px(HintFontSize), hintColor);
    }
}
