using Vespera.Core.Common;

namespace Vespera.Core.Hunt;

/// <summary>Um monstro de uma onda: tipo, pé no chão da sala e se é a variante de elite.</summary>
public sealed record SpawnPlan(string Monster, float X, float Y, bool Elite);

/// <summary>Uma sala da caçada: chão (fase de data/levels), tipo, ondas e posição na caçada.</summary>
public sealed record RoomPlan(int Index, string Ground, RoomKind Kind, IReadOnlyList<IReadOnlyList<SpawnPlan>> Waves)
{
    public int MonsterCount => Waves.Sum(w => w.Count);

    public int EliteCount => Waves.Sum(w => w.Count(s => s.Elite));
}

/// <summary>A caçada montada numa entrada do portal: semente, salas em ordem e se termina na Lodo-Mãe.</summary>
public sealed record RunPlan(int Seed, IReadOnlyList<RoomPlan> Rooms)
{
    public bool HasGuardian => Rooms.Count > 0 && Rooms[^1].Kind == RoomKind.Guardian;

    public int WaveCount => Rooms.Sum(r => r.Waves.Count);

    public int MonsterCount => Rooms.Sum(r => r.MonsterCount);
}

/// <summary>
/// Monta uma caçada (5-7 salas) a partir dos chãos do esgoto e da semente (playtest 23/10): a primeira sala é de combate
/// leve, a dificuldade sobe sala a sala (ondas maiores, mais Espectros, elites), há no máximo uma sala de tesouro
/// (opcional), sempre uma sala de descanso antes do fim, e o fim é o Covil — com a Lodo-Mãe só depois de
/// <see cref="HuntConfig.GuardianAfterRuns"/> caçadas completas; antes disso, uma sala de elite.
/// Mesma semente e mesmo estado = mesma caçada.
/// </summary>
public sealed class RunPlanner(HuntConfig config)
{
    private const string Lodo = "lodo";
    private const string Espectro = "espectro";
    private const string FinalGround = "esgoto_arena";

    public HuntConfig Config => config;

    /// <summary>A Lodo-Mãe já espera no fim da caçada?</summary>
    public bool GuardianUnlocked(int completedRuns) => completedRuns >= config.GuardianAfterRuns;

    public RunPlan Plan(int seed, int completedRuns)
    {
        var random = new SeededRandom(seed);
        var count = random.RangeInt(config.RoomsMin, config.RoomsMax);
        var kinds = Layout(count, random, GuardianUnlocked(completedRuns));
        var rooms = new List<RoomPlan>(count);
        string? previous = null;
        for (var i = 0; i < count; i++)
        {
            var ground = i == count - 1 ? FinalGround : PickGround(kinds[i], previous, random);
            previous = ground;
            rooms.Add(new RoomPlan(i, ground, kinds[i], Waves(ground, kinds[i], i, count, random)));
        }

        return new RunPlan(seed, rooms);
    }

    private List<RoomKind> Layout(int count, IRandom random, bool guardian)
    {
        var kinds = Enumerable.Repeat(RoomKind.Combat, count).ToList();
        kinds[^1] = guardian ? RoomKind.Guardian : RoomKind.Elite;
        kinds[^2] = RoomKind.Rest;
        if (count >= 5 && random.NextDouble() < config.TreasureChance)
        {
            kinds[random.RangeInt(1, count - 3)] = RoomKind.Treasure;
        }

        // Uma sala de elite no meio, nas caçadas mais longas.
        if (count >= 6)
        {
            var middle = Enumerable.Range(2, count - 4).Where(i => kinds[i] == RoomKind.Combat).ToList();
            if (middle.Count > 0)
            {
                kinds[middle[random.RangeInt(0, middle.Count - 1)]] = RoomKind.Elite;
            }
        }

        return kinds;
    }

    private string PickGround(RoomKind kind, string? previous, IRandom random)
    {
        var options = config.Grounds.Where(g => g.Value.Kinds.Contains(kind)).Select(g => g.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var fresh = options.Where(id => id != previous).ToList();
        var pool = fresh.Count > 0 ? fresh : options;
        return pool[random.RangeInt(0, pool.Count - 1)];
    }

    private List<IReadOnlyList<SpawnPlan>> Waves(string ground, RoomKind kind, int index, int count, IRandom random)
    {
        var waves = new List<IReadOnlyList<SpawnPlan>>();
        if (kind is RoomKind.Rest or RoomKind.Treasure or RoomKind.Guardian)
        {
            return waves;
        }

        var spawns = config.Grounds[ground].Spawns;
        var depth = count <= 1 ? 0.0 : index / (double)(count - 1);
        var waveCount = 2 + (index >= config.ExtraWaveFromRoom ? 1 : 0);
        var espectroShare = Math.Min(config.EspectroShareMax, config.EspectroShareStart + (config.EspectroSharePerRoom * index));
        for (var w = 0; w < waveCount; w++)
        {
            var size = Math.Min(config.MaxWaveSize, (int)Math.Round(config.FirstWaveSize + (config.WaveSizePerRoom * index) + (w * 0.5 * depth)));
            var points = Shuffle(spawns.Count, random).Take(size).ToList();
            var wave = new List<SpawnPlan>(size);
            var elites = EliteCount(kind, index, w, waveCount, random);
            for (var s = 0; s < size; s++)
            {
                var monster = random.NextDouble() < espectroShare ? Espectro : Lodo;
                var p = spawns[points[s]];
                wave.Add(new SpawnPlan(monster, p[0], p[1], s < elites));
            }

            waves.Add(wave);
        }

        return waves;
    }

    private int EliteCount(RoomKind kind, int index, int wave, int waveCount, IRandom random)
    {
        if (kind == RoomKind.Elite)
        {
            // Sala de elite: um elite por onda e dois na última.
            return wave == waveCount - 1 ? 2 : 1;
        }

        return index >= config.EliteChanceFromRoom && random.NextDouble() < config.EliteChance ? 1 : 0;
    }

    private static List<int> Shuffle(int n, IRandom random)
    {
        var order = Enumerable.Range(0, n).ToList();
        for (var i = n - 1; i > 0; i--)
        {
            var j = random.RangeInt(0, i);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }
}
