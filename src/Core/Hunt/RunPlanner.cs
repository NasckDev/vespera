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
/// leve (só Lodos), a dificuldade sobe sala a sala (ondas maiores, mais Espectros, elites) e caçada a caçada
/// (<see cref="HuntConfig.RunGrowth"/>), há no máximo uma sala de tesouro (opcional), sempre uma sala de descanso antes
/// do fim, e o fim é o Covil — com a Lodo-Mãe só quando a história abre o portão (exame do Benício); antes disso, uma
/// sala de elite. A primeira caçada tem <see cref="HuntConfig.FirstRunRooms"/> salas. Mesma semente e mesmo estado =
/// mesma caçada.
/// </summary>
public sealed class RunPlanner(HuntConfig config)
{
    private const float SwarmOffset = 40f;

    public HuntConfig Config => config;

    /// <summary>Caçadas completas suficientes para o exame do Benício?</summary>
    public bool ReadyForExam(int completedRuns) => completedRuns >= config.GuardianAfterRuns;

    /// <param name="completedRuns">Caçadas completas antes desta (deixa as ondas maiores).</param>
    /// <param name="guardian">A Lodo-Mãe espera no fim (portão da história aberto).</param>
    public RunPlan Plan(int seed, int completedRuns, bool guardian)
    {
        var random = new SeededRandom(seed);
        var rolled = random.RangeInt(config.RoomsMin, config.RoomsMax);
        var count = guardian ? config.GuardianRunRooms : completedRuns == 0 ? config.FirstRunRooms : rolled;
        var growth = config.RunGrowth * Math.Min(completedRuns, config.RunGrowthCap);
        var kinds = Layout(count, random, guardian);
        var rooms = new List<RoomPlan>(count);
        string? previous = null;
        var grounds = new string[count];
        for (var attempt = 0; attempt < 30; attempt++)
        {
            previous = null;
            for (var i = 0; i < count; i++)
            {
                grounds[i] = i == count - 1 ? config.FinalGround : PickGround(kinds[i], previous, random);
                previous = grounds[i];
            }

            AvoidRepeats(grounds, kinds, random);
            if (!grounds.Where((g, i) => i > 0 && g == grounds[i - 1]).Any())
            {
                break;
            }
        }

        Require(grounds, kinds);
        for (var i = 0; i < count; i++)
        {
            rooms.Add(new RoomPlan(i, grounds[i], kinds[i], Waves(grounds[i], kinds[i], i, count, growth, random)));
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
            kinds[random.RangeInt(1, Math.Max(1, count - 4))] = RoomKind.Treasure; // nunca colado ao descanso (os dois só existem no chão do tesouro)
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

    /// <summary>ROTEIRO-V2 §5.4: nunca duas salas iguais em sequência. Troca o chão de uma das duas por outro que aceite o tipo.</summary>
    private void AvoidRepeats(string[] grounds, List<RoomKind> kinds, IRandom random)
    {
        for (var i = 1; i < grounds.Length; i++)
        {
            if (grounds[i] != grounds[i - 1])
            {
                continue;
            }

            foreach (var j in new[] { i - 1, i })
            {
                if (j == grounds.Length - 1)
                {
                    continue;
                }

                var avoid = new HashSet<string>();
                if (j > 0)
                {
                    avoid.Add(grounds[j - 1]);
                }

                if (j < grounds.Length - 1)
                {
                    avoid.Add(grounds[j + 1]);
                }

                var options = config.Grounds.Where(g => g.Value.Kinds.Contains(kinds[j]) && !avoid.Contains(g.Key))
                    .Select(g => g.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();
                if (options.Count > 0)
                {
                    grounds[j] = options[random.RangeInt(0, options.Count - 1)];
                    break;
                }
            }
        }
    }

    /// <summary>
    /// <see cref="HuntConfig.RequiredGround"/>: se o sorteio deixou o chão de fora, ele toma o lugar da primeira sala
    /// de combate depois da entrada (nunca a primeira nem a última; como não estava em lugar nenhum, não repete vizinho).
    /// </summary>
    private void Require(string[] grounds, List<RoomKind> kinds)
    {
        var required = config.RequiredGround;
        if (required.Length == 0 || grounds.Contains(required) || !config.Grounds.TryGetValue(required, out var def))
        {
            return;
        }

        for (var i = 1; i < grounds.Length - 1; i++)
        {
            if (kinds[i] == RoomKind.Combat && def.Kinds.Contains(RoomKind.Combat))
            {
                grounds[i] = required;
                return;
            }
        }
    }

    private string PickGround(RoomKind kind, string? previous, IRandom random)
    {
        var options = config.Grounds.Where(g => g.Value.Kinds.Contains(kind)).Select(g => g.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var fresh = options.Where(id => id != previous).ToList();
        var pool = fresh.Count > 0 ? fresh : options;
        return pool[random.RangeInt(0, pool.Count - 1)];
    }

    private List<IReadOnlyList<SpawnPlan>> Waves(string ground, RoomKind kind, int index, int count, double growth, IRandom random)
    {
        var waves = new List<IReadOnlyList<SpawnPlan>>();
        if (kind is RoomKind.Rest or RoomKind.Treasure or RoomKind.Guardian)
        {
            return waves;
        }

        var spawns = config.Grounds[ground].Spawns;
        var depth = count <= 1 ? 0.0 : index / (double)(count - 1);
        var waveCount = 2 + (index >= config.ExtraWaveFromRoom ? 1 : 0);
        // A primeira sala é só de Lodos (o caçador aprende a bater e a esquivar antes do Espectro).
        var espectroShare = index == 0 ? 0 : Math.Min(config.EspectroShareMax, config.EspectroShareStart + (config.EspectroSharePerRoom * index));
        for (var w = 0; w < waveCount; w++)
        {
            var extra = index == 0 ? 0 : growth;
            var size = Math.Min(config.MaxWaveSize, (int)Math.Round(config.FirstWaveSize + (config.WaveSizePerRoom * index) + (w * 0.5 * depth) + extra));
            if (kind == RoomKind.Elite)
            {
                // Sala de elite: menos monstros, cada um mais perigoso (o playtest do bot caía sempre aqui).
                size = Math.Max(2, size - 2);
            }

            var points = Shuffle(spawns.Count, random).Take(size).ToList();
            var wave = new List<SpawnPlan>(size);
            var elites = EliteCount(kind, index, w, waveCount, growth > 0, random);
            for (var s = 0; s < size; s++)
            {
                var second = random.NextDouble() < espectroShare;
                var monster = second ? config.SecondMonster : config.CommonMonster;
                var swarm = second ? config.SecondSwarm : 1;
                // Terceiro monstro (só nas Brechas que têm): sorteado entre os comuns, depois da primeira sala.
                if (!second && index > 0 && config.ThirdMonster.Length > 0 && random.NextDouble() < config.ThirdShare)
                {
                    monster = config.ThirdMonster;
                    swarm = config.ThirdSwarm;
                }

                var p = spawns[points[s]];
                wave.Add(new SpawnPlan(monster, p[0], p[1], s < elites));
                // Enxame: o segundo monstro chega em bando, nos arredores do mesmo ponto (o diretor prende ao chão).
                for (var k = 1; k < swarm; k++)
                {
                    wave.Add(new SpawnPlan(monster, p[0] + (SwarmOffset * (k % 2 == 0 ? 1 : -1)), p[1] + (SwarmOffset * 0.5f * k), false));
                }
            }

            waves.Add(wave);
        }

        return waves;
    }

    private int EliteCount(RoomKind kind, int index, int wave, int waveCount, bool veteran, IRandom random)
    {
        if (kind == RoomKind.Elite)
        {
            // Sala de elite: um elite por onda; dois na última depois da primeira caçada completa.
            return veteran && wave == waveCount - 1 ? 2 : 1;
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
