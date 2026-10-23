using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Story;
using Vespera.Core.Advancement;
using Vespera.Game.Hunter;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.World;
using Vespera.Game.UI;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot de combate (headless, <c>--combat-bot</c>): na Praça abre o portal, escolhe a arma, atravessa as quatro salas
/// da Brecha E matando tudo com golpes de verdade (caçador invulnerável e dano ×10 para caber no teste), abre o baú,
/// derrota a Lodo-Mãe e volta pela fenda. Depois cai de propósito para provar a Lanterna (bolsa fica na Brecha) e a
/// recupera. Falha se algo travar ou se o save não receber o saque.
/// </summary>
public sealed partial class CombatBot(Level level, SmokeTest host)
{
    private const int RoomFrameLimit = 60 * 120;
    private const int StuckFrames = 75;
    private const float AttackReach = 48f;

    private readonly List<string> _failures = [];

    public IReadOnlyList<string> Failures => _failures;
    private readonly ScriptedInput _input = new();

    /// <summary>Ranks celebrados durante a caçada (a tela de rank abre sozinha depois do Guardião).</summary>
    public List<Vespera.Core.Ranks.Rank> RankUps { get; } = [];

    /// <summary>Fotografa a caçada (QA visual): cada sala, golpe com pausa no impacto, avisos e a celebração de rank.</summary>
    public bool Shots { get; init; }

    /// <summary>Prefixo dos nomes das capturas.</summary>
    public string ShotPrefix { get; init; } = "caca";

    private readonly HashSet<string> _shot = [];

    private async Task ShotOnce(string name)
    {
        if (Shots && _shot.Add(name))
        {
            await host.CaptureNow($"{ShotPrefix}_{name}");
        }
    }

    /// <summary>Capturas de momentos da luta (uma vez cada): golpe acertando (hit-stop), avisos âmbar dos monstros.</summary>
    private async Task ShotMoments(string room, int frames)
    {
        if (!Shots)
        {
            return;
        }

        if (frames == (room == RiftSession.Brecha.Rooms[^1] ? 240 : 90))
        {
            var at = level.WorldToScreen(World.Player.GlobalPosition);
            Log.Info($"qa-pos {ShotPrefix}_{room}_luta {at.X:0} {at.Y:0} {CameraCheck.OnScreenHeight(level):0.0}");
            await ShotOnce($"{room}_luta");
        }

        if (Engine.TimeScale < 1.0 && !_shot.Contains("golpe_hitstop_a"))
        {
            await ShotOnce("golpe_hitstop_a");
            await host.PhysicsFrames(2);
            await ShotOnce("golpe_hitstop_b");
        }

        foreach (var enemy in World.Enemies.ToList())
        {
            if (GodotObject.IsInstanceValid(enemy) && enemy.State == EnemyState.Telegraph
                && (enemy is LodoMae || enemy.GlobalPosition.DistanceTo(World.Player.GlobalPosition) > 0.9f * World.Data.HumanHeight))
            {
                await ShotOnce(enemy switch { LodoMae => "lodo_mae_aviso", Espectro => "espectro_aviso", _ => "lodo_aviso" });
            }
        }
    }

    private LevelWorld World => level.World;

    private float S => World.Data.ActorScale;

    /// <param name="hunter">Arma da caçada; null = a que o seletor do portal oferece (a equipada).</param>
    /// <param name="lanternTest">Depois da caçada, cai de propósito para provar a Lanterna.</param>
    public async Task<List<string>> Run(HunterClass? hunter, bool lanternTest = true)
    {
        var save = RiftSession.Save;
        var shardsBefore = RiftSession.WalletFragments;
        var guardiansBefore = Progression.State.GuardiansDefeated.Count;
        var closedBefore = save.BrechasClosed;
        RiftSession.Cheat = true;

        // O teste isolado da Brecha quer a Lodo-Mãe no fim: abre o portão do exame do Benício.
        StoryService.SetFlag(StoryFlags.GuardianGate);
        await OpenPortalAndEnter(hunter);
        var metrics = await PlayRooms();
        var gained = RiftSession.WalletFragments - shardsBefore;
        Check(!RiftSession.InRift && !Progression.Engine.InRun, "depois do portal de saída a caçada deveria ter acabado");
        Check(save.BrechasClosed == closedBefore + 1, $"Brecha deveria contar como fechada ({save.BrechasClosed})");
        Check(gained > 0, $"a carteira deveria receber Fragmentos (ganhou {gained})");
        Check(Progression.State.GuardiansDefeated.Contains(RiftSession.Brecha.GuardianId), "a progressão deveria registrar a Lodo-Mãe");
        Check(Progression.State.GuardiansDefeated.Count >= guardiansBefore, "conquistas não podem sumir");
        Log.Info($"bot de combate: rank {Progression.Rank} com {Progression.State.Xp} XP; celebrações: [{string.Join(",", RankUps)}]");
        Log.Info($"bot de combate: {hunter?.ToString() ?? "arma equipada"} limpou {metrics.Rooms} salas, {metrics.Kills} monstros, +{gained} Fragmentos");
        if (_failures.Count == 0 && lanternTest)
        {
            await DieAndRecoverLantern();
        }

        RiftSession.Cheat = false;
        return _failures;
    }

    private async Task OpenPortalAndEnter(HunterClass? hunter)
    {
        var portal = World.Zones.First(z => z.Kind == "brecha");
        Bind();
        World.Player.Teleport(portal.GlobalPosition + new Vector2(0, 40f * S));
        await host.PhysicsFrames(4);
        Check(World.Player.Focus?.Kind == "brecha", $"perto do portal o foco deveria ser a Brecha, foi '{World.Player.Focus?.Kind}'");
        if (!StoryService.Engine.HasFlag(StoryFlags.StarterBlade))
        {
            // Sem a Lâmina da Odete a fenda não deixa entrar (o bot de combate testa a Brecha isolada: arma o caçador).
            await TapInteract();
            await host.PhysicsFrames(4);
            Check(!RiftSession.InRift && level.Hud.PanelOpen, "sem a Lâmina da Odete o portal deveria recusar com um aviso");
            await TapInteract();
            await host.PhysicsFrames(4);
            StoryService.SetFlag(StoryFlags.StarterBlade);
        }

        _input.Tap(InputActions.Interact);
        await host.PhysicsFrames(4);
        var equipped = RiftSession.HunterFor(Progression.Engine.ClassId);
        Check(RiftSession.InRift, "interagir com a Brecha deveria entrar direto, sem perguntar a classe");
        Check(RiftSession.Hunter == equipped, $"a Brecha deveria usar a arma equipada ({equipped}), usou {RiftSession.Hunter}");
        if (hunter is { } other && other != equipped)
        {
            level.EnterRift(other);
        }
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "primeira sala");
        Check(World.Data.Id == RiftSession.CurrentRoom, $"primeira sala deveria ser {RiftSession.CurrentRoom} (da caçada sorteada), foi {World.Data.Id}");
    }

    private async Task CollectAndLeave(RoomDirector director)
    {
        await RestAndBreak(director);
        if (director.Chest is { } chest)
        {
            World.Player.Teleport(World.ClampToFloor(chest.GlobalPosition + (Vector2.Down * 40f * S)));
            await host.PhysicsFrames(4);
            await TapInteract();
            await host.PhysicsFrames(6);
            Check(chest.IsOpen, "interagir com o baú deveria abri-lo");
        }

        await host.PhysicsFrames(90);
        if (director.ExitPortal is null && World.Data.Rift?.ExitPortal is not null)
        {
            var waited = 0;
            while (director.ExitPortal is null && waited < 1200)
            {
                await WaitUntil(() => director.ExitPortal is not null, 60, string.Empty, quiet: true);
                waited += 60;
            }

            Log.Info($"bot: fenda de saída {(director.ExitPortal is null ? "NÃO abriu" : "abriu")} depois de ~{waited + 90} quadros (pausado {host.GetTree().Paused})");
            Check(director.ExitPortal is not null, "bot de combate: a fenda de saída não abriu");
        }

        await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 400, "celebração de rank fechada");
        if (director.ExitPortal is { } portal)
        {
            World.Player.Teleport(World.ClampToFloor(portal.GlobalPosition + (Vector2.Down * 40f * S)));
            await host.PhysicsFrames(6);
            var room = RiftSession.RoomIndex;
            for (var attempt = 0; attempt < 5 && !level.Switching && RiftSession.InRift && RiftSession.RoomIndex == room; attempt++)
            {
                // Como um jogador: se o aperto não pegou (aviso na tela, golpe acabando), aperta de novo.
                if (attempt > 0)
                {
                    Log.Info($"bot: Interagir na fenda não pegou (tentativa {attempt + 1}); foco '{World.Player.Focus?.Kind}', painel {level.Hud.PanelOpen}");
                }

                await TapInteract();
                await host.PhysicsFrames(20);
            }

            return;
        }

        if (World.Data.Rift?.Exit is { } exit)
        {
            var t = exit.Trigger;
            World.Player.Teleport(new Vector2(t[0] + (t[2] / 2f), t[1] + (t[3] / 2f)));
            await host.PhysicsFrames(4);
        }
    }

    /// <summary>
    /// Lanterna (regra da progressão): cai com Fragmentos da caçada, volta à Praça sem eles (ficam na Lanterna do bairro),
    /// entra de novo e eles voltam à caçada logo na entrada; sair vivo leva tudo para a carteira.
    /// </summary>
    private async Task DieAndRecoverLantern()
    {
        RiftSession.Cheat = false;
        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "sala para a queda");
        Bind();
        Progression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        var held = RiftSession.Fragments;
        var wallet = RiftSession.WalletFragments;
        Check(held > 0, "o saque dentro da Brecha deveria ficar na caçada");
        World.Player.Teleport(World.ClampToFloor(World.SpawnPoint + (new Vector2(260f, 0f) * S)));
        await host.PhysicsFrames(2);
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta à Praça depois da queda");
        await CloseResults();
        Check(RiftSession.LanternFragments == held, $"a queda deveria deixar {held} Fragmentos na Lanterna (tem {RiftSession.LanternFragments})");
        Check(RiftSession.WalletFragments == wallet, "os Fragmentos da caçada não deveriam entrar na carteira na queda");
        Check(!World.Player.Combat.IsDead && World.Player.Combat.Hp > 0, "o caçador deveria acordar vivo na Praça");

        level.EnterRift(World.Player.Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "reentrada na Brecha");
        Bind();
        Check(RiftSession.LanternFragments == 0 && RiftSession.Fragments >= held, "entrar de novo deveria devolver os Fragmentos da Lanterna à caçada");
        Check(level.CombatHud.LastToast.Contains("Lanterna recuperada", System.StringComparison.Ordinal), $"a volta da Lanterna deveria ser avisada (último aviso: '{level.CombatHud.LastToast}')");

        RiftSession.Cheat = true;
        World.Player.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta final");
        await CloseResults();
        World.Player.Combat.RefreshStats(fullHeal: true);
        Log.Info($"bot de combate: {held} Fragmentos ficaram na Lanterna na queda e voltaram na reentrada");
    }

    private void Bind()
    {
        World.Player.Input = _input;
        _input.Move = Vector2.Zero;
    }

    private async Task WaitUntil(Func<bool> condition, int frames, string what, bool quiet = false)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            if (RankUpScreen.Active is { Revealed: true } celebration)
            {
                // O Guardião sobe o rank (E -> D): a celebração pausa o jogo; o bot a registra e fecha.
                RankUps.Add(celebration.Change.To);
                await ShotOnce($"rank_{celebration.Change.To}");
                celebration.Close();
            }

            await host.PhysicsFrames(1);
        }

        Check(quiet || condition(), $"bot de combate: tempo esgotado esperando {what} (sala {World.Data.Id}, trocando {level.Switching}, " +
            $"na Brecha {RiftSession.InRift}, pausado {host.GetTree().Paused}, celebração {RankUpScreen.Active is not null}, " +
            $"tela {SceneRouter.Instance.Current?.Name}, foco '{World.Player.Focus?.Kind}', congelado {World.Player.Frozen}, estado {World.Player.Combat.State}, golpe ocupado {World.Player.Combat.Moves.Busy}, anim {World.Player.CurrentAnimation})");
    }

    private void Check(bool ok, string failure)
    {
        if (!ok)
        {
            _failures.Add(failure);
        }
    }
}
