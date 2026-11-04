using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Roteiro dos Atos VI e VII (<c>--atos67</c>). Ato VI: a Nilza serve a Sopa da Véspera nas Ruas Baixas; na Brecha das
/// Ruas Invertidas a primeira queda levanta o caçador e a segunda o devolve ao bairro; a mãe do Ravi; uma caçada real
/// (bot) pelas salas com Varal, Despejado e Rasgo; o Inquilino no fim; a arma de Duas Vésperas da Odete; Benício, Ravi e
/// a Lis da manhã. Ato VII: a praça escura e a Brecha do Avesso profundo; caçada (bot) por bairros misturados; Celeste;
/// a dupla de Guardiões; a sala do Avesso ("Ficar" = sonho da sopa; "Voltar" = selo SSS); Benício, Odete, Ravi, Severo
/// com o badalo e a Lis à noite, que abre o Final.
/// </summary>
public partial class SmokeTest
{
    public const string Atos67Flag = "--atos67";

    private static readonly string[] DoneBeforeAto6 =
    [
        "GuardianDefeated:farol_afogado", "act3_done", "met_severo", "pescou", "lanterna_m", "odete_lanterna_m",
        "GuardianDefeated:velora", "act4_done", "met_vigia", "ravi_rota_a", "ruas_fechada_a_tempo",
        "GuardianDefeated:sineiro_mudo", "act5_done", "ato5_porta", "ato5_confissao", "ato5_depois", "ato5_ravi", "ato5_lis",
        "ato3_fecho_benicio", "ato3_manha_nilza", "ato3_manha_lis", "severo_viu_lanterna", "exame_b_visto", "ato4_fecho_benicio",
        "ato4_odete", "ravi_cena_ato4", "ato5_odete",
    ];

    private async Task RunAtos67(Level start)
    {
        var engine = StoryService.Engine;
        foreach (var flag in DoneBeforeAto4.Concat(DoneBeforeAto6))
        {
            engine.SetFlag(flag);
        }

        engine.Dawn();
        var level = await ActSixStreets(start);
        level = await ActSixHunts(level);
        level = await ActSixClose(level);
        level = await ActSevenOpen(level);
        level = await ActSevenAvesso(level);
        await ActSevenClose(level);
        Log.Info("atos VI e VII OK: sopa (segunda vida), Ruas Invertidas, Inquilino, Duas Vésperas, praça escura, Avesso, dupla, exame SSS e badalo");
    }

    /// <summary>Nilza e a sopa, a segunda vida dentro da Brecha e a mãe do Ravi.</summary>
    private async Task<Level> ActSixStreets(Level level)
    {
        var engine = StoryService.Engine;
        await level.GoToRoom("ruas_baixas", withDirector: false);
        await PhysicsFrames(3);
        Check(engine.AutoplayFor("ruas_baixas")?.Dialogue == "ato6/nilza", "nas Ruas Baixas a Nilza deveria vir com a sopa");
        Check(level.PlayAutoplay(), "a fala da sopa deveria tocar");
        await WaitForDialogue(level, "Nilza com a sopa");
        await Shot("atos67_01_nilza_sopa", 0.5);
        await SkipDialogue(level);
        Check(engine.HasFlag(RunPerks.SecondLifeFlag), "a Nilza deveria liberar a Sopa da Véspera (UnlockSopaDaVespera)");

        // Segunda vida: a primeira queda levanta ali mesmo; a segunda acaba a caçada.
        var bot = new CombatBot(level, this);
        await bot.EnterHunt();
        Check(RiftSession.Brecha.Id == "ruas_invertidas", $"a Brecha das Ruas Baixas deveria ser as Ruas Invertidas, é '{RiftSession.Brecha.Id}'");
        Check(HuntService.SecondLifeReady, "com a sopa, a caçada deveria começar com a segunda vida");
        await Fall(level);
        var combat = level.Player.Combat;
        // A queda só é tratada depois da animação de morte (HunterCombat.DeathDelay, timer de jogo): espera por tempo,
        // não por quadros (com a máquina carregada ou quadros rápidos, uma contagem fixa de quadros acaba antes).
        await WaitWhile(() => combat.IsDead && RiftSession.InRift, RiseWaitSeconds);

        Check(RiftSession.InRift && !combat.IsDead, "a primeira queda deveria levantar o caçador dentro da Brecha");
        Check(combat.Hp == RunPerks.SecondLifeHp(combat.MaxHp), $"a sopa deveria levantar com metade da vida ({combat.Hp}/{combat.MaxHp})");
        Check(!HuntService.SecondLifeReady, "a segunda vida só vale uma vez por caçada");
        await Shot("atos67_02_levantou_com_a_sopa", 0.3);
        await Fall(level);
        await BackFromFall(level, "ruas_baixas");
        Check(engine.HasFlag("ato6_morte_1"), "a queda nas Ruas Invertidas deveria contar para as falas de morte do Ato VI");
        await PlayPendingScenes(level);

        await OpenTalk(level, "mae_ravi");
        await Shot("atos67_03_mae_ravi", 0.5);
        await SkipDialogue(level);
        Check(engine.ObjectiveId == "ato6_brecha", $"depois da mãe do Ravi o objetivo deveria ser ato6_brecha, é '{engine.ObjectiveId}'");
        return level;
    }

    private const double RiseWaitSeconds = 20;
    private const double BackHomeWaitSeconds = 40;

    /// <summary>Espera enquanto <paramref name="busy"/> vale, até <paramref name="seconds"/> segundos de relógio (a câmera lenta da queda estica o tempo de jogo).</summary>
    private async Task WaitWhile(System.Func<bool> busy, double seconds)
    {
        var start = Time.GetTicksMsec();
        while (busy() && (Time.GetTicksMsec() - start) / 1000.0 < seconds)
        {
            await PhysicsFrames(1);
        }
    }

    /// <summary>Espera a volta ao bairro depois da queda (passa pela tela de resultado).</summary>
    private async Task BackFromFall(Level level, string hub)
    {
        await WaitWhile(() =>
        {
            RunResultScreen.Active?.Close();
            return RiftSession.InRift || level.Switching || level.World.Data.Id != hub || RunResultScreen.Active is not null;
        }, BackHomeWaitSeconds);

        Check(!RiftSession.InRift && level.World.Data.Id == hub, $"depois da queda o caçador deveria voltar a {hub}, está em '{level.World.Data.Id}' (na Brecha: {RiftSession.InRift})");
        await PhysicsFrames(4);
    }

    private async Task Fall(Level level)
    {
        var combat = level.Player.Combat;
        for (var i = 0; i < 60 && !combat.IsDead; i++)
        {
            combat.ReceiveHit(new HitInfo(combat.MaxHp * 10.0, level.Player.GlobalPosition + new Vector2(8, 0), Vector2.Zero, 0, 1, false, 0));
            await PhysicsFrames(2);
        }

        Check(combat.IsDead, "o golpe enorme deveria derrubar o caçador");
    }

    /// <summary>Uma caçada pelas salas das Ruas Invertidas e outra com o Inquilino no fim (bot invulnerável).</summary>
    private async Task<Level> ActSixHunts(Level level)
    {
        var engine = StoryService.Engine;
        var bot = new CombatBot(level, this);
        var metrics = await bot.PlayHunt(invulnerable: true);
        _failures.AddRange(bot.Failures);
        Check(metrics.Rooms >= 5 && !metrics.Guardian, $"a primeira caçada das Ruas deveria ter 5+ salas sem Guardião ({metrics.Rooms}, guardião {metrics.Guardian})");
        Check(engine.HasFlag("ruas_invertidas:hunt_1"), "a caçada completa deveria ligar ruas_invertidas:hunt_1");
        Check(engine.ObjectiveId == "ato6_inquilino", $"depois da primeira caçada o objetivo deveria ser ato6_inquilino, é '{engine.ObjectiveId}'");

        level = (Level)_router.Current!;
        bot = new CombatBot(level, this);
        metrics = await bot.PlayHunt(invulnerable: true);
        _failures.AddRange(bot.Failures);
        Check(metrics.Guardian, "a segunda caçada das Ruas deveria terminar no Inquilino");
        Check(engine.HasFlag("GuardianDefeated:inquilino") && engine.HasFlag("act6_done"), "vencer o Inquilino deveria fechar o Ato VI");
        return (Level)_router.Current!;
    }

    /// <summary>Odete dá a arma de Duas Vésperas; Benício (SS), Ravi e a Lis da manhã na praça.</summary>
    private async Task<Level> ActSixClose(Level level)
    {
        var engine = StoryService.Engine;
        await level.GoToRoom("forja_interior", withDirector: false);
        await PhysicsFrames(3);
        Check(engine.AutoplayFor("forja_interior")?.Dialogue == "ato6/odete", "na forja a Odete deveria dar a arma lendária");
        Check(level.PlayAutoplay(), "a cena da arma lendária deveria tocar");
        await WaitForDialogue(level, "Odete e a arma de Duas Vésperas");
        await Shot("atos67_04_odete_duas_vesperas", 0.5);
        await SkipDialogue(level);
        var weapon = Progression.Engine.WeaponStats;
        Check(weapon.TierRank == Rank.SS && weapon.Name.Contains("Duas Vésperas", System.StringComparison.Ordinal),
            $"a Odete deveria dar a arma de Duas Vésperas (está com '{weapon.Name}', {weapon.TierRank})");

        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        foreach (var (dialogue, flag) in new[] { ("ato6/benicio", "ato6_fecho_benicio"), ("ato6/ravi", "ato6_ravi"), ("ato6/lis", "ato6_lis") })
        {
            Check(engine.AutoplayFor("praca")?.Dialogue == dialogue, $"na praça deveria tocar {dialogue}, vem '{engine.AutoplayFor("praca")?.Dialogue}'");
            Check(level.PlayAutoplay(), $"{dialogue} deveria tocar");
            await WaitForDialogue(level, dialogue);
            await SkipDialogue(level);
            Check(engine.HasFlag(flag), $"{dialogue} deveria ligar {flag}");
        }

        Check(engine.ObjectiveId == "ato7", $"depois da Lis da manhã o objetivo deveria ser ato7, é '{engine.ObjectiveId}'");
        return level;
    }

    /// <summary>Praça escura, Lis e Benício ("quantas você perdeu?"), Brecha do Avesso, caçada por bairros misturados e Celeste.</summary>
    private async Task<Level> ActSevenOpen(Level level)
    {
        var engine = StoryService.Engine;
        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        var ground = level.World.GetNodeOrNull<Sprite2D>("Ground");
        Check(ground?.Texture?.ResourcePath.Contains("chao_praca_escura", System.StringComparison.Ordinal) == true,
            $"no Ato VII a praça deveria estar escura (chão '{ground?.Texture?.ResourcePath}')");
        Check(level.World.Zones.Count(z => z.Kind == "brecha") == 1 && level.World.Zones.Single(z => z.Kind == "brecha").Target == "avesso_profundo",
            "no Ato VII a única fenda da praça deveria descer ao Avesso profundo");
        await Shot("atos67_05_praca_escura", 0.5);
        foreach (var dialogue in new[] { "ato7/lis", "ato7/benicio" })
        {
            Check(level.PlayAutoplay(), $"{dialogue} deveria tocar na praça escura");
            await WaitForDialogue(level, dialogue);
            if (dialogue == "ato7/benicio")
            {
                level.Dialogue.Complete();
                await Frames(2);
                for (var i = 0; i < 6 && level.DialogueOpen && level.Dialogue.CurrentNodeId != "a2"; i++)
                {
                    await PressInWorld(Key.E);
                }

                Check(System.Text.RegularExpressions.Regex.IsMatch(level.Dialogue.FullText, @"\d"), $"o Benício deveria dizer o número de Lanternas perdidas: '{level.Dialogue.FullText}'");
                await Shot("atos67_06_benicio_lanternas", 0.3);
            }

            await SkipDialogue(level);
        }

        Check(engine.ObjectiveId == "ato7_brecha", $"depois do Benício o objetivo deveria ser ato7_brecha, é '{engine.ObjectiveId}'");
        var bot = new CombatBot(level, this);
        var metrics = await bot.PlayHunt(invulnerable: true);
        _failures.AddRange(bot.Failures);
        Check(!metrics.Guardian && engine.HasFlag("avesso_profundo:hunt_1"), "a primeira caçada do Avesso deveria fechar sem a dupla");
        Check(engine.ObjectiveId == "ato7_vigia", $"depois da caçada o objetivo deveria ser ato7_vigia, é '{engine.ObjectiveId}'");

        // A pergunta cinco do Vigia é só uma fala (a noite no Cemitério já tem o smoke --ato45); aqui só a resposta.
        engine.SetFlag("vigia_p5_feita");
        engine.SetFlag("vigia_p5_volto");
        level = (Level)_router.Current!;
        await level.GoToRoom("pomar_alto", withDirector: false);
        await PhysicsFrames(3);
        Check(engine.AutoplayFor("pomar_alto")?.Dialogue == "ato7/celeste", "no Pomar a Celeste deveria falar da Lis");
        Check(level.PlayAutoplay(), "a fala da Celeste deveria tocar");
        await WaitForDialogue(level, "Celeste e a Lis");
        await SkipDialogue(level);
        Check(engine.ObjectiveId == "ato7_dupla", $"depois da Celeste o objetivo deveria ser ato7_dupla, é '{engine.ObjectiveId}'");
        return level;
    }

    /// <summary>Dupla de Guardiões e sala do Avesso: primeiro "Ficar" (sonho da sopa), depois "Voltar" (selo SSS).</summary>
    private async Task<Level> ActSevenAvesso(Level level)
    {
        var engine = StoryService.Engine;
        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        foreach (var answer in new[] { "Ficar", "Voltar" })
        {
            level = (Level)_router.Current!;
            await EnterAvessoDuo(level);
            var director = level.World.Director!;
            var pair = $"{director.Guardian?.MonsterId}+{director.Partner?.MonsterId}";
            Check(pair is "lodo_mae+macieira_oca" or "farol_afogado+velora" or "sineiro_mudo+inquilino", $"a sala deveria ter uma das três duplas, tem '{pair}'");
            Log.Info($"atos67: dupla {pair} em {level.World.Data.Id}");
            Check(director.Guardian?.Def.Telegraph == 0.4 && director.Partner?.Def.Telegraph == 0.4, "na dupla os avisos ficam em 0,4 s");
            await Shot($"atos67_07_dupla_{answer}", 0.6);
            director.Guardian?.Die();
            await PhysicsFrames(10);
            Check(!director.Cleared, "com um Guardião de pé a sala da dupla não acaba");
            director.Partner?.Die();
            await WaitFor(() => director.ExitPortal is not null, "fenda depois da dupla");
            director.RequestExit();
            await WaitFor(() => !level.Switching && level.World.Data.Id == Level.AvessoRoom && level.DialogueOpen, "o Avesso falando na última sala");
            await Shot($"atos67_08_avesso_{answer}", 0.5);
            await Choose(level, answer);
            await SkipDialogue(level);
            if (answer == "Ficar")
            {
                await BackFromFall(level, "praca");
                await WaitForDialogue(level, "Nilza acordando o caçador");
                Check(level.Dialogue.SpeakerName.Length > 0, "a Nilza deveria acordar o caçador");
                await SkipDialogue(level);
                Check(engine.HasFlag("avesso_ficou") && !engine.HasFlag("avesso_recusado"), "ficar não fecha a Brecha");
                Check(engine.ObjectiveId == "ato7_dupla", $"depois do sonho o objetivo continua ato7_dupla, é '{engine.ObjectiveId}'");
            }
        }

        await WaitFor(() => !RiftSession.InRift && !level.Switching && level.World.Data.Id == "praca", "volta à praça com o Avesso fechado");
        RunResultScreen.Active?.Close();
        await PhysicsFrames(4);
        Check(engine.HasFlag("avesso_recusado"), "voltar deveria recusar a oferta do Avesso");
        Check(Progression.State.Xp >= Progression.Catalog.Step(Rank.SSS).Xp, $"recusar a oferta é o exame SSS: a XP deveria chegar ao selo SSS ({Progression.State.Xp})");
        Check(engine.ObjectiveId == "ato7_fecho", $"depois do Avesso o objetivo deveria ser ato7_fecho, é '{engine.ObjectiveId}'");
        return (Level)_router.Current!;
    }

    private async Task EnterAvessoDuo(Level level)
    {
        RiftSession.Select("avesso_profundo");
        level.EnterRift(RiftSession.HunterFor(Progression.Engine.ClassId));
        await WaitFor(() => !level.Switching && level.World.Director is not null, "primeira sala do Avesso");
        Check(HuntService.Plan?.HasGuardian == true, "com a Celeste ouvida, a caçada do Avesso deveria terminar na dupla");
        var plan = HuntService.Plan!;
        Check(plan.Rooms[^1].Ground == Level.AvessoRoom, $"a última sala deveria ser a do Avesso, é '{plan.Rooms[^1].Ground}'");
        while (RiftSession.RoomIndex < plan.Rooms.Count - 2)
        {
            RiftSession.AdvanceRoom();
        }

        await level.GoToRoom(RiftSession.CurrentRoom);
        await WaitFor(() => !level.Switching && level.World.Director?.Guardian is not null, "sala da dupla");
    }

    /// <summary>Benício (SSS), Odete, Ravi, Severo com o badalo e a Lis à noite, que liga act7_done e abre o Final.</summary>
    private async Task ActSevenClose(Level level)
    {
        var engine = StoryService.Engine;
        Check(level.PlayAutoplay(), "o Benício deveria falar do selo SSS");
        await WaitForDialogue(level, "Benício e o selo SSS");
        await Choose(level, "Quer estar");
        await SkipDialogue(level);
        Check(engine.HasFlag("ato7_fecho_benicio") && engine.HasFlag("benicio_na_decisao"), "o fecho do Benício deveria tocar e levar a escolha dele");

        await level.GoToRoom("forja_interior", withDirector: false);
        await PhysicsFrames(3);
        Check(level.PlayAutoplay(), "a Odete deveria dizer que vai junto");
        await WaitForDialogue(level, "Odete vai junto");
        await SkipDialogue(level);

        await level.GoToRoom("praca", withDirector: false);
        await PhysicsFrames(3);
        Check(level.World.Npcs.Any(n => n.NpcId == "severo") || engine.AutoplayFor("praca")?.Dialogue == "ato7/ravi", "a praça deveria preparar o Ravi e o Severo");
        foreach (var dialogue in new[] { "ato7/ravi", "ato7/severo" })
        {
            Check(engine.AutoplayFor("praca")?.Dialogue == dialogue, $"na praça deveria tocar {dialogue}, vem '{engine.AutoplayFor("praca")?.Dialogue}'");
            if (dialogue == "ato7/severo")
            {
                await level.GoToRoom("praca", withDirector: false);
                await PhysicsFrames(3);
                Check(level.World.Npcs.Any(n => n.NpcId == "severo"), "o Severo deveria estar na praça com o badalo");
            }

            Check(level.PlayAutoplay(), $"{dialogue} deveria tocar");
            await WaitForDialogue(level, dialogue);
            await SkipDialogue(level);
        }

        Check(engine.HasFlag("tem_badalo"), "o Severo deveria entregar o badalo");
        Check(level.PlayAutoplay(), "a Lis deveria falar à noite na escada");
        await WaitForDialogue(level, "Lis à noite");
        await Shot("atos67_09_lis_noite", 0.5);
        await Choose(level, "Fica perto");
        await SkipDialogue(level);
        Check(engine.HasFlag("act7_done") && engine.HasFlag("lis_apresentada"), "a Lis à noite fecha o Ato VII (act7_done)");
        Check(engine.ObjectiveId == "final", $"depois do Ato VII o objetivo deveria ser 'final', é '{engine.ObjectiveId}'");
    }
}
