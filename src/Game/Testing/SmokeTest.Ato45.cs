using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Story;
using Vespera.Game.World;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro dos Atos IV e V (<c>--ato45</c>): ponte do Pomar Alto trancada até o Farol Afogado e aberta depois; Cemitério
/// de dia (sem Vigia, Benício do exame B, Brecha aberta, banco para esperar a noite); à noite o Vigia faz a
/// pergunta um (Sim/Não/Não entendi), guarda o Eco no primeiro slot e não pergunta de novo na mesma noite; noite seguinte,
/// pergunta dois; avisos da Lis, da Nilza e do Ravi na Praça (§8.1, sem romper enquanto a Brecha B não existe); a Brecha
/// do Cemitério com a Velória (bot de combate) e o selo A; Torre do Sino com o Benício na porta, a confissão, a Brecha do
/// Sino Mudo com o Guardião e o selo S; a descida das Forjas para as Ruas Baixas aberta.
/// </summary>
public partial class SmokeTest
{
    public const string Ato45Flag = "--ato45";

    /// <summary>Flags dos Atos I a III já cumpridos, para nenhuma cena antiga tocar no lugar das novas.</summary>
    private static readonly string[] DoneBeforeAto4 =
    [
        "arrival_done", "starter_blade", "registered", "met_benicio", "met_ravi", "ravi_placar", "met_nilza", "met_odete",
        "met_lis", "hunt_1", "hunt_2", "exame_e", "act1_done", "ato1_fim_done", "odete_marisa", "maca_done", "met_celeste",
        "celeste_lis_done", "act2_done", "celeste_fim_done", "ato2_fim_done", StoryFlags.GuardianDefeatedLodoMae,
        "GuardianDefeated:macieira_oca",
    ];

    private async Task RunAto45(Level start)
    {
        var engine = StoryService.Engine;
        foreach (var flag in DoneBeforeAto4)
        {
            engine.SetFlag(flag);
        }

        engine.Dawn();
        var level = start;

        // Ponte do Pomar Alto: trancada antes do Farol Afogado.
        await level.GoToRoom("pomar_alto", withDirector: false);
        await PhysicsFrames(3);
        var bridge = level.World.Zones.First(z => z.GlobalPosition.X > 1600);
        Check(bridge.Target.Length == 0 && bridge.Text.Contains("Cais Afogado", System.StringComparison.Ordinal),
            $"antes do Farol Afogado a ponte para o Cemitério deveria estar trancada, texto '{bridge.Text}'");

        engine.SetFlag("GuardianDefeated:farol_afogado");
        await level.GoToRoom("pomar_alto", withDirector: false);
        await PhysicsFrames(3);
        level = await Travel(level, z => z.Target == "cemiterio", new Vector2(-30, 0), "Level_cemiterio");

        // Cemitério de dia: sem Vigia; Benício do exame B.
        Check(level.World.Npcs.All(n => n.NpcId != "vigia"), "de dia o Vigia não deveria estar no Cemitério");
        Check(engine.AutoplayFor("cemiterio")?.Dialogue == "ato4/benicio", "o Benício deveria falar do exame B ao chegar");
        Check(level.PlayAutoplay(), "a fala do exame B deveria tocar");
        await WaitForDialogue(level, "Benício no Cemitério");
        await Shot("ato45_01_benicio_exame_b", 0.5);
        await SkipDialogue(level);
        Check(engine.HasFlag("exame_b_visto"), "a fala do exame B deveria ligar exame_b_visto");

        // Brecha aberta: leva para o Cemitério das Lanternas do lado de baixo (não está mais selada).
        Check(level.World.Zones.All(z => z.Kind != LevelWorld.SealedBrechaKind), "a Brecha do Cemitério não deveria estar selada");
        var rift = level.World.Zones.First(z => z.Kind == "brecha");
        Check(rift.Target == "cemiterio_das_lanternas", $"a Brecha do Cemitério deveria levar a cemiterio_das_lanternas, leva a '{rift.Target}'");

        // Banco: esperar a noite recarrega o Cemitério com o Vigia.
        var bench = level.World.Zones.First(z => z.Dialogue == "ato4/banco");
        await StandAt(level, bench.GlobalPosition + new Vector2(0, 20 * level.World.Data.ActorScale));
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, "E no banco deveria abrir a conversa");
        await Choose(level, "Esperar a noite");
        await SkipDialogue(level);
        await WaitFor(() => _router.Current is Level { Switching: false } l && l != level && l.World.Data.Id == "cemiterio" && !_router.IsBusy, "Cemitério à noite");
        level = (Level)_router.Current!;
        await Frames(4);
        Check(engine.HasFlag(NightFlags.Night), "esperar no banco deveria fazer cair a noite");
        Check(level.World.Npcs.Any(n => n.NpcId == "vigia"), "à noite o Vigia deveria estar no banco");
        await Shot("ato45_03_cemiterio_noite", 0.5);

        // Pergunta um + Eco.
        await OpenTalk(level, "vigia");
        await Shot("ato45_04_vigia_pergunta_um", 0.5);
        await Choose(level, "Não entendi");
        await SkipDialogue(level);
        Check(engine.HasFlag("vigia_p1_naoentendi"), "a resposta 'Não entendi' deveria virar vigia_p1_naoentendi");
        Check(engine.HasFlag("eco_slot_1"), "o Vigia deveria liberar o primeiro slot de Eco (eco_slot_1)");
        Check(engine.ObjectiveId == "ato4_brecha", $"depois do Vigia o objetivo deveria ser ato4_brecha, é '{engine.ObjectiveId}'");
        Check(engine.HasFlag(RaviRescue.Opened), "conhecer o Vigia deveria abrir a Brecha B das Ruas Baixas");

        await OpenTalk(level, "vigia");
        Check(level.Dialogue.CurrentNodeId == "h1", $"na mesma noite o Vigia não pergunta de novo (nó '{level.Dialogue.CurrentNodeId}')");
        await Choose(level, "Até a próxima noite");
        await SkipDialogue(level);

        // Avisos do Ravi na Praça: Lis, Nilza, Ravi — uma véspera cada.
        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        await PlayWarning(level, "lis", "ato45_05_aviso_lis");
        StoryService.NightFalls();
        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        await PlayWarning(level, "nilza", "ato45_06_aviso_nilza");
        StoryService.NightFalls();
        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        await PlayWarning(level, "ravi", "ato45_07_aviso_ravi");
        StoryService.NightFalls();
        Check(!engine.HasFlag(RaviRescue.Ruptured), "sem a Brecha B jogável as Ruas Baixas não deveriam romper");

        // Noite nova: pergunta dois.
        await level.GoToRoom("cemiterio", withDirector: false);
        await PhysicsFrames(3);
        await OpenTalk(level, "vigia");
        Check(level.Dialogue.CurrentNodeId == "s1", $"na noite seguinte o Vigia deveria fazer a pergunta dois (nó '{level.Dialogue.CurrentNodeId}')");
        await Choose(level, "Aceitava");
        await Choose(level, "Até a próxima noite");
        await SkipDialogue(level);
        Check(engine.HasFlag("vigia_p2_sim") && engine.HasFlag("vigia_p1_naoentendi"), "o Vigia deveria lembrar as duas respostas");

        // Brecha do Cemitério com a Velória no fim (uma caçada completa já feita): o bot de combate fecha a Brecha.
        Check(engine.ObjectiveId == "ato4_brecha", $"antes da caçada o objetivo deveria ser ato4_brecha, é '{engine.ObjectiveId}'");
        engine.SetFlag("cemiterio_das_lanternas:hunt_1");
        Check(engine.ObjectiveId == "ato4_guardiao", $"depois da primeira caçada o objetivo deveria ser ato4_guardiao, é '{engine.ObjectiveId}'");
        await level.GoToRoom("cemiterio", withDirector: false);
        await PhysicsFrames(3);
        await HuntGuardian(level, "velora");
        Check(engine.ObjectiveId == "ato4_fecho" && engine.HasFlag("act4_done"), $"depois da Velória o objetivo deveria ser ato4_fecho, é '{engine.ObjectiveId}'");
        await PlayStoryScene(level, "Benício: selo A");
        Check(engine.HasFlag("ato4_fecho_benicio"), "o Benício deveria fechar o Ato IV");
        Check(engine.ObjectiveId == "ato5", $"depois do fecho do Ato IV o objetivo deveria ser ato5, é '{engine.ObjectiveId}'");
        await Shot("ato45_09_fecho_ato4", 0.3);

        // Torre do Sino (depois da Velória): porta, confissão olhando o sino e a Brecha aberta.
        await level.GoToRoom("cemiterio", withDirector: false);
        await PhysicsFrames(3);
        level = await Travel(level, z => z.Target == "torre", new Vector2(-30, 0), "Level_torre");
        Check(level.PlayAutoplay(), "na Torre o Benício deveria falar na porta");
        await WaitForDialogue(level, "Benício na porta da Torre");
        await SkipDialogue(level);
        Check(engine.HasFlag("ato5_porta"), "a fala da porta deveria ligar ato5_porta");
        Check(level.PlayAutoplay(), "depois da porta vem a confissão");
        await WaitForDialogue(level, "confissão do Benício");
        await Shot("ato45_08_confissao_sino", 0.8);
        await SkipDialogue(level);
        Check(engine.HasFlag("ato5_confissao"), "a confissão deveria ligar ato5_confissao");
        Check(engine.ObjectiveId == "ato5_brecha", $"depois da confissão o objetivo deveria ser ato5_brecha, é '{engine.ObjectiveId}'");
        Check(level.World.Zones.All(z => z.Kind != LevelWorld.SealedBrechaKind), "a Brecha da Torre não deveria estar selada");
        Check(level.World.Zones.Any(z => z.Kind == "brecha" && z.Target == "sino_mudo"), "a Brecha da Torre deveria levar ao Sino Mudo");

        // Brecha do Sino Mudo com o Guardião no fim: o bot fecha; o Benício queima o selo S e manda para as Ruas Baixas.
        engine.SetFlag("sino_mudo:hunt_1");
        Check(engine.ObjectiveId == "ato5_guardiao", $"depois da primeira caçada o objetivo deveria ser ato5_guardiao, é '{engine.ObjectiveId}'");
        await HuntGuardian(level, "sineiro_mudo");
        Check(!Audio.Silence.Active, "depois do Sineiro Mudo o som deveria voltar");
        Check(engine.ObjectiveId == "ato5_fecho" && engine.HasFlag("act5_done"), $"depois do Sineiro Mudo o objetivo deveria ser ato5_fecho, é '{engine.ObjectiveId}'");
        await PlayStoryScene(level, "Benício: selo S");
        Check(engine.ObjectiveId == "ato6", $"depois do fecho do Ato V o objetivo deveria ser ato6, é '{engine.ObjectiveId}'");
        await Shot("ato45_10_fecho_ato5", 0.3);
        await level.GoToRoom("forjas", withDirector: false);
        await PhysicsFrames(3);
        Check(level.World.Zones.Any(z => z.Target == "ruas_baixas"), "depois do Sineiro Mudo a descida das Forjas deveria levar às Ruas Baixas");
        Log.Info("atos IV e V OK: ponte, Cemitério de dia e de noite, Vigia (2 perguntas, Eco), avisos do Ravi, Velória, selo A, Torre, confissão, Sineiro Mudo, selo S e Ruas Baixas abertas");
    }

    /// <summary>Entra na Brecha do bairro atual com o bot de combate (caçador invulnerável) e volta com o Guardião vencido.</summary>
    private async Task HuntGuardian(Level level, string guardian)
    {
        var failures = await new CombatBot(level, this) { ShotPrefix = $"ato45_{guardian}" }.Run(null, lanternTest: false);
        foreach (var failure in failures)
        {
            Check(false, $"caçada da {guardian}: {failure}");
        }

        Check(StoryService.Engine.HasFlag($"GuardianDefeated:{guardian}"), $"a caçada deveria terminar com {guardian} vencido");
        await PhysicsFrames(6);
    }

    /// <summary>A cena de autoplay do bairro (toca sozinha na volta; se ainda não tocou, chama) até o fim.</summary>
    private async Task PlayStoryScene(Level level, string what)
    {
        if (!level.DialogueOpen)
        {
            level.PlayAutoplay();
        }

        await WaitForDialogue(level, what);
        await SkipDialogue(level);
    }

    private async Task PlayWarning(Level level, string npc, string shot)
    {
        var auto = StoryService.Engine.AutoplayFor("praca");
        Check(auto is { Dialogue: "ato4/aviso_ruas" } && auto.Npc == npc, $"o aviso das Ruas Baixas deveria vir de {npc}, veio '{auto?.Dialogue}'/'{auto?.Npc}'");
        Check(level.PlayAutoplay(), $"o aviso de {npc} deveria tocar");
        await WaitForDialogue(level, $"aviso de {npc}");
        await Shot(shot, 0.5);
        await SkipDialogue(level);
    }
}
