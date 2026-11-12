using Vespera.Core.Coop;
using Vespera.Core.Net;
using Vespera.Game.Boot;
using Vespera.Game.Coop;

namespace Vespera.Game.Net;

/// <summary>
/// No host, os comandos de um convidado online (<see cref="CommandQueue"/>): a rede empurra, o caçador dele consome um por
/// passo de física pela <see cref="CommandInputSource"/>, igual ao co-op local.
/// </summary>
public sealed class NetworkCommandProvider(byte slot) : ICommandProvider
{
    private readonly CommandQueue _queue = new(slot);

    public int DeviceId => LocalInputSource.KeyboardMouse;

    /// <summary>Comandos recebidos (para os bots).</summary>
    public int Received { get; private set; }

    public void Push(PlayerCommand command)
    {
        if (_queue.Push(command))
        {
            Received++;
        }
    }

    public PlayerCommand Next(uint tick) => _queue.Next(tick);
}
