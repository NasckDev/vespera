using Vespera.Core.Data;
using Vespera.Core.Save;

namespace Vespera.Core.Combat;

/// <summary>Sala da Brecha: a do tema (ondas) ou o Núcleo (chefe).</summary>
public static class BrechaRooms
{
    public const string Theme = "theme";
    public const string Core = "core";
}

/// <summary>O que a incursão pede ao mundo neste frame.</summary>
public enum BrechaRunEvent
{
    None,

    /// <summary>Nasce a onda <see cref="BrechaRun.Wave"/> − 1 (já avançada).</summary>
    SpawnWave,

    /// <summary>Ondas limpas: libere a porta para o Núcleo ("A passagem se abriu").</summary>
    DoorOpened,

    /// <summary>Ondas limpas num mapa com chefe no próprio mapa (Catedral): "O altar desperta".</summary>
    AltarAwakens,

    /// <summary>Hora de o chefe nascer.</summary>
    SpawnBoss,

    /// <summary>1.4 s depois do chefe cair: portal de saída.</summary>
    ExitPortal,
}

/// <summary>
/// Incursão numa Brecha (<c>G.run</c>; <c>enterBrecha</c>/<c>setupDungeon</c>/<c>updRun</c>/<c>bossDefeated</c>,
/// <c>world.js:75-122</c>; spec 01 §1.9, spec 02 §3.14): bolsa, sala, ondas com seus intervalos (4 s na primeira Brecha
/// da vida, 2.5 s nas demais, 1.6 s entre ondas), chefe (1.8 s no Núcleo, 2.2 s no altar) e portal de saída (1.4 s).
/// </summary>
public sealed class BrechaRun
{
    public const double FirstWaveDelay = 2.5;
    public const double FirstWaveDelayEasy = 4;
    public const double BetweenWaves = 1.6;
    public const double BossDelayCore = 1.8;
    public const double BossDelayAltar = 2.2;
    public const double ExitPortalDelay = 1.4;

    private double _exitT = -1;

    public BrechaRun(int brechaId, int portalSpot, ThemeDef theme, int rank, string seasonId, IReadOnlyList<IReadOnlyList<string>> waves, bool easy)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(waves);
        BrechaId = brechaId;
        PortalSpot = portalSpot;
        Theme = theme;
        Rank = rank;
        SeasonId = seasonId;
        Waves = waves;
        Easy = easy;
        WaveT = easy ? FirstWaveDelayEasy : FirstWaveDelay;
    }

    public int BrechaId { get; }

    public int PortalSpot { get; }

    public ThemeDef Theme { get; }

    public int Rank { get; }

    public string SeasonId { get; }

    public bool Easy { get; }

    public IReadOnlyList<IReadOnlyList<string>> Waves { get; }

    /// <summary>Tudo que foi coletado (só entra no save ao sair, fugir ou recuperar a Lanterna).</summary>
    public RunBag Bag { get; } = new();

    public string Room { get; private set; } = BrechaRooms.Theme;

    /// <summary>Próxima onda a nascer (0 = nenhuma ainda).</summary>
    public int Wave { get; private set; }

    public double WaveT { get; private set; }

    /// <summary>Sala atual limpa (ondas ou chefe).</summary>
    public bool Cleared { get; private set; }

    public double BossT { get; private set; }

    public bool BossSpawned { get; private set; }

    public bool BossDefeated { get; private set; }

    public int Kills { get; set; }

    public int PartsBroken { get; set; }

    /// <summary>Segundos dentro da Brecha (timer da incursão).</summary>
    public double Elapsed { get; private set; }

    /// <summary>Ondas atuais da sala do tema (para o spawner).</summary>
    public IReadOnlyList<string> CurrentWave => Waves[Wave - 1];

    /// <summary>
    /// Um frame. <paramref name="alive"/> = monstros vivos + nascendo na sala; <paramref name="bossInThemeMap"/> = o mapa tem
    /// <c>bossHere</c> (Catedral).
    /// </summary>
    public BrechaRunEvent Update(double dt, int alive, bool bossInThemeMap)
    {
        Elapsed += dt;
        if (_exitT >= 0)
        {
            _exitT -= dt;
            if (_exitT < 0)
            {
                return BrechaRunEvent.ExitPortal;
            }
        }

        if (Room == BrechaRooms.Theme && !Cleared && alive == 0)
        {
            if (Wave < Waves.Count)
            {
                WaveT -= dt;
                if (WaveT <= 0)
                {
                    Wave++;
                    WaveT = BetweenWaves;
                    return BrechaRunEvent.SpawnWave;
                }

                return BrechaRunEvent.None;
            }

            Cleared = true;
            if (bossInThemeMap)
            {
                Room = BrechaRooms.Core;
                Cleared = false;
                BossT = BossDelayAltar;
                return BrechaRunEvent.AltarAwakens;
            }

            return BrechaRunEvent.DoorOpened;
        }

        if (Room == BrechaRooms.Core && !BossSpawned && !Cleared)
        {
            BossT -= dt;
            if (BossT <= 0)
            {
                BossSpawned = true;
                return BrechaRunEvent.SpawnBoss;
            }
        }

        return BrechaRunEvent.None;
    }

    /// <summary>Passou pela porta: Câmara do Núcleo, chefe em 1.8 s.</summary>
    public void EnterCore()
    {
        Room = BrechaRooms.Core;
        Cleared = false;
        BossSpawned = false;
        BossT = BossDelayCore;
    }

    /// <summary>Chefe caiu: sala limpa e portal de saída em 1.4 s.</summary>
    public void DefeatBoss()
    {
        Cleared = true;
        BossDefeated = true;
        _exitT = ExitPortalDelay;
    }

    /// <summary>Resumo da bolsa (<c>summaryBag</c>, <c>world.js:129</c>).</summary>
    public static string Summary(RunBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var mats = 0;
        foreach (var n in bag.Materials.Values)
        {
            mats += n;
        }

        var text = $"+{bag.Lumes} Lumes · {mats} materiais · {bag.Gear.Count} equipamentos";
        return bag.Lanterns.Count > 0 ? $"{text} · {bag.Lanterns.Count} lanterna(s)" : text;
    }
}
