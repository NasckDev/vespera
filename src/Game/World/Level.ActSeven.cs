using System.Threading.Tasks;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// Ato VII (ROTEIRO-V2 §13, "A Cidade de Baixo"): a última sala do Avesso profundo, onde o Avesso fala e oferece ficar
/// (<c>ato7/avesso</c>). É o exame SSS: <c>CloseBreach</c> (Voltar) fecha a Brecha e aprova o selo SSS;
/// <c>SoftGameOver:sopa</c> (Ficar) é o game over suave: tela escura, a caçada acaba sem fechar e a Nilza acorda o
/// caçador (<c>ato7/nilza_sonho</c>). O Severo entrega o badalo (<c>GiveItem:badalo</c>).
/// </summary>
public partial class Level
{
    /// <summary>Sala final da Brecha do Avesso profundo (epílogo da caçada com a dupla de Guardiões).</summary>
    public const string AvessoRoom = "avesso_final";
    public const string AvessoDialogue = "ato7/avesso";
    public const string DreamDialogue = "ato7/nilza_sonho";
    public const string EventCloseBreach = "CloseBreach";
    public const string EventSoftGameOver = "SoftGameOver:sopa";
    public const string EventGiveBadalo = "GiveItem:badalo";
    private const float AvessoSpeaksAfter = 1.4f;

    private void ShowActSeven()
    {
        if (!RiftSession.InRift || World.Data.Id != AvessoRoom)
        {
            return;
        }

        // O Avesso fala sozinho quando o caçador entra: não há saída até ele responder.
        GetTree().CreateTimer(AvessoSpeaksAfter).Timeout += () =>
        {
            if (IsInsideTree() && RiftSession.InRift && World.Data.Id == AvessoRoom)
            {
                StartDialogue(AvessoDialogue);
            }
        };
    }

    private bool HandleActSevenEvent(string name)
    {
        if (name == EventCloseBreach)
        {
            CloseAvesso();
            return true;
        }

        if (name == EventSoftGameOver)
        {
            _ = DreamOfSoup();
            return true;
        }

        if (name == EventGiveBadalo)
        {
            CombatHud.Toast("Badalo de bronze guardado. Pesa mais do que parece.");
            return true;
        }

        return false;
    }

    /// <summary>Voltar: o exame SSS é recusar a oferta. Aprova o selo e fecha a caçada como Brecha fechada.</summary>
    private void CloseAvesso()
    {
        if (!RiftSession.InRift)
        {
            return;
        }

        if (Hunter.Progression.IsActive)
        {
            Hunter.Progression.TryPassExam(Rank.SSS);
        }

        OnRoomExit();
    }

    /// <summary>Ficar: tela escura, a caçada acaba sem fechar a Brecha (nada fica na Lanterna) e a Nilza acorda o caçador.</summary>
    private async Task DreamOfSoup()
    {
        if (!RiftSession.InRift)
        {
            return;
        }

        var (_, summary) = RiftSession.EndRunWithSummary(survived: true, completed: false);
        _carriedHp = null;
        GameSession.Autosave();
        await ReturnToHub("Tudo escureceu. Uma colher bate numa tigela.", "Foi um sonho", summary, false);
        if (IsInsideTree())
        {
            StartDialogue(DreamDialogue);
        }
    }
}
