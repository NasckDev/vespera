using System;
using Godot;
using Vespera.Autoload;
using Vespera.UI.Common;
using Vespera.UI.Widgets;

namespace Vespera.UI.HudKit;

/// <summary>Dicas de controle da primeira vez (frente C1, DECISIONS C1-D13).</summary>
public enum OnboardingStep
{
    Move = 0,
    Interact = 1,
    Attack = 2,
    Dodge = 3,
    Potion = 4,
}

/// <summary>
/// Dicas curtas de controle na primeira vez (C1-D13): andar (logo que o jogo libera o jogador), interagir (primeiro
/// prompt), atacar (primeiro boneco ou onda), esquivar (primeiro golpe levado na Brecha) e poção (vida abaixo de 50%
/// com poção na bolsa). Uma por vez, acima da hotbar, com a tecla do dispositivo em uso (teclado ou controle, já com o
/// remapeamento). Some quando o jogador faz a ação (ou depois de 10 s) e fica marcada em <c>user://settings.cfg</c>
/// (<c>[onboarding]</c>) para não voltar. Nada aloca por quadro.
/// </summary>
public partial class OnboardingHints : PanelContainer
{
    private const string Section = "onboarding";
    private const float MaxSeconds = 10f;
    private const float MoveSecondsToLearn = 0.8f;

    private static readonly string[] Keys = ["move", "interact", "attack", "dodge", "potion"];
    private static readonly bool[] Done = new bool[5];
    private static bool _loaded;

    private readonly KeyCap _cap;
    private readonly Label _text;
    private OnboardingStep? _current;
    private float _t;
    private float _moved;
    private bool _promptShown;
    private bool WantInteract => _promptShown && !_inFight;
    private bool _inFight;
    private bool _wantAttack;
    private bool _wantDodge;

    public OnboardingHints()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ThemeTypeVariation = "SoftPanel";
        _cap = KeyCap.Create(string.Empty, string.Empty, UiFonts.Body);
        _text = UiScaling.Reading(Ui.Text(string.Empty, UiFonts.Body, UiPalette.Creme));
        AddChild(Ui.Row(5, _cap, _text));
        Visible = false;
    }

    /// <summary>Dica na tela agora (smoke test).</summary>
    public OnboardingStep? Current => _current;

    /// <summary>Texto da dica (smoke test).</summary>
    public string Text => _text.Text;

    /// <summary>Tecla mostrada (smoke test).</summary>
    public string KeyText => _cap.Text;

    public static bool IsDone(OnboardingStep step)
    {
        EnsureLoaded();
        return Done[(int)step];
    }

    /// <summary>Esquece as dicas vistas (smoke test, "rever dicas").</summary>
    public static void ResetProgress()
    {
        EnsureLoaded();
        Array.Clear(Done);
        var config = new ConfigFile();
        config.Load(FilePath);
        if (config.HasSection(Section))
        {
            config.EraseSection(Section);
        }

        config.Save(FilePath);
    }

    public override void _EnterTree()
    {
        var bus = EventBus.Instance;
        bus.InteractionPromptChanged += OnPrompt;
        bus.PlayerDamaged += OnDamaged;
        bus.WaveStarted += OnWave;
        bus.BrechaEnded += OnBrechaEnded;
        InputHints.DeviceChanged += RefreshKey;
        InputBindings.Changed += RefreshKey;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.InteractionPromptChanged -= OnPrompt;
        bus.PlayerDamaged -= OnDamaged;
        bus.WaveStarted -= OnWave;
        bus.BrechaEnded -= OnBrechaEnded;
        InputHints.DeviceChanged -= RefreshKey;
        InputBindings.Changed -= RefreshKey;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var game = GameManager.Instance;
        var blocked = game is null || game.IsGameplayBlocked;
        if (_current is { } step)
        {
            if (Learned(step, dt))
            {
                Finish(step);
            }
            else if (step == OnboardingStep.Interact && !WantInteract)
            {
                Dismiss(); // o prompt sumiu (ou a luta começou): a dica volta no próximo
            }
            else if (!blocked)
            {
                _t += dt;
                if (_t >= MaxSeconds)
                {
                    Dismiss(); // não marca: volta numa próxima vez
                }
            }

            Visible = _current is not null && !blocked;
            return;
        }

        Visible = false;
        if (blocked)
        {
            return;
        }

        // Próxima dica que tem motivo para aparecer agora.
        if (!IsDone(OnboardingStep.Move))
        {
            Show(OnboardingStep.Move);
        }
        else if (WantInteract && !IsDone(OnboardingStep.Interact))
        {
            Show(OnboardingStep.Interact);
        }
        else if ((_wantAttack || game!.State.CurrentQuest?.Id == "bonecos") && !IsDone(OnboardingStep.Attack))
        {
            Show(OnboardingStep.Attack);
        }
        else if (_wantDodge && !IsDone(OnboardingStep.Dodge))
        {
            Show(OnboardingStep.Dodge);
        }
        else if (!IsDone(OnboardingStep.Potion) && game!.State is { } s && s.Save.Potions > 0 && s.MaxHp > 0 && s.Hp < s.MaxHp * 0.5)
        {
            Show(OnboardingStep.Potion);
        }
    }

    private bool Learned(OnboardingStep step, float dt)
    {
        switch (step)
        {
            case OnboardingStep.Move:
                if (Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown) != Vector2.Zero)
                {
                    _moved += dt;
                }

                return _moved >= MoveSecondsToLearn;
            case OnboardingStep.Interact:
                return Input.IsActionJustPressed(InputActions.Interact);
            case OnboardingStep.Attack:
                return Input.IsActionJustPressed(InputActions.Attack) || Input.IsActionJustPressed(InputActions.AttackHeavy);
            case OnboardingStep.Dodge:
                return Input.IsActionJustPressed(InputActions.Dodge);
            default:
                return Input.IsActionJustPressed(InputActions.Potion);
        }
    }

    private void Show(OnboardingStep step)
    {
        _current = step;
        _t = 0;
        _moved = 0;
        _text.Text = step switch
        {
            OnboardingStep.Move => UiText.HintMove,
            OnboardingStep.Interact => UiText.HintInteract,
            OnboardingStep.Attack => UiText.HintAttack,
            OnboardingStep.Dodge => UiText.HintDodge,
            _ => UiText.HintPotion,
        };
        RefreshKey();
        Visible = true;
    }

    private void Dismiss()
    {
        _current = null;
        Visible = false;
    }

    private void Finish(OnboardingStep step)
    {
        Done[(int)step] = true;
        var config = new ConfigFile();
        config.Load(FilePath);
        config.SetValue(Section, Keys[(int)step], true);
        config.Save(FilePath);
        switch (step)
        {
            case OnboardingStep.Attack:
                _wantAttack = false;
                break;
            case OnboardingStep.Dodge:
                _wantDodge = false;
                break;
        }

        Dismiss();
    }

    private void RefreshKey()
    {
        if (_current is not { } step)
        {
            return;
        }

        if (step == OnboardingStep.Move)
        {
            var keys = $"{InputBindings.Label(InputActions.MoveUp, false)}{InputBindings.Label(InputActions.MoveLeft, false)}{InputBindings.Label(InputActions.MoveDown, false)}{InputBindings.Label(InputActions.MoveRight, false)}";
            _cap.SetKeys(keys, UiText.HintStick);
            return;
        }

        _cap.SetAction(step switch
        {
            OnboardingStep.Interact => InputActions.Interact,
            OnboardingStep.Attack => InputActions.Attack,
            OnboardingStep.Dodge => InputActions.Dodge,
            _ => InputActions.Potion,
        });
    }

    private void OnPrompt(string prompt) => _promptShown = prompt.Length > 0;

    private void OnWave(int wave, int total)
    {
        _wantAttack = true;
        _inFight = true;
    }

    private void OnBrechaEnded(string themeId, bool cleared) => _inFight = false;

    private void OnDamaged(int amount, int hp, int max) => _wantDodge |= _wantAttack || IsDone(OnboardingStep.Attack);

    private static string FilePath => AudioManager.Instance?.SettingsFile ?? AudioManager.SettingsPath;

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var config = new ConfigFile();
        if (config.Load(FilePath) != Error.Ok)
        {
            return;
        }

        for (var i = 0; i < Keys.Length; i++)
        {
            Done[i] = (bool)config.GetValue(Section, Keys[i], false);
        }
    }
}
