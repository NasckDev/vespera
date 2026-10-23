using System.Collections.Generic;
using Godot;
using Vespera.Core.Save;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Escolha do espaço de save para Nova história (confirma antes de sobrescrever), Continuar ou Co-op local (espaço com
/// história continua; vazio começa uma nova). No co-op o J1 é o dono do espaço e o J2 entra como convidado nele.
/// </summary>
public partial class SlotScreen(SlotScreen.Mode mode) : UiScreen
{
    public enum Mode
    {
        NewGame,
        Continue,

        /// <summary>Depois da tela de entrada do co-op: qualquer espaço serve.</summary>
        Coop,
    }

    private Label _status = null!;
    private Button? _firstEnabled;
    private Button _back = null!;

    protected override Control? DefaultFocus() => _firstEnabled ?? _back;

    protected override void Build()
    {
        var title = mode switch
        {
            Mode.NewGame => "Nova história",
            Mode.Coop => "Co-op local",
            _ => "Continuar",
        };
        var panel = BuildPage(title, out var column, sideMargin: 260);
        var inner = UiKit.MakeColumn(16);
        panel.AddChild(UiKit.PadForGlow(inner));
        var caption = mode switch
        {
            Mode.NewGame => "Escolha onde guardar",
            Mode.Coop => "História do J1 (o J2 joga como convidado neste espaço)",
            _ => "Escolha o espaço",
        };
        inner.AddChild(UiKit.MakeLabel(caption, UiTheme.HeadingVariation, HorizontalAlignment.Center));

        var all = new List<Control>();
        for (var slot = 1; slot <= SaveService.SlotCount; slot++)
        {
            var summary = SaveService.Summarize(slot);
            var captured = slot;
            var button = UiKit.MakeButton(Describe(summary), () => Choose(captured, summary.Exists));
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Disabled = mode == Mode.Continue && !summary.Exists;
            _firstEnabled ??= button.Disabled ? null : button;
            inner.AddChild(button);
            all.Add(button);
        }

        _status = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation, HorizontalAlignment.Center);
        inner.AddChild(_status);
        _back = UiKit.MakeButton("Voltar", Back, 360);
        _back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_back);
        all.Add(_back);
        UiKit.WrapFocus(all);
    }

    private static string Describe(SlotSummary s) => s.Exists
        ? $"Espaço {s.Slot}: {s.Name}, dia {s.Day}, {s.SavedAt.ToLocalTime():dd/MM/yyyy HH:mm}"
        : $"Espaço {s.Slot}: vazio";

    private void Choose(int slot, bool exists)
    {
        if (mode == Mode.Continue || (mode == Mode.Coop && exists))
        {
            Begin(slot, SaveService.Load(slot), "Não foi possível ler este save.");
            return;
        }

        if (exists)
        {
            SceneRouter.Instance.Push(new ConfirmScreen($"O espaço {slot} já tem uma história. Sobrescrever?", () => StartNew(slot)));
            return;
        }

        StartNew(slot);
    }

    private void StartNew(int slot) => Begin(slot, SaveService.StartNew(slot), "Não foi possível gravar o save.");

    private void Begin(int slot, SaveData? data, string failure)
    {
        if (data is null)
        {
            _status.Text = failure;
            return;
        }

        GameSession.Begin(slot, data);
        var fresh = mode == Mode.NewGame || (mode == Mode.Coop && data.Story.Flags.Count == 0 && string.IsNullOrEmpty(data.Story.LevelId));
        if (mode == Mode.Coop && Game.Coop.CoopSession.Pending is { } roster)
        {
            Game.Coop.CoopSession.Begin(roster);
            GameSession.Autosave();
        }

        SceneRouter.Instance.GoTo(fresh ? ScreenKeys.Prologue : ScreenKeys.AfterPrologue);
    }
}
