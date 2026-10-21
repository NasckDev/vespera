using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Common;
using Vespera.Core.Save;

namespace Vespera.World.Dungeon;

/// <summary>
/// Morte e Lanterna (spec 02 §3.13; <c>respawnAfterDeath</c>, <c>rescueLantern</c>, <c>combat.js:25-41</c>,
/// <c>world.js:66-72</c>). <c>PlayerDied</c> sai do GameManager quando o HP zera; a Interface mostra a tela de morte e
/// emite <c>RespawnRequested</c>; aqui se aplica <c>DieInBrecha</c> (a bolsa fica na Lanterna, na posição da morte) ou
/// <c>DieInCity</c> e o jogador volta à Guilda/estalagem com 1,5 s de i-frames. A Lanterna volta ao ser pega na próxima
/// Brecha do tema (D40), quando um caçador a traz (<c>R(55, 85)</c> s; 20 s ao carregar um save; 12 s pagando a Odete
/// com <see cref="ScheduleRescue"/>) ou quando a Brecha dela fecha.
/// </summary>
public sealed class LanternKeeper(CombatDirector combat, BrechaController brecha)
{
    private static readonly Vector2 GuildRespawn = new(250, 330); // combat.js:35
    private static readonly Vector2 InnRespawn = new(250, 520); // combat.js:38

    private double _clock;
    private double _rescueAt = -1;

    /// <summary>Segundos até um caçador trazer a Lanterna (−1 = nenhum resgate marcado).</summary>
    public double RescueIn => _rescueAt < 0 ? -1 : _rescueAt - _clock;

    /// <summary>Marca o resgate para daqui a <paramref name="seconds"/> s (a Odete: <c>LanternRules.OdeteRescueDelay</c>).</summary>
    public void ScheduleRescue(double seconds) => _rescueAt = _clock + seconds;

    public void Step(float realDt)
    {
        _clock += realDt;
        if (_rescueAt >= 0 && _clock >= _rescueAt)
        {
            _rescueAt = -1;
            if (combat.State.HasLostLantern)
            {
                Rescue(RandomHunter());
            }
        }
    }

    public void OnGameLoaded()
    {
        _rescueAt = combat.State.HasLostLantern ? _clock + combat.State.LanternRules.RescueOnLoadDelay : -1;
    }

    /// <summary><c>respawnAfterDeath</c>.</summary>
    public void OnRespawnRequested()
    {
        if (!combat.PlayerDead)
        {
            return;
        }

        var state = combat.State;
        var world = combat.World;
        if (brecha.Run is { } run)
        {
            var foot = combat.PlayerFoot;
            state.DieInBrecha(new LostLantern
            {
                BrechaId = run.BrechaId,
                Bairro = run.Theme.Bairro,
                ThemeId = run.Theme.Id,
                Bag = run.Bag,
                MapId = world.MapId,
                Room = run.Room,
                X = foot.X,
                Y = foot.Y,
            });
            _rescueAt = _clock + state.LanternRules.RollRescueDelay(combat.Rng);
            brecha.EndRun(cleared: false);
            combat.PlayerCombat.Revive();
            brecha.ToastOnArrival("Sua Lanterna ficou na Brecha", "Vida máxima reduzida até alguém trazê-la de volta.");
            world.GoMap("guild", GuildRespawn);
        }
        else
        {
            state.DieInCity();
            combat.PlayerCombat.Revive();
            brecha.ToastOnArrival("Lúcia te arrastou até a estalagem", "Você perdeu 10 Lumes no tumulto.");
            world.GoMap("inn", InnRespawn);
        }

        GameManager.Instance.SaveGame();
    }

    /// <summary>Pegou a própria Lanterna na Brecha ("Recuperar sua Lanterna").</summary>
    public void RecoverHere()
    {
        if (!combat.State.RecoverLantern())
        {
            return;
        }

        _rescueAt = -1;
        combat.Sfx("eco", combat.PlayerFoot);
        combat.Toast("Sua Lanterna voltou a acender", "Os itens que estavam nela voltaram para você.");
        GameManager.Instance.SaveGame();
    }

    /// <summary><c>returnLostIfHere</c>: a Brecha da Lanterna fechou (chefe, invasão resolvida ou uma Casa).</summary>
    public void ReturnIfHere(int brechaId)
    {
        if (brechaId > 0 && combat.State.Save.LostLantern is { } lost && lost.BrechaId == brechaId)
        {
            Rescue(RandomHunter());
        }
    }

    /// <summary><c>rescueLantern(who)</c>.</summary>
    private void Rescue(string who)
    {
        if (combat.State.Save.LostLantern is not { } lost)
        {
            return;
        }

        var bairro = lost.Bairro;
        combat.State.RecoverLantern();
        _rescueAt = -1;
        combat.Toast($"{who} trouxe sua Lanterna de volta", $"Ela estava no {bairro}. Os itens guardados nela voltaram para você.");
        combat.Sfx("eco", combat.PlayerFoot);
        GameManager.Instance.SaveGame();
    }

    private string RandomHunter() => combat.Rng.Pick(combat.Db.Social.Hunters).Name;
}
