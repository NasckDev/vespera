using System;
using System.Collections.Generic;
using System.Linq;
using Vespera.Core.Advancement;
using Vespera.Core.Common;
using Vespera.Core.Hunt;
using Vespera.Core.Ranks;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Hunter;

/// <summary>Resultado de uma caçada para a tela de resultados: relatório, contratos, XP/rank antes e depois.</summary>
public sealed record RunSummary(
    RunReport Report,
    IReadOnlyList<ContractProgress> Contracts,
    RankStatus Before,
    RankStatus After,
    int BonusFragmentos,
    bool ExamJustReady);

/// <summary>
/// Caçadas da Brecha (playtest 23/10): monta a caçada a cada entrada (<see cref="RunPlanner"/>), anota o que acontece
/// (mortes, ondas, golpes levados, saque) num <see cref="RunReport"/>, paga os contratos do mural na volta e cuida das
/// poções, das flores, da moeda do poço e dos quebráveis. Estado no save (<see cref="HuntState"/>); estático, sem nó.
/// </summary>
public static class HuntService
{
    public const string ConfigFile = "res://data/cacada/cacada.json";

    private static HuntConfig? _config;
    private static RunPlanner? _planner;
    private static bool _luckActive;
    private static RankStatus? _rankAtStart;
    private static int _xpAtStart;
    private static int _hitsAtRoomStart;
    private static bool _subscribed;

    public static HuntConfig Config => _config ??= HuntConfig.Parse(UserFile.ReadText(ConfigFile)
        ?? throw new InvalidOperationException($"Dado do jogo ausente: {ConfigFile}"));

    public static RunPlanner Planner => _planner ??= new RunPlanner(Config);

    public static ContractBoard Board => new(Config, MaterialName);

    public static HuntState State
    {
        get
        {
            var save = RiftSession.Save;
            save.Hunt ??= new HuntState();
            return save.Hunt;
        }
    }

    /// <summary>Caçada em andamento (null fora da Brecha).</summary>
    public static RunPlan? Plan { get; private set; }

    /// <summary>Relatório da caçada em andamento (ou da última, até a próxima entrada).</summary>
    public static RunReport? Report { get; private set; }

    /// <summary>A Lodo-Mãe espera no fim das próximas caçadas (o Benício aprovou o exame do rank E).</summary>
    public static bool GuardianUnlocked => Story.StoryService.Engine.HasFlag(StoryFlags.GuardianGate) || ReadyForExam;

    /// <summary>Caçadas completas suficientes para o Benício aceitar o exame.</summary>
    public static bool ReadyForExam => Planner.ReadyForExam(State.RunsCompleted);

    /// <summary>Tem o Eco Viscoso (habilidade do Q).</summary>
    public static bool HasEcoViscoso => Progression.IsActive && Progression.State.Echoes.Contains("eco_viscoso");

    /// <summary>Contratos em oferta (sorteia se o mural estiver vazio).</summary>
    public static IReadOnlyList<Contract> Contracts
    {
        get
        {
            var state = State;
            if (state.Contracts.Count == 0)
            {
                state.Contracts = Board.Roll(new SeededRandom(state.NextSeed + (state.Returns * 7919)));
            }

            return state.Contracts;
        }
    }

    /// <summary>Flores ainda colhíveis na Praça hoje.</summary>
    public static int FlowersLeft
    {
        get
        {
            if (State.FlowersLeft < 0)
            {
                State.FlowersLeft = Config.Flowers.PerReturn;
            }

            return State.FlowersLeft;
        }
    }

    /// <summary>Começa uma caçada: sorteia as salas (semente do save) e zera o relatório. Devolve o plano.</summary>
    public static RunPlan BeginRun()
    {
        Subscribe();
        var state = State;
        _ = Contracts;
        state.RunsStarted++;
        var seed = state.NextSeed++;
        Plan = Planner.Plan(seed, state.RunsCompleted, GuardianUnlocked);
        Report = new RunReport { RoomCount = Plan.Rooms.Count };
        _luckActive = state.WellLuck;
        state.WellLuck = false;
        _rankAtStart = Progression.IsActive ? Progression.Status : null;
        _xpAtStart = Progression.IsActive ? Progression.State.Xp : 0;
        Log.Info($"caçada {state.RunsStarted}: semente {seed}, {Plan.Rooms.Count} salas [{string.Join(", ", Plan.Rooms.Select(r => $"{r.Kind}:{r.Ground}:{r.MonsterCount}"))}]" +
            $"{(Plan.HasGuardian ? " com a Lodo-Mãe" : string.Empty)}");
        return Plan;
    }

    /// <summary>Conteúdo da sala <paramref name="index"/> da caçada sobre o chão <paramref name="ground"/> (do JSON da fase).</summary>
    public static RiftRoomData RoomRift(int index, LevelData ground)
    {
        var plan = Plan ?? throw new InvalidOperationException("sem caçada em andamento");
        var room = plan.Rooms[index];
        var baseRift = ground.Rift ?? new RiftRoomData();
        var last = index == plan.Rooms.Count - 1;
        var kindName = room.Kind switch
        {
            RoomKind.Rest => "Descanso",
            RoomKind.Treasure => "Tesouro",
            RoomKind.Guardian => "Covil da Lodo-Mãe",
            RoomKind.Elite when last => "Covil",
            RoomKind.Elite => "Ninho de elite",
            _ => ground.Rift?.Title.Split('—')[^1].Trim() ?? "Galeria",
        };
        var data = new RiftRoomData
        {
            Title = $"{RiftSession.Brecha.Name} — Sala {index + 1}/{plan.Rooms.Count}: {kindName}",
            Sealed = baseRift.Sealed,
            Exit = baseRift.ExitPortal is null ? baseRift.Exit : null,
            ExitPortal = baseRift.ExitPortal,
            Chest = room.Kind == RoomKind.Treasure ? baseRift.Chest : null,
            Boss = room.Kind == RoomKind.Guardian ? baseRift.Boss : null,
            Waves = room.Waves.Select(w => w.Select(s => new SpawnData { Type = s.Monster, At = [s.X, s.Y], Elite = s.Elite }).ToList()).ToList(),
            Kind = room.Kind.ToString().ToLowerInvariant(),
        };

        if (room.Kind == RoomKind.Rest && Config.Grounds.TryGetValue(room.Ground, out var g) && g.Rest.Length == 2)
        {
            data.Rest = g.Rest;
        }

        if (room.Kind is RoomKind.Combat or RoomKind.Elite or RoomKind.Treasure && Config.Grounds.TryGetValue(room.Ground, out var gb))
        {
            data.Breakables = gb.Breakables;
        }

        return data;
    }

    public static RoomPlan? CurrentRoomPlan => Plan is { } p && RiftSession.RoomIndex < p.Rooms.Count ? p.Rooms[RiftSession.RoomIndex] : null;

    /// <summary>Uma sala começou: anota quantos golpes o caçador já levou (para "sala sem dano").</summary>
    public static void RoomStarted() => _hitsAtRoomStart = Report?.HitsTaken ?? 0;

    /// <summary>Sala de combate limpa.</summary>
    public static void RoomCleared(bool combat)
    {
        if (Report is not { } r)
        {
            return;
        }

        r.RoomsCleared++;
        if (combat && r.HitsTaken == _hitsAtRoomStart)
        {
            r.FlawlessRooms++;
        }
    }

    public static void WaveCleared()
    {
        if (Report is { } r)
        {
            r.WavesSurvived++;
        }
    }

    public static void Killed(bool elite, bool boss)
    {
        if (Report is not { } r || !RiftSession.InRift)
        {
            return;
        }

        r.Kills++;
        r.EliteKills += elite ? 1 : 0;
        r.GuardianDefeated |= boss;
        State.TotalKills++;
    }

    /// <summary>O caçador levou um golpe (dano já descontado).</summary>
    public static void HunterHit(int damage)
    {
        if (Report is { } r && RiftSession.InRift)
        {
            r.HitsTaken++;
            r.DamageTaken += damage;
        }
    }

    public static void AddSeconds(double seconds)
    {
        if (Report is { } r && RiftSession.InRift)
        {
            r.Seconds += seconds;
        }
    }

    /// <summary>
    /// Fim da caçada (depois do <see cref="Progression.EndRun"/>): paga contratos, aplica a sorte do poço, conta a caçada
    /// completa (a Lodo-Mãe aparece depois de N), renova contratos e flores e devolve o resumo para a tela de resultados.
    /// </summary>
    public static RunSummary? EndRun(bool survived, bool completed, RunEnd? end)
    {
        if (Report is not { } report)
        {
            return null;
        }

        var state = State;
        var readyBefore = ReadyForExam;
        report.Survived = survived;
        report.Completed = completed;
        report.Fragmentos = end?.FragmentosBanked ?? 0;
        var bonus = 0;
        if (survived && _luckActive && report.Fragmentos > 0 && Progression.IsActive)
        {
            bonus = Math.Max(1, report.Fragmentos * Config.Well.FragmentBonusPct / 100);
            Progression.State.Fragmentos += bonus;
        }

        var board = Board;
        var results = Contracts.Select(c => board.Evaluate(c, report)).ToList();
        foreach (var done in results.Where(r => r.Done))
        {
            state.ContractsDone++;
            if (!Progression.IsActive)
            {
                continue;
            }

            Progression.State.Fragmentos += done.Reward.Fragmentos;
            Progression.State.Lumes += done.Reward.Lumes;
            Progression.AddXp(done.Reward.Xp);
        }

        if (survived && completed)
        {
            state.RunsCompleted++;
            if (state.BestRunSeconds <= 0 || report.Seconds < state.BestRunSeconds)
            {
                state.BestRunSeconds = report.Seconds;
            }
        }

        state.Deaths += survived ? 0 : 1;
        report.Xp = Progression.IsActive ? Progression.State.Xp - _xpAtStart : 0;
        NewDay();
        var after = Progression.IsActive ? Progression.Status : null;
        var summary = new RunSummary(report, results, _rankAtStart ?? after!, after ?? _rankAtStart!, bonus, !readyBefore && ReadyForExam);
        Plan = null;
        _luckActive = false;
        if (Progression.IsActive)
        {
            Progression.Notify();
            Progression.Save();
        }

        Log.Info($"caçada encerrada: {(survived ? "viva" : "caiu")}{(completed ? ", completa" : string.Empty)}; {report.Kills} mortes, " +
            $"{report.DamageTaken} de dano levado, {report.Seconds:0} s, {report.Fragmentos}+{bonus} Fragmentos, {report.Xp} XP, " +
            $"contratos {results.Count(r => r.Done)}/{results.Count}; caçadas completas {state.RunsCompleted}");
        return summary;
    }

    /// <summary>Volta da Brecha = um novo "dia": contratos sorteados de novo, flores e quebráveis renovam.</summary>
    private static void NewDay()
    {
        var state = State;
        state.Returns++;
        state.Contracts = Board.Roll(new SeededRandom(state.NextSeed + (state.Returns * 7919)));
        state.FlowersLeft = Config.Flowers.PerReturn;
        state.Broken.Clear();
    }

    /// <summary>Bebe uma poção: cura <see cref="PotionDef.HealPct"/> da vida máxima. Falso sem poção ou com a vida cheia.</summary>
    public static bool TryDrink(Combat.HunterCombat combat)
    {
        var state = State;
        if (state.Potions <= 0 || combat.Hp >= combat.MaxHp || combat.IsDead)
        {
            return false;
        }

        state.Potions--;
        combat.Heal((int)Math.Round(combat.MaxHp * Config.Potions.HealPct));
        return true;
    }

    /// <summary>Compra uma poção na barraca (com flor fica mais barata). Devolve a mensagem para o jogador.</summary>
    public static (bool Ok, string Message) BuyPotion(bool withFlower)
    {
        var state = State;
        var p = Config.Potions;
        if (state.Potions >= p.Max)
        {
            return (false, $"A bolsa só leva {p.Max} poções.");
        }

        if (withFlower && state.Flowers <= 0)
        {
            return (false, "Você não tem flores. Colha algumas no carrinho de flores da Praça.");
        }

        var price = withFlower ? p.FlowerPrice : p.Price;
        if (!Progression.IsActive || Progression.State.Lumes < price)
        {
            return (false, $"Faltam Lumes: a poção custa {price}. Quebre barris e cumpra contratos.");
        }

        Progression.State.Lumes -= price;
        state.Flowers -= withFlower ? 1 : 0;
        state.Potions++;
        Progression.Notify();
        Progression.Save();
        return (true, withFlower ? $"Poção preparada com a flor (-{price} Lumes)." : $"Poção comprada (-{price} Lumes).");
    }

    /// <summary>Colhe uma flor (ingrediente de poção).</summary>
    public static (bool Ok, string Message) PickFlower()
    {
        if (FlowersLeft <= 0)
        {
            return (false, "Já colheu as flores de hoje. Amanhã (depois da próxima caçada) tem mais.");
        }

        State.FlowersLeft--;
        State.Flowers++;
        return (true, $"Você colheu uma flor-de-véspera ({State.Flowers} na bolsa). A barraca de poções usa como ingrediente.");
    }

    /// <summary>Joga uma moeda no poço: sorte na próxima caçada.</summary>
    public static (bool Ok, string Message) ThrowCoin()
    {
        var state = State;
        if (state.WellLuck)
        {
            return (false, "A moeda de hoje já brilha no fundo do poço. A sorte vai com você na próxima caçada.");
        }

        var price = Config.Well.Price;
        if (!Progression.IsActive || Progression.State.Lumes < price)
        {
            return (false, "Sem um Lume no bolso nem para o poço.");
        }

        Progression.State.Lumes -= price;
        state.WellLuck = true;
        Progression.Notify();
        return (true, $"Plim! A moeda some na água. Sorte na próxima caçada: +{Config.Well.FragmentBonusPct}% de Fragmentos se voltar viva.");
    }

    /// <summary>Quebrável quebrado: alguns Lumes (e às vezes um Fragmento). Devolve (lumes, fragmentos).</summary>
    public static (int Lumes, int Fragmentos) BreakableLoot(string key)
    {
        if (key.Length > 0 && !State.Broken.Contains(key))
        {
            State.Broken.Add(key);
        }

        var b = Config.Breakables;
        var lumes = RiftSession.Random.RangeInt(b.Lumes[0], b.Lumes[^1]);
        var fragment = RiftSession.InRift && RiftSession.Random.NextDouble() < b.FragmentChance ? 1 : 0;
        if (Progression.IsActive)
        {
            Progression.State.Lumes += lumes;
            if (fragment > 0)
            {
                Progression.GrantLoot(RiftSession.BreachRank, LootSource.Part);
            }

            Progression.Notify();
        }

        return (lumes, fragment);
    }

    public static bool IsBroken(string key) => State.Broken.Contains(key);

    /// <summary>Esquece a caçada em andamento (troca de save).</summary>
    public static void Reset()
    {
        Plan = null;
        Report = null;
        _luckActive = false;
    }

    private static void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        Progression.LootGranted += grant =>
        {
            if (Report is not { } r || !RiftSession.InRift)
            {
                return;
            }

            foreach (var entry in grant.Loot.Entries)
            {
                if (entry.Kind == LootEntryKind.Material)
                {
                    r.Materials[entry.Id] = r.MaterialCount(entry.Id) + entry.Amount;
                }
                else
                {
                    r.Items.Add(new LootLine(entry.Name, entry.Rank.ToString(), entry.Rare));
                }
            }
        };
    }

    public static string MaterialName(string id) =>
        Progression.Catalog.Materials.TryGetValue(id, out var m) ? m.Name : id;
}
