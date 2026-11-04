using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Lugares novos do Ato I (issue #40): o jogo novo começa na estrada (o smoke sobe a escadaria para a Praça), a sopa da
/// Nilza é servida dentro da taverna, a cama do quarto passa a noite e cair na Brecha acorda no quarto com a fala de morte.
/// </summary>
public partial class SmokeTest
{
    /// <summary>Quadros de espera para a queda (animação de morte, troca de sala, tela de resultado).</summary>
    private const int FallFrames = 1500;

    private async Task<Level> LeaveRoad(Level level)
    {
        if (level.World.Data.Id != Level.RoadLevelId)
        {
            return level;
        }

        // Os bots de combate (--ato1, --combat-bot) caem de propósito e esperam voltar à Praça, não ao quarto.
        Level.WakeInRoom = false;

        Check(level.World.Zones.Any(z => z.Target == Level.HubLevelId), "a estrada deveria ter a subida para a Praça");
        await Shot("08a_estrada", 0.4);
        return await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(0, 30 * level.World.Data.ActorScale), "Level_praca");
    }

    /// <summary>Taverna da Nilza por dentro (ROTEIRO-V2 §3, 7:00): o Ravi paga, a Nilza vem do caldeirão e serve a sopa.</summary>
    private async Task<Level> TavernSoup(Level level)
    {
        Check(StoryService.Engine.ObjectiveId == "nilza", $"antes da sopa o objetivo deveria ser a Nilza, é '{StoryService.Engine.ObjectiveId}'");
        level = await Travel(level, z => z.Target == "taverna", new Vector2(16, 0), "Level_taverna");
        Check(level.World.Npcs.Any(n => n.NpcId == "nilza"), "a Tia Nilza deveria estar na taverna, na frente do caldeirão");
        Check(level.World.Npcs.Any(n => n.NpcId == "ravi"), "o Ravi deveria estar na taverna (ele paga a sopa)");
        Check(level.PlayAutoplay(), "na taverna a Nilza deveria vir servir a sopa");
        await WaitForDialogue(level, "sopa da Nilza na taverna");
        _scenes.Add("nilza");
        await Shot("taverna_sopa", 0.4);
        await SkipDialogue(level);
        Check(Flag(StoryFlags.MetNilza), "a sopa na taverna deveria ligar met_nilza");
        Check(HuntService.State.SoupReady, "a sopa da Nilza deveria valer para a próxima caçada");
        Check(StoryService.Engine.ObjectiveId == "forja", $"depois da sopa o objetivo deveria ser a forja, é '{StoryService.Engine.ObjectiveId}'");
        return await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(0, -30 * level.World.Data.ActorScale), "Level_praca");
    }

    /// <summary>
    /// Fecho do Ato I (§3, 9:00 e §4.6 cena 3): sobe ao quarto, dorme na cama e desce para a manhã das maçãs roxas, com o
    /// carroceiro, a carroça, os lampiões novos e a banca de verdade da Lis na Praça.
    /// </summary>
    private async Task<Level> SleepAndMorning(Level level)
    {
        level = await Travel(level, z => z.Target == "forjas", new Vector2(0, -30 * level.World.Data.ActorScale), "Level_forjas");
        level = await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(-24, 0), "Level_praca");
        level = await Travel(level, z => z.Target == "taverna", new Vector2(16, 0), "Level_taverna");
        Check(level.World.Npcs.All(n => n.NpcId != "ravi"), "depois da Odete o Ravi não deveria mais estar na taverna");
        level = await Travel(level, z => z.Target == Level.RoomLevelId, new Vector2(-20, 20), "Level_quarto");

        var bed = level.World.Zones.FirstOrDefault(z => z.Kind == "bed");
        Check(bed is not null, "o quarto deveria ter a cama");
        if (bed is not null)
        {
            await StandAt(level, bed.GlobalPosition);
            Check(level.Player.Focus == bed, $"perto da cama o foco deveria ser a cama, foi '{level.Player.Focus?.Kind}'");
            await PressInWorld(Key.E);
            await WaitFor(() => Level.SleptInRoom && !level.SceneBusy, "dormir no quarto");
            await Shot("quarto_manha", 0.3);
        }

        level = await Travel(level, z => z.Target == "taverna", new Vector2(-20, 20), "Level_taverna");
        level = await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(0, -30 * level.World.Data.ActorScale), "Level_praca");
        CheckMorningPlaza(level);
        if (level.World.Props.FirstOrDefault(p => p.Type == "carroca_macas_roxas") is { } cart)
        {
            await StandAt(level, level.World.ClampToFloor(cart.GlobalPosition + new Vector2(0, level.World.Data.HumanHeight)));
            await Shot("praca_carroca_macas", 0.3);
        }

        Check(level.PlayAutoplay(), "na manhã depois de dormir a Lis deveria vir com a maçã roxa");
        await WaitForDialogue(level, "maçã roxa da Lis");
        Check(!Level.SleptInRoom, "a manhã da Praça deveria usar a noite dormida no quarto");
        await Shot("praca_manha_macas", 0.4);
        await SkipDialogue(level);
        Check(Flag("maca_done"), "a cena da maçã roxa deveria ligar maca_done");
        return level;
    }

    private void CheckMorningPlaza(Level level)
    {
        var props = level.World.Props.Select(p => p.Type).ToList();
        Check(level.World.Npcs.Any(n => n.NpcId == "carroceiro"), "na manhã das maçãs o carroceiro deveria estar na Praça");
        Check(props.Contains("carroca_macas_roxas"), "na manhã das maçãs a carroça deveria estar na Praça");
        Check(props.Count(p => p == "lampiao_praca") >= 4, "depois do selo D a Praça deveria ter as fileiras de lampiões novos");
        Check(props.Contains("banca_lis_verdade") && !props.Contains("banca_lis_mapas"), "depois do selo D a Lis deveria ter a banca de verdade");
    }

    /// <summary>
    /// Cair na Brecha (ROTEIRO-V2 §6.1): acorda no quarto em cima da taverna, quem disse a fala de morte sobe a escada e
    /// fala ali; depois desce pela taverna até a Praça.
    /// </summary>
    private async Task<Level> FallAndWakeInRoom(Level level)
    {
        Level.WakeInRoom = true;
        try
        {
            var bot = new CombatBot(level, this);
            await bot.EnterHunt();
            var combat = level.Player.Combat;
            for (var i = 0; i < FallFrames && !combat.IsDead; i++)
            {
                combat.ReceiveHit(new HitInfo(combat.MaxHp * 10.0, level.Player.GlobalPosition + new Vector2(8, 0), Vector2.Zero, 0, 1, false, 0));
                await PhysicsFrames(2);
            }

            Check(combat.IsDead, "o golpe enorme deveria derrubar o caçador");
            for (var i = 0; i < FallFrames && (level.Switching || level.World.Data.Id != Level.RoomLevelId || RunResultScreen.Active is null); i++)
            {
                await Frames(1);
            }

            Check(level.World.Data.Id == Level.RoomLevelId, $"depois da queda o caçador deveria acordar no quarto, está em '{level.World.Data.Id}'");
            await Shot("ato1_queda_resultado", 0.3);
            RunResultScreen.Active?.Close();
            await WaitForDialogue(level, "fala de morte no quarto");
            Log.Info($"ato I: acordou no quarto; fala de morte de '{level.Dialogue.SpeakerName}': {level.Dialogue.FullText}");
            Check(level.World.Entities.GetNodeOrNull("DeathVisitor") is not null || level.Dialogue.SpeakerName.Length > 0,
                "a fala de morte deveria vir de alguém no quarto");
            await Shot("ato1_quarto_fala_morte", 0.4);
            await SkipDialogue(level);
        }
        finally
        {
            Level.WakeInRoom = false;
        }

        Check(!RiftSession.InRift, "acordado no quarto, a caçada deveria ter acabado");
        level = await Travel(level, z => z.Target == "taverna", new Vector2(-20, 20), "Level_taverna");
        return await Travel(level, z => z.Target == Level.HubLevelId, new Vector2(0, -30 * level.World.Data.ActorScale), "Level_praca");
    }
}
