using System.Threading.Tasks;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>Capturas de revisão visual das camadas 3 e 4 (com janela e <c>--qa-combate --shots=pasta</c>).</summary>
public sealed class CombatQaCapture(Level level, SmokeTest host)
{
    public Task Run() => Task.CompletedTask;
}
