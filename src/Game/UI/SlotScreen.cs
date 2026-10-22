using System.Collections.Generic;
using Godot;
using Vespera.Core.Save;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>Escolha do espaço de save para Nova história (confirma antes de sobrescrever) ou Continuar.</summary>
public partial class SlotScreen(SlotScreen.Mode mode) : UiScreen
{
    public enum Mode
    {
        NewGame,
        Continue,
    }

    private Label _status = null!;
    private Button? _firstEnabled;
    private Button _back = null!;

    protected override Control? DefaultFocus() => _firstEnabled ?? _back;

    protected override void Build()
    {
        var title = mode == Mode.NewGame ? "Nova história" : "Continuar";
        var panel = BuildPage(title, out var column, sideMargin: 260);
        var inner = UiKit.MakeColumn(16);
        panel.AddChild(UiKit.PadForGlow(inner));
        inner.AddChild(UiKit.MakeLabel(mode == Mode.NewGame ? "Escolha onde guardar" : "Escolha o espaço", UiTheme.HeadingVariation, HorizontalAlignment.Center));

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
        if (mode == Mode.Continue)
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
        SceneRouter.Instance.GoTo(ScreenKeys.Game);
    }
}
