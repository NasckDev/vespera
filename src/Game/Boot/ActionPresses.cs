using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Boot;

/// <summary>
/// Apertos de botão guardados por evento, para o jogo ler "acabou de apertar" sem perder toques (ponto único de leitura).
/// Antes a borda vinha de amostrar o estado do botão a cada tique de física: um toque que começa e termina entre dois
/// tiques (toque rápido, touchpad, quadro lento, render acima de 60 Hz) sumia; e o clique que a interface já tinha usado
/// (fechar a fala) ainda aparecia como golpe. Aqui só entra o que ninguém tratou (<c>_UnhandledInput</c>) e cada aperto
/// vale até ser consumido ou até o tique de física seguinte passar.
/// </summary>
public static class ActionPresses
{
    /// <summary>Tiques de física que um aperto não consumido sobrevive (o tique em curso e o seguinte).</summary>
    private const ulong LifetimeTicks = 1;

    private static readonly Dictionary<(int Device, string Code), ulong> Pending = [];

    /// <summary>Guarda o aperto de tecla, botão do mouse ou do controle (eventos de eixo ficam com a amostragem).</summary>
    public static void Record(InputEvent @event)
    {
        var press = @event switch
        {
            InputEventJoypadButton { Pressed: true } pad => (pad.Device, InputSetup.FromEvent(pad, BindingSlot.Gamepad)),
            InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: true } =>
                (LocalInputSource.KeyboardMouse, InputSetup.FromEvent(@event, BindingSlot.Primary)),
            _ => (0, null),
        };
        if (press.Item2 is { } code)
        {
            Pending[(press.Item1, code)] = Engine.GetPhysicsFrames();
        }
    }

    /// <summary>Consome o aperto guardado de <paramref name="code"/> no dispositivo; falso se não houve ou já expirou.</summary>
    public static bool Consume(int device, string code) =>
        Pending.Remove((device, code), out var frame) && Engine.GetPhysicsFrames() - frame <= LifetimeTicks;
}
