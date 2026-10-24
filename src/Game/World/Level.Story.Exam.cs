using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.World;

/// <summary>
/// O exame da véspera 3 (ROTEIRO-V2 §4.5): não é tela separada. O Benício entra atrás do caçador e fica parado na
/// entrada de cada sala, e diz UMA linha seca por sala (data/dialogue/exame_benicio.json). Só observa: não fala
/// quando o jogador se aproxima e não atrapalha a luta.
/// </summary>
public partial class Level
{
    private const string ExamDialogue = "exame_benicio";
    private const float ExamBenicioSideHumans = 1.3f;
    private const int ExamCleanLines = 3;

    private int _examHpAtEntry = -1;
    private int _examCleanRooms;

    /// <summary>Chamado ao montar cada sala da Brecha (gancho em <c>OnWorldMounted</c>).</summary>
    private void ShowExamObserver()
    {
        var engine = StoryService.Engine;
        if (!RiftSession.InRift || World.Director is null || !engine.HasFlag(StoryFlags.GuardianGate) || engine.HasFlag("act1_done"))
        {
            _examHpAtEntry = -1;
            return;
        }

        var node = ExamLineFor(RiftSession.RoomIndex);
        _examHpAtEntry = Player.Combat.Hp;
        PlaceExamBenicio();
        var line = StoryService.LoadDialogue(ExamDialogue).Nodes.TryGetValue(node, out var found) ? found.Text : string.Empty;
        if (line.Length > 0)
        {
            CombatHud.Toast($"Benício: {line}");
        }

        Vespera.Game.Boot.Log.Info($"exame: sala {RiftSession.RoomIndex + 1}, Benício na entrada: '{node}'");
    }

    private string ExamLineFor(int room)
    {
        if (room == 0)
        {
            _examCleanRooms = 0;
            return "entrada";
        }

        if (RiftSession.IsLastRoom && Hunter.HuntService.Plan?.HasGuardian == true)
        {
            return "guardiao";
        }

        if (_examHpAtEntry >= 0 && Player.Combat.Hp < _examHpAtEntry)
        {
            return "dano";
        }

        _examCleanRooms = System.Math.Min(ExamCleanLines, _examCleanRooms + 1);
        return $"limpo{_examCleanRooms}";
    }

    /// <summary>O Benício parado ao lado da entrada (onde o caçador aparece), olhando a sala, sem conversa.</summary>
    private void PlaceExamBenicio()
    {
        var human = World.Data.HumanHeight;
        var at = World.ClampToFloor(Player.GlobalPosition + new Vector2(-human * ExamBenicioSideHumans, 0));
        var placement = new NpcPlacement { Id = "benicio", At = [at.X, at.Y], Routine = "idle", Dialogue = ExamDialogue };
        var npc = new Npc("benicio", placement, human) { Name = "ExamBenicio", Position = at };
        World.Entities.AddChild(npc);
        npc.Interaction.Enabled = false;
        npc.FaceTowards(Player.GlobalPosition + new Vector2(human * 3f, -human));
    }
}
