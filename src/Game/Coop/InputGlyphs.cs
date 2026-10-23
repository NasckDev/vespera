using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.Coop;

/// <summary>Nome do botão de uma ação para a fonte de input de um jogador (o teclado do J2 mostra a tecla do J2).</summary>
public static class InputGlyphs
{
    public static string Describe(IInputSource source, string action) => source switch
    {
        CommandInputSource { Provider: LocalCommandProvider local } => Describe(local.Device, action),
        MergedInput merged => Describe(merged.LastUsed, action),
        LocalInputSource localSource => localSource.Describe(action),
        _ => GlyphNames.Describe(
            InputSetup.Table.Get(action, source.DeviceId == LocalInputSource.KeyboardMouse ? BindingSlot.Primary : BindingSlot.Gamepad),
            GlyphNames.DetectFamily()),
    };
}
