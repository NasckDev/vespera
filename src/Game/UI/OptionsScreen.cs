using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>Opções: volumes, tela cheia, escala da janela, tremor de tela, reduzir flashes e tamanho do texto.</summary>
public partial class OptionsScreen : UiScreen
{
    private const int VolumeStep = 10;
    private const int ShakeStep = 10;

    private StepRow _first = null!;

    protected override Control? DefaultFocus() => _first;

    protected override void Build()
    {
        var panel = BuildPage("Opções", out var column);
        var scroll = UiKit.MakeScroll();
        panel.AddChild(scroll);
        var list = UiKit.MakeColumn(12);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(list));

        var rows = new List<Control>();
        foreach (var row in BuildRows())
        {
            list.AddChild(row);
            rows.Add(row);
        }

        _first = (StepRow)rows[0];
        var back = UiKit.MakeButton("Voltar", Back, 360);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(back);
        rows.Add(back);
        UiKit.WrapFocus(rows);
    }

    private IEnumerable<StepRow> BuildRows()
    {
        var s = GameSettings.Current;
        yield return new StepRow("Volume geral", () => $"{s.MasterVolume}%", d => Change(() => s.MasterVolume = Step(s.MasterVolume, d, VolumeStep)));
        yield return new StepRow("Música", () => $"{s.MusicVolume}%", d => Change(() => s.MusicVolume = Step(s.MusicVolume, d, VolumeStep)));
        yield return new StepRow("Efeitos sonoros", () => $"{s.SfxVolume}%", d => Change(() => s.SfxVolume = Step(s.SfxVolume, d, VolumeStep)));
        yield return new StepRow("Tela cheia", () => YesNo(s.Fullscreen), _ => Change(() => s.Fullscreen = !s.Fullscreen));
        yield return new StepRow("Tamanho da janela", () => WindowText(s.WindowSize),
            d => Change(() => s.WindowSize = Math.Clamp(s.WindowSize + d, 0, GameSettings.WindowHeights.Length - 1)));
        yield return new StepRow("Tremor de tela", () => $"{s.ScreenShake}%", d => Change(() => s.ScreenShake = Step(s.ScreenShake, d, ShakeStep)));
        yield return new StepRow("Reduzir flashes", () => YesNo(s.ReduceFlashes), _ => Change(() => s.ReduceFlashes = !s.ReduceFlashes));
        yield return new StepRow("Tamanho do texto", () => s.TextSize == GameSettings.TextLarge ? "Grande" : "Normal",
            _ => Change(() => s.TextSize = s.TextSize == GameSettings.TextLarge ? GameSettings.TextNormal : GameSettings.TextLarge));
    }

    private void Change(Action mutate)
    {
        mutate();
        GameSettings.Commit(GetTree());
    }

    private static int Step(int value, int direction, int step) => Math.Clamp(value + (direction * step), 0, 100);

    private static string WindowText(int index)
    {
        var height = GameSettings.WindowHeights[index];
        return $"{height * 16 / 9}x{height}";
    }

    private static string YesNo(bool value) => value ? "Sim" : "Não";
}
