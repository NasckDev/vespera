using GdUnit4;
using static GdUnit4.Assertions;
using Vespera.Core.Hunt;
using Vespera.Core.Rift;
using Vespera.Core.Story;
using Vespera.Game.World;

namespace Vespera.Tests.Game;

/// <summary>
/// Ato VII (ROTEIRO-V2 §13, "A Cidade de Baixo"): a Brecha do Avesso profundo mistura salas de todos os bairros, termina
/// numa dupla de Guardiões e depois na sala do Avesso (exame SSS: recusar a oferta). A praça escurece e a história segue
/// Lis → Benício → caçada → Vigia → Celeste → dupla → Avesso → Benício → Odete → Ravi → Severo (badalo) → Lis.
/// </summary>
[TestSuite]
public class AvessoHuntTest
{
    private static readonly string[] AvessoMonsters = ["ceifador_pomar", "sombra_cacador", "rasgo"];

    /// <summary>Guardiões que já existem no jogo (mesma lista de EnemyFactory.HasGuardian).</summary>
    private static readonly string[] ImplementedGuardians = ["lodo_mae", "macieira_oca", "farol_afogado", "inquilino"];

    private static string Dir(string name) => Path.Combine(AppContext.BaseDirectory, name);

    private static HuntConfig Config => HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), "cacada_avesso.json")));

    private static BrechaDef Brecha => BrechaDef.Parse(File.ReadAllText(Path.Combine(Dir("Data"), "brecha_avesso.json")));

    private static LevelData Level(string id) => LevelJson.ParseLevel(File.ReadAllText(Path.Combine(Dir("Levels"), $"{id}.json")));

    private static StoryEngine Engine() => new(new StoryProgress(), StoryScript.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), "story.json"))));

    private static DialogueFile Dialogue(string id) => DialogueFile.Parse(File.ReadAllText(Path.Combine(Dir("Dialogue"), $"{id}.json")));

    [TestCase]
    public void GuardianRunEndsWithTheDuoAndThenTheAvessoRoom()
    {
        var planner = new RunPlanner(Config);
        for (var seed = 1; seed <= 60; seed++)
        {
            var plan = planner.Plan(seed, 1, true);
            AssertThat(plan.HasGuardian).IsTrue();
            AssertThat(plan.Rooms[^1].Ground).IsEqual("avesso_final");
            AssertThat(plan.Rooms[^1].MonsterCount).IsEqual(0);
            AssertThat(Config.FinalGrounds).Contains(plan.Rooms[^2].Ground);
            AssertThat(plan.Rooms[^2].Kind).IsEqual(RoomKind.Guardian);
            AssertThat(plan.Rooms[^3].Kind).IsEqual(RoomKind.Rest);
        }
    }

    [TestCase]
    public void OrdinaryRunsMixEveryNeighbourhoodWithoutTheAvessoRoom()
    {
        var planner = new RunPlanner(Config);
        var grounds = new HashSet<string>();
        var monsters = new HashSet<string>();
        for (var seed = 1; seed <= 150; seed++)
        {
            var plan = planner.Plan(seed, 1, false);
            AssertThat(plan.HasGuardian).IsFalse();
            AssertThat(plan.Rooms[0].Waves.SelectMany(w => w).All(s => s.Monster == "ceifador_pomar")).IsTrue();
            foreach (var room in plan.Rooms)
            {
                AssertThat(room.Ground).IsNotEqual("avesso_final");
                grounds.Add(room.Ground);
                foreach (var spawn in room.Waves.SelectMany(w => w))
                {
                    AssertThat(AvessoMonsters).Contains(spawn.Monster);
                    monsters.Add(spawn.Monster);
                }
            }
        }

        AssertThat(monsters).ContainsExactlyInAnyOrder(AvessoMonsters);
        // Bairros misturados: salas novas do Avesso e salas dos baralhos anteriores.
        AssertThat(grounds).Contains("avesso_cais_pomar", "avesso_torre_cemiterio", "avesso_ruas_esgoto", "cais_pier", "esgoto_cruzamento", "pomar_corredor");
    }

    [TestCase]
    public void EpilogueGroundMustBeKnown()
    {
        var config = Config;
        config.EpilogueGround = "nao_existe";
        AssertThat(config.Validate()).IsNotEmpty();
        config = Config;
        config.FinalGrounds = ["nao_existe"];
        AssertThat(config.Validate()).IsNotEmpty();
    }

    [TestCase]
    public void OtherBreachesHaveNoEpilogueNorFinalRotation()
    {
        foreach (var file in new[] { "cacada.json", "cacada_pomar.json", "cacada_cais.json", "cacada_ruas.json" })
        {
            var config = HuntConfig.Parse(File.ReadAllText(Path.Combine(Dir("Cacada"), file)));
            AssertThat(config.EpilogueGround).OverrideFailureMessage(file).IsEqual(string.Empty);
            AssertThat(config.FinalGrounds).OverrideFailureMessage(file).IsEmpty();
            AssertThat(new RunPlanner(config).Plan(5, 2, true).Rooms[^1].Ground).IsEqual(config.FinalGround);
        }
    }

    [TestCase]
    public void BrechaIsRankSSWithShortWarningsForTheDuo()
    {
        var def = Brecha;
        AssertThat(def.Id).IsEqual("avesso_profundo");
        AssertThat(def.Rank).IsEqual("SS");
        AssertThat(def.GuardianId).IsEqual("guardiao_revertido");
        AssertThat(def.Hub).IsEqual("praca");
        foreach (var id in new[] { "ceifador_pomar", "sombra_cacador_lamina", "sombra_cacador_arco", "sombra_cacador_martelo", "rasgo" })
        {
            AssertThat(def.Monsters.ContainsKey(id)).OverrideFailureMessage(id).IsTrue();
            AssertThat(def.Monsters[id].Telegraph).OverrideFailureMessage(id).IsBetween(0.4, 0.8);
        }

        foreach (var id in ImplementedGuardians)
        {
            // Guardiões em dupla: avisos encurtados para 0,4 s (CAMPANHA, Ato VII).
            AssertThat(def.Monsters[id].Boss).OverrideFailureMessage(id).IsTrue();
            AssertThat(def.Monsters[id].Telegraph).OverrideFailureMessage(id).IsEqual(0.4);
        }
    }

    [TestCase]
    public void EveryDuoArenaPairsTwoGuardiansAndTheRotationOnlyUsesReadyOnes()
    {
        var pairs = new Dictionary<string, (string, string)>
        {
            ["avesso_dupla_esgoto_pomar"] = ("lodo_mae", "macieira_oca"),
            ["avesso_dupla_cais_cemiterio"] = ("farol_afogado", "velora"),
            ["avesso_dupla_torre_ruas"] = ("sineiro_mudo", "inquilino"),
        };
        foreach (var (id, (boss, partner)) in pairs)
        {
            var rift = Level(id).Rift!;
            AssertThat(rift.Boss!.Type).OverrideFailureMessage(id).IsEqual(boss);
            AssertThat(rift.Partner!.Type).OverrideFailureMessage(id).IsEqual(partner);
        }

        foreach (var id in Config.FinalGrounds)
        {
            var rift = Level(id).Rift!;
            AssertThat(ImplementedGuardians).Contains(rift.Boss!.Type, rift.Partner!.Type);
            AssertThat(Brecha.Monsters.ContainsKey(rift.Boss.Type)).IsTrue();
            AssertThat(Brecha.Monsters.ContainsKey(rift.Partner.Type)).IsTrue();
        }
    }

    [TestCase]
    public void EveryAvessoRoomIsWalkableFromTheEntrance()
    {
        foreach (var (id, ground) in Config.Grounds)
        {
            var level = Level(id);
            var grid = LevelGrid.Parse(level.Cell, level.Cols, level.Rows, level.Grid);
            var reach = RuasHuntTest.Reachable(grid, level.Spawn);
            var rift = level.Rift!;
            var points = ground.Spawns.Concat(ground.Breakables).ToList();
            if (ground.Rest.Length == 2)
            {
                points.Add(ground.Rest);
            }

            foreach (var p in new[] { rift.Chest, rift.ExitPortal, rift.Boss?.At, rift.Partner?.At })
            {
                if (p is not null)
                {
                    points.Add(p);
                }
            }

            foreach (var p in points)
            {
                var cell = ((int)(p[0] / grid.Cell), (int)(p[1] / grid.Cell));
                AssertThat(reach.Contains(cell)).OverrideFailureMessage($"{id}: ponto {p[0]},{p[1]} fora do chão andável").IsTrue();
            }
        }
    }

    [TestCase]
    public void AvessoRoomHasNoWayOutButTheAnswer()
    {
        var rift = Level("avesso_final").Rift!;
        AssertThat(rift.Exit).IsNull();
        AssertThat(rift.ExitPortal).IsNull();
        AssertThat(rift.Boss).IsNull();
    }

    [TestCase]
    public void SayingBackClosesTheBreachAndStayingIsASoftGameOver()
    {
        foreach (var (choice, flag, ev) in new[] { (1, "avesso_recusado", "CloseBreach"), (0, "avesso_ficou", "SoftGameOver:sopa") })
        {
            var engine = Engine();
            var events = new List<string>();
            engine.EventRaised += events.Add;
            var runner = new DialogueRunner(Dialogue("ato7/avesso"), engine, "Rui");
            AssertThat(runner.Start()).IsTrue();
            runner.Advance();
            AssertThat(runner.Choices.Count).IsEqual(2);
            runner.Advance(choice);
            while (runner.Advance())
            {
            }

            AssertThat(engine.HasFlag(flag)).IsTrue();
            AssertThat(events).Contains(ev);
        }
    }

    [TestCase]
    public void ActSevenStoryWalksFromTheDarkSquareToTheBell()
    {
        var engine = Engine();
        var steps = new (string Flag, string Objective)[]
        {
            ("ato6_lis", "ato7"),
            ("ato7_benicio", "ato7_brecha"),
            ("avesso_profundo:hunt_1", "ato7_vigia"),
            ("vigia_p5_feita", "ato7_celeste"),
            ("lis_revelada", "ato7_dupla"),
            ("avesso_recusado", "ato7_fecho"),
            ("ato7_fecho_benicio", "ato7_odete"),
            ("odete_vai_junto", "ato7_severo"),
        };
        foreach (var (flag, objective) in steps)
        {
            engine.SetFlag(flag);
            AssertThat(engine.ObjectiveId).OverrideFailureMessage(flag).IsEqual(objective);
        }

        AssertThat(engine.HasFlag("act7_done")).IsFalse();
        engine.SetFlag("tem_badalo");
        AssertThat(engine.HasFlag("ato7_vespera_final")).IsTrue();

        // Fecho do Ato VII: a Lis à noite, na escada. A fala liga act7_done, que abre o Final (FinalStory.UnlockFlag).
        var lis = new DialogueRunner(Dialogue("ato7/lis"), engine, "Rui");
        AssertThat(lis.Start()).IsTrue();
        AssertThat(lis.CurrentId).IsEqual("b1");
        while (lis.Advance(1))
        {
        }

        AssertThat(engine.HasFlag("act7_done")).IsTrue();
        AssertThat(engine.HasFlag("lis_escondida")).IsTrue();
        AssertThat(engine.ObjectiveId).IsEqual("final");
        foreach (var id in new[] { "ato7", "ato7_brecha", "ato7_vigia", "ato7_celeste", "ato7_dupla", "ato7_fecho", "ato7_odete", "ato7_severo" })
        {
            var title = engine.Script.Objective(id)!.Title;
            AssertThat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length).OverrideFailureMessage(title).IsLessEqual(5);
        }
    }

    [TestCase]
    public void TheDarkSquareReplacesTheSewerBreachAfterActSix()
    {
        var praca = Level("praca");
        AssertThat(praca.Ground.Variants.Single().Conditions).Contains("act6_done");
        var brechas = praca.Zones.Where(z => z.Kind == "brecha").ToList();
        AssertThat(brechas.Single(z => z.Brecha == "avesso_profundo").Conditions).Contains("act6_done");
        AssertThat(brechas.Single(z => z.Brecha.Length == 0).Conditions).Contains("!act6_done|parte1_fim");
        AssertThat(praca.Npcs.Any(n => n.Id == "severo" && n.Dialogue == "ato7/severo")).IsTrue();
    }

    [TestCase]
    public void EachAvessoDeathPlaysANewLineOnceInTwoSentenceBalloons()
    {
        var engine = Engine();
        var file = Dialogue("ato7/morte");
        var seen = new List<string>();
        for (var n = 1; n <= 6; n++)
        {
            engine.SetFlag($"avesso_profundo:fell_{n}");
            var runner = new DialogueRunner(file, engine, "Rui");
            AssertThat(runner.Start()).IsTrue();
            seen.Add(runner.CurrentId!);
            do
            {
                // Duas frases por balão, no máximo (ROTEIRO-V2 §2.1).
                AssertThat(runner.Text.Count(c => c is '.' or '?' or '!')).OverrideFailureMessage(runner.Text).IsLessEqual(2);
            }
            while (runner.Advance());

            AssertThat(engine.HasFlag($"morte_avesso_{n}")).IsTrue();
        }

        AssertThat(seen).ContainsExactly("m1", "m2", "m3", "m4", "m5", "m6");
    }
}
