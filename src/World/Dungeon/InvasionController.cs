using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Common;

namespace Vespera.World.Dungeon;

/// <summary>
/// Ruptura de Brecha (spec 02 §3.14; <c>rupture</c>/<c>spawnInvasion</c>/<c>resolveInvasion</c>, <c>world.js:49-65,156</c>):
/// a Cidade pede com <c>InvasionRequested</c>; <c>BrechaRules.Invasion(rk)</c> monstros do tema (+ o Alfa) nascem na Rua
/// da Forja (agora, ou quando o jogador chegar lá), escalados pelo rank da Brecha. Limpar todos resolve: +30 Lumes,
/// +30 XP, preços normais e <c>InvasionStateChanged(false)</c>. Sair da rua guarda quantos faltam.
/// </summary>
public sealed class InvasionController(CombatDirector combat, BrechaController brecha)
{
    private string _themeId = string.Empty;
    private string _alpha = string.Empty;
    private int _brechaId;
    private int _remaining;
    private bool _alphaPending;
    private bool _seen;
    private bool _spawned;

    public bool Active { get; private set; }

    public int Rank { get; private set; }

    /// <summary>Monstros que ainda faltam (vivos + nascendo, ou guardados fora da rua).</summary>
    public int Remaining => _remaining + (_alphaPending ? 1 : 0);

    public void OnInvasionRequested(string themeId, int rank)
    {
        if (Active || !combat.Db.Themes.ContainsKey(themeId))
        {
            return;
        }

        var size = combat.State.Brechas.Invasion(rank);
        _themeId = themeId;
        Rank = rank;
        _remaining = size.Mobs;
        _alphaPending = size.WithAlpha;
        _alpha = size.Alpha;
        _brechaId = 0;
        foreach (var open in brecha.OpenBrechas.Values)
        {
            if (open.ThemeId == themeId && open.Rank == rank)
            {
                _brechaId = open.Id;
            }
        }

        combat.PreloadTheme(combat.Db.Themes[themeId]);
        Active = true;
        _seen = false;
        _spawned = false;
        combat.State.Invasion = true;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, true);
        if (combat.World.MapId == World.CityMapId && !combat.World.IsTransitioning)
        {
            Spawn();
        }
    }

    /// <summary>Jogo novo/carregado: a invasão da sessão anterior acaba sem recompensa.</summary>
    public void Reset()
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        _remaining = 0;
        _alphaPending = false;
        combat.State.Invasion = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
    }

    /// <summary>Chegou à Rua da Forja durante a invasão.</summary>
    public void OnForgeLoaded()
    {
        _spawned = false;
        _seen = false;
        if (Active)
        {
            Spawn();
        }
    }

    /// <summary><c>spawnInvasion</c>: a ≥ 110 u do jogador, telegraph de <c>R(0.4, 1.4)</c> s; o Alfa a ≥ 130 u em 1,6 s.</summary>
    private void Spawn()
    {
        var rng = combat.Rng;
        var theme = combat.Db.Themes[_themeId];
        for (var i = 0; i < _remaining; i++)
        {
            var at = combat.RandPoint(110);
            var type = rng.Pick(theme.Mobs).Monster; // pick(im)[0]: sorteio uniforme
            combat.SpawnTelegraphed(type, at, (float)rng.Range(0.4, 1.4), chase: true, invasion: true);
        }

        if (_alphaPending)
        {
            var at = combat.RandPoint(130);
            combat.SpawnTelegraphed(_alpha, at, 1.6f, chase: true, invasion: true);
        }

        _spawned = true;
    }

    public void Step()
    {
        if (!Active || !_spawned || combat.World.MapId != World.CityMapId || combat.World.IsTransitioning)
        {
            return;
        }

        var aliveAlpha = 0;
        var alive = combat.Hazards.PendingInvasionSpawns;
        foreach (var e in combat.Enemies)
        {
            if (e.Invasion && !e.Body.Dead)
            {
                alive++;
                if (e.Body.Def.Id == _alpha)
                {
                    aliveAlpha++;
                }
            }
        }

        if (alive > 0)
        {
            _seen = true;
            _alphaPending = aliveAlpha > 0 || (_alphaPending && combat.Hazards.PendingInvasionSpawns > 0);
            _remaining = alive - (_alphaPending ? 1 : 0);
            return;
        }

        if (_seen)
        {
            Resolve();
        }
    }

    /// <summary><c>resolveInvasion</c>.</summary>
    private void Resolve()
    {
        Active = false;
        _remaining = 0;
        _alphaPending = false;
        combat.State.Invasion = false;
        combat.State.RecordInvasionResolved();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InvasionStateChanged, false);
        combat.Toast("A Rua da Forja respira de novo", "+30 Lumes. Brás reabre a forja; os preços voltam ao normal.");
        brecha.Lantern.ReturnIfHere(_brechaId);
        GameManager.Instance.SaveGame();
    }
}
