using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Cena de jogo provisória da Camada 0: uma sala colorida com um marcador movido por <see cref="IInputSource"/>
/// (prova do desacoplamento do input) e a pausa. A Camada 1 substitui isto pelo piso em tiles.
/// </summary>
public partial class GameRoom : Control
{
    private const float Speed = 360f;
    private const int Margin = 96;
    private static readonly Vector2 PlayerSize = new(48, 64);

    private static readonly Rect2 Floor = new(Margin, Margin + 32, 1920 - (2 * Margin), 1080 - (2 * Margin) - 32);

    private readonly List<IInputSource> _sources = [new LocalInputSource(LocalInputSource.KeyboardMouse)];
    private Vector2 _player = new(960, 600);

    /// <summary>Posição do marcador (pé), exposta para o smoke test.</summary>
    public Vector2 PlayerPosition => _player;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (var pad in Input.GetConnectedJoypads())
        {
            _sources.Add(new LocalInputSource(pad));
        }

        var hint = new Label { Text = "Camada 1 vem aqui", ThemeTypeVariation = UiTheme.TitleVariation, HorizontalAlignment = HorizontalAlignment.Center };
        hint.Position = new Vector2(0, 360);
        hint.Size = new Vector2(1920, 130);
        AddChild(hint);

        var help = new Label { Text = "Esc ou Start: pausa", ThemeTypeVariation = UiTheme.DimVariation, HorizontalAlignment = HorizontalAlignment.Center };
        help.Position = new Vector2(0, Margin + 48);
        help.Size = new Vector2(1920, 48);
        AddChild(help);

        GameSession.Autosave();
    }

    public override void _PhysicsProcess(double delta)
    {
        var move = Vector2.Zero;
        foreach (var source in _sources)
        {
            if (source.Move.LengthSquared() > move.LengthSquared())
            {
                move = source.Move;
            }
        }

        var next = _player + (move * Speed * (float)delta);
        _player = new Vector2(
            Mathf.Clamp(next.X, Floor.Position.X + (PlayerSize.X / 2), Floor.End.X - (PlayerSize.X / 2)),
            Mathf.Clamp(next.Y, Floor.Position.Y + PlayerSize.Y, Floor.End.Y));
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(InputActions.Pause) || !SceneRouter.Instance.IsActive(this))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        GetTree().Paused = true;
        SceneRouter.Instance.Push(ScreenKeys.Pause);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1920, 1080), Palette.Indigo);
        DrawRect(Floor, new Color("8A5A3C"));
        DrawRect(Floor, Palette.Purple, filled: false, width: 12f);
        DrawRect(new Rect2(Floor.Position + new Vector2(64, 64), new Vector2(96, 96)), new Color("5C8C4A"));
        DrawRect(new Rect2(Floor.End - new Vector2(160, 160), new Vector2(96, 96)), new Color("3C7A8A"));

        var origin = (_player - new Vector2(PlayerSize.X / 2, PlayerSize.Y)).Round();
        DrawRect(new Rect2(origin, PlayerSize), Palette.Amber);
        DrawRect(new Rect2(origin, PlayerSize), Palette.Cream, filled: false, width: 3f);
    }
}
