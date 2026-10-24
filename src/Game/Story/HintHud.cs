using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Story;

/// <summary>
/// Dicas de controle na hora certa (onboarding do playtest 23/10: "sem paredes de texto"): andar ao chegar, atacar na
/// primeira sala, esquivar quando um monstro brilha perto, golpe forte, poção com pouca vida e o Eco. Uma dica por vez,
/// uma linha, com a tecla do dispositivo em uso; some quando o jogador faz a ação e não volta (flag <c>hint_*</c> no save).
/// </summary>
public partial class HintHud : Control
{
    private const float FadeSpeed = 5f;
    private const float MoveSecondsToLearn = 1.2f;
    private const int AttacksToLearn = 3;
    private const float DodgeWarnHumans = 3f;
    private const float MaxShownSeconds = 9f;
    private const float LowY = 820f;
    private const float HighY = 300f;
    private const float PlayerLowLine = 600f;
    private const float SlideSpeed = 2400f;

    /// <summary>Segundos que cada dica já ficou na tela nesta sessão (soma entre fases).</summary>
    private static readonly Dictionary<string, float> ShownSeconds = [];

    private readonly Level _level;
    private PanelContainer _box = null!;
    private Label _label = null!;
    private Hint? _current;
    private float _progress;

    public HintHud(Level level)
    {
        _level = level;
        Name = "Hints";
    }

    private sealed record Hint(string Id, string Action, Func<string, string> Text, Func<bool> When, float Need);

    /// <summary>Texto da dica na tela agora (testes).</summary>
    public string CurrentText => _box.Visible ? _label.Text : string.Empty;

    private Hint[]? _hints;

    private Hint[] Hints => _hints ??=
    [
        new("move", string.Empty, _ => $"{Keys("move_up")}{Keys("move_left")}{Keys("move_down")}{Keys("move_right")} ou analógico: andar",
            () => !RiftSession.InRift && StoryService.Engine.HasFlag(StoryFlags.ArrivalDone), MoveSecondsToLearn),
        new("attack", InputActions.Attack, k => $"{k}: atacar. Vá até o monstro e bata!", () => RiftSession.InRift && AnyEnemy(), AttacksToLearn),
        new("dodge", InputActions.Dodge, k => $"{k}: esquivar. Quando o monstro brilhar laranja, saia do lugar!", () => RiftSession.InRift && Telegraphing(), 1),
        new("heavy", InputActions.Heavy, k => $"Segure {k}: golpe forte (mais lento, bate mais)", () => RiftSession.InRift && RiftSession.RoomIndex >= 1 && AnyEnemy(), 1),
        new("potion", InputActions.Potion, k => $"{k}: beber poção (cura vida)", LowHpWithPotion, 1),
        new("eco", InputActions.Skill, k => $"{k}: Eco Viscoso (avanço com rastro de lodo)", () => HuntService.HasEcoViscoso, 1),
    ];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _box = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false, Modulate = new Color(1, 1, 1, 0) };
        var style = new StyleBoxFlat { BgColor = new Color(Palette.Indigo, 0.9f), BorderColor = Palette.Amber, AntiAliasing = true };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 26;
        style.ContentMarginTop = style.ContentMarginBottom = 10;
        _box.AddThemeStyleboxOverride("panel", style);
        _label = UiKit.MakeLabel(string.Empty, UiTheme.ReadingVariation, HorizontalAlignment.Center);
        _box.AddChild(_label);
        AddChild(_box);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var blocked = _level.DialogueOpen || _level.SceneBusy || !Progression.IsActive || GetTree().Paused;
        if (_current is null && !blocked)
        {
            _current = Hints.FirstOrDefault(h => !Seen(h.Id) && h.When());
            _progress = 0f;
            if (_current is not null)
            {
                _label.Text = _current.Text(Keys(_current.Action));
                _box.ResetSize();
                _box.Size = _box.GetCombinedMinimumSize();
                _box.Position = new Vector2((1920f - _box.Size.X) / 2f, HintY(LowY));
            }
        }

        if (_current is not null && !blocked)
        {
            // Fora do eixo do caçador: embaixo quando ele está na metade de cima da tela, em cima quando está embaixo.
            var y = Mathf.MoveToward(_box.Position.Y, HintY(_box.Position.Y), SlideSpeed * dt);
            _box.Position = new Vector2(_box.Position.X, y);
            Learn(_current, dt);
        }

        var show = _current is not null && !blocked;
        var alpha = Mathf.MoveToward(_box.Modulate.A, show ? 1f : 0f, FadeSpeed * dt);
        _box.Modulate = new Color(1, 1, 1, alpha);
        _box.Visible = alpha > 0.01f;
    }

    private void Learn(Hint hint, float dt)
    {
        if (hint.Id == "move")
        {
            _progress += _level.Player.Velocity.LengthSquared() > 1f ? dt : 0f;
        }
        else if (Input.IsActionJustPressed(hint.Action) || (hint.Id == "heavy" && Input.IsActionPressed(hint.Action)))
        {
            _progress += 1f;
        }

        // Dica vista tempo bastante conta como aprendida (painel 23/10: "J: atacar" seguia na Lodo-Mãe para quem atacava
        // pelo mouse ou já sabia). O tempo soma entre fases, então ela não volta numa sala nova.
        ShownSeconds[hint.Id] = ShownSeconds.GetValueOrDefault(hint.Id) + dt;
        if (_progress >= hint.Need || ShownSeconds[hint.Id] >= MaxShownSeconds)
        {
            StoryService.Engine.SetFlag(StoryFlags.HintPrefix + hint.Id);
            _current = null;
        }
    }

    /// <summary>Altura da dica: a faixa de baixo, ou a de cima quando o caçador está na parte de baixo da tela.</summary>
    private float HintY(float fallback)
    {
        if (!IsInstanceValid(_level.Player))
        {
            return fallback;
        }

        var screen = _level.WorldToScreen(_level.Player.GlobalPosition);
        return screen.Y > PlayerLowLine ? HighY : LowY;
    }

    private static bool Seen(string id) => StoryService.Engine.HasFlag(StoryFlags.HintPrefix + id);

    private static string Keys(string action) => action.Length == 0 ? string.Empty : InputDevices.KeyName(action);

    private bool AnyEnemy() => _level.World.Enemies.Any(e => IsInstanceValid(e) && !e.Dead);

    private bool Telegraphing()
    {
        var near = DodgeWarnHumans * _level.World.Data.HumanHeight;
        var at = _level.Player.GlobalPosition;
        return _level.World.Enemies.Any(e => IsInstanceValid(e) && e.State == EnemyState.Telegraph && e.GlobalPosition.DistanceTo(at) < near);
    }

    private bool LowHpWithPotion()
    {
        var c = _level.Player.Combat;
        return HuntService.State.Potions > 0 && c.Hp < c.MaxHp / 2;
    }
}
