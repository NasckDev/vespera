using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Ranks;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Hunt;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// O arco do Ato I jogado de verdade (<c>--ato1</c>, playtest 23/10): caçadas SEM invulnerabilidade e sem dano extra
/// (o bot esquiva de metade dos avisos e bebe poção com pouca vida), queda possível e repetida até passar, Tia Nilza,
/// forja, contratos, exame do Benício, Lodo-Mãe, rank D, Eco e gancho do Ato II. No fim, registra os números de QA.
/// </summary>
public partial class SmokeTest
{
    private const int MaxHuntTries = 5;
    private readonly List<HuntMetrics> _hunts = [];
    private readonly List<string> _scenes = [];
    private int _dialogueChars;

    private bool Flag(string flag) => StoryService.Engine.HasFlag(flag);

    /// <summary>Do portal da Praça até a Lodo-Mãe, com tudo o que a cidade oferece entre as caçadas.</summary>
    private async Task<Level> ActOneArc(Level level)
    {
        level = await HuntUntil(level, StoryFlags.HuntOne, "caca1");
        Check(StoryService.Engine.ObjectiveId == "forja", $"depois da 1ª caçada o objetivo deveria ser a forja, é '{StoryService.Engine.ObjectiveId}'");
        Check(Flag(StoryFlags.MetNilza), "na volta da Brecha a Tia Nilza deveria vir dar a poção");

        level = await ActOneForgeUpgrade(level);
        level = await ActOneTown(level);
        level = await HuntUntil(level, StoryFlags.HuntTwo, "caca2");
        level = await ActOneExam(level);
        Check(HuntService.GuardianUnlocked, "com o exame aprovado a Lodo-Mãe deveria esperar no fim da Brecha");
        level = await HuntUntil(level, StoryFlags.GuardianDefeatedLodoMae, "caca_mae");
        Check(Progression.Rank == Rank.D, $"depois da Lodo-Mãe o rank deveria ser D, é {Progression.Rank}");
        Check(_scenes.Contains("ato1_fim"), $"a cena do fim do Ato I deveria tocar (cenas: {string.Join(",", _scenes)})");
        Check(Flag(StoryFlags.Ato1FimDone), "o fim do Ato I deveria ligar ato1_fim_done");
        Check(HuntService.HasEcoViscoso, "a Lodo-Mãe deveria deixar o Eco Viscoso");
        Check(StoryService.Engine.ObjectiveId == "ato2" && StoryService.Engine.CurrentObjective?.Locked != true,
            $"o objetivo final deveria ser o Ato II (aberto: Pomar Alto), é '{StoryService.Engine.ObjectiveId}'");
        LogActOneNumbers();
        return level;
    }

    /// <summary>Caça até a flag ligar (a queda manda de volta à Praça; tenta de novo).</summary>
    private async Task<Level> HuntUntil(Level level, string flag, string label)
    {
        for (var i = 0; i < MaxHuntTries && !Flag(flag); i++)
        {
            StockPotions();
            var shots = _shotsDir is not null && DisplayServer.GetName() != "headless";
            var bot = new CombatBot(level, this) { Shots = shots && i == 0, ShotPrefix = $"ato1_{label}", DodgeSkill = 0.5 };
            var metrics = await bot.PlayHunt(invulnerable: false);
            _failures.AddRange(bot.Failures);
            _hunts.Add(metrics);
            level = await Hub();
            await PlayPendingScenes(level);
            if (bot.Failures.Count > 0)
            {
                break;
            }
        }

        Check(Flag(flag), $"depois de {MaxHuntTries} tentativas a flag '{flag}' deveria estar ligada");
        return level;
    }

    /// <summary>Como um jogador prevenido (o Benício manda): enche a bolsa de poções na Tia Nilza com os Lumes que tem.</summary>
    private static void StockPotions()
    {
        var bought = 0;
        while (HuntService.BuyPotion(withFlower: false).Ok)
        {
            bought++;
        }

        Log.Info($"ato I: poções na bolsa {HuntService.State.Potions} (+{bought} compradas), {Progression.State.Lumes} Lumes");
    }

    private async Task<Level> Hub()
    {
        await WaitFor(() => _router.Current is Level { Switching: false } l && l.World.Data.Id == Level.HubLevelId && !GetTree().Paused, "Praça");
        return (Level)_router.Current!;
    }

    /// <summary>Toca as cenas automáticas pendentes (no jogo abrem sozinhas; o smoke as chama à mão).</summary>
    private async Task PlayPendingScenes(Level level)
    {
        for (var i = 0; i < 4; i++)
        {
            await Seconds(0.3);
            var auto = StoryService.Engine.AutoplayFor(level.World.Data.Id);
            if (auto is null || !level.PlayAutoplay())
            {
                return;
            }

            await WaitForDialogue(level, $"cena {auto.Dialogue}");
            _scenes.Add(auto.Dialogue);
            Log.Info($"ato I: cena '{auto.Dialogue}' (nó {level.Dialogue.CurrentNodeId})");
            await Shot($"ato1_cena_{auto.Dialogue}", 0.5);
            await SkipDialogue(level);
        }
    }

    private async Task<Level> ActOneForgeUpgrade(Level level)
    {
        level = await Travel(level, z => z.Target == "forjas", new Vector2(24, 0), "Level_forjas");
        level = await Travel(level, z => z.Target == "forja_interior", new Vector2(0, 40 * level.World.Data.ActorScale), "Level_forja_interior");
        var check = Progression.Forge.CanUpgrade();
        Log.Info($"ato I: forja depois da 1ª caçada: pode {check.CanUpgrade} ({check.Reason}); Fragmentos {Progression.State.Fragmentos}, " +
            $"materiais [{string.Join(", ", Progression.State.Materials.Select(m => $"{m.Key} {m.Value}"))}]");
        await TalkToNpc(level, "odete");
        await WaitFor(() => FindOverlay<ForgeScreen>() is not null, "forja aberta pela Odete depois da caçada");
        await Shot("ato1_forja_primeira");
        if (check.CanUpgrade)
        {
            await Tap(Key.Enter);
            await Seconds(1.0);
        }

        await Tap(Key.Escape);
        await Frames(4);
        await Seconds(0.4);
        if (check.CanUpgrade)
        {
            Check(Flag(StoryFlags.Forged), "com recursos, a forja deveria melhorar a arma");
            Check(StoryService.Engine.ObjectiveId is "cacada2" or "exame", $"depois de forjar o objetivo deveria ser a 2ª caçada, é '{StoryService.Engine.ObjectiveId}'");
        }
        else
        {
            Log.Info("ato I: ainda faltam materiais para forjar (o sorteio do saque); o bot volta à forja depois da próxima caçada");
        }

        level = await Travel(level, z => z.Target == "forjas", new Vector2(0, -30 * level.World.Data.ActorScale), "Level_forjas");
        return await Travel(level, z => z.Target == "praca", new Vector2(-24, 0), "Level_praca");
    }

    /// <summary>Cidade entre caçadas: a Lis explica as salas, a Nilza vende poção, o Benício mostra os contratos.</summary>
    private async Task<Level> ActOneTown(Level level)
    {
        await TalkToNpc(level, "lis", shot: "ato1_lis_salas");
        var potions = HuntService.State.Potions;
        await OpenTalk(level, "nilza");
        await Choose(level, "Comprar");
        await SkipDialogue(level);
        Log.Info($"ato I: Nilza: {potions} -> {HuntService.State.Potions} poções, {Progression.State.Lumes} Lumes");
        await PressInWorld(Key.E);
        await OpenTalk(level, "benicio");
        await Choose(level, "Ver os contratos");
        await SkipDialogue(level);
        await WaitFor(() => ContractsScreen.Active is not null, "mural de contratos");
        await Shot("ato1_contratos");
        ContractsScreen.Active?.Close();
        await Frames(4);
        await Seconds(0.4);
        return level;
    }

    /// <summary>Exame do Benício: se faltar algo, ele diz o quê e o bot caça mais uma vez.</summary>
    private async Task<Level> ActOneExam(Level level)
    {
        for (var i = 0; i < MaxHuntTries && !Flag(StoryFlags.GuardianGate); i++)
        {
            if (!Flag(StoryFlags.Forged) && Progression.Forge.CanUpgrade().CanUpgrade)
            {
                level = await ActOneForgeUpgrade(level);
            }

            await OpenTalk(level, "benicio");
            await Choose(level, "Quero fazer o exame");
            Log.Info($"ato I: exame do Benício: nó '{level.Dialogue.CurrentNodeId}' — {level.Dialogue.FullText}");
            await Shot($"ato1_exame_{i}", 0.4);
            await SkipDialogue(level);
            if (!Flag(StoryFlags.GuardianGate))
            {
                StockPotions();
                var bot = new CombatBot(level, this) { DodgeSkill = 0.5 };
                _hunts.Add(await bot.PlayHunt(invulnerable: false));
                _failures.AddRange(bot.Failures);
                level = await Hub();
                await PlayPendingScenes(level);
            }
        }

        Check(Flag(StoryFlags.GuardianGate), "o Benício deveria aprovar o exame");
        Check(StoryService.Engine.ObjectiveId == "lodo_mae", $"depois do exame o objetivo deveria ser a Lodo-Mãe, é '{StoryService.Engine.ObjectiveId}'");
        return level;
    }

    private async Task OpenTalk(Level level, string npcId)
    {
        var npc = level.World.Npcs.First(n => n.NpcId == npcId);
        await StandAt(level, level.World.ClampToFloor(npc.GlobalPosition + new Vector2(0, 1.0f * level.World.Data.HumanHeight)));
        await PressInWorld(Key.E);
        Check(level.DialogueOpen, $"E em {npcId} deveria abrir a conversa");
    }

    /// <summary>Avança a conversa até aparecerem respostas e aperta a que começa com <paramref name="prefix"/>.</summary>
    private async Task Choose(Level level, string prefix)
    {
        for (var i = 0; i < 40 && level.DialogueOpen && !level.Dialogue.HasChoices; i++)
        {
            if (level.Dialogue.Typing)
            {
                level.Dialogue.Complete();
                await Frames(2);
                continue;
            }

            await PressInWorld(Key.E);
        }

        var button = level.Dialogue.ChoiceButtons.FirstOrDefault(b => b.Text.StartsWith(prefix, System.StringComparison.Ordinal));
        Check(button is not null, $"a conversa deveria oferecer '{prefix}' (opções: {string.Join(" | ", level.Dialogue.ChoiceButtons.Select(b => b.Text))})");
        button?.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
    }

    /// <summary>Números do QA do Ato I no log (o relatório do dev usa estes).</summary>
    private void LogActOneNumbers()
    {
        var huntSeconds = _hunts.Sum(h => h.Seconds);
        var deaths = _hunts.Count(h => h.Died);
        foreach (var (h, i) in _hunts.Select((h, i) => (h, i + 1)))
        {
            Log.Info($"qa-ato1 caçada {i}: {(h.Died ? "caiu" : "voltou")}, salas {h.Rooms}/{h.RoomCount}, {h.Kills} monstros, " +
                $"vida perdida {h.DamageTaken} (vida máx {h.MaxHp}), {h.HitsTaken} golpes, {h.Dodges} esquivas, {h.Potions} poções, {h.Seconds:0} s" +
                $"{(h.Guardian ? ", com a Lodo-Mãe" : string.Empty)}");
        }

        // Leitura: ~14 caracteres por segundo para quem lê com calma; cidade: andar entre NPCs e mapas (~25 s por conversa/viagem).
        var readSeconds = _dialogueChars / 14.0;
        Log.Info($"qa-ato1 total: {_hunts.Count} caçadas, {deaths} quedas, {huntSeconds / 60:0.0} min dentro da Brecha (bot), " +
            $"{_dialogueChars} caracteres de fala (~{readSeconds / 60:0.0} min de leitura)");
    }
}
