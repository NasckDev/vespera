using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Boot;
using Vespera.Game.Coop;
using Vespera.Game.Hunter;
using Vespera.Game.Story;
using Vespera.Game.UI;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Testing;

/// <summary>
/// Bot do co-op local: duas fontes de input simuladas (uma por caçador, cada uma passando pelo comando do jogador) na
/// Praça e na Brecha E. Confere a câmera compartilhada (caçador ≥ 85 px, coleira), a conversa puxada pelo J2, a escala
/// dos monstros (vida ×1,5, +1 por onda, Guardião ×1,5), a queda e a reanimação (segurar 2 s), a Lodo-Mãe, o saque
/// individual e o XP compartilhado, o convidado gravado no save e a queda do grupo inteiro (Lanterna de cada um).
/// Caçadores invulneráveis e dano ×10 (modo do bot de combate) para caber no teste; a queda é forçada.
/// </summary>
public sealed class CoopBot(Level level, SmokeTest host)
{
    private const int RoomFrameLimit = 60 * 150;
    private const int StuckFrames = 75;
    private const float AttackReach = 48f;
    private const float MinHunterPx = 85f;

    private readonly List<string> _failures = [];
    private readonly ScriptedInput[] _inputs = [new(), new()];
    private readonly HashSet<string> _shot = [];
    private readonly HashSet<int> _lootOwnersSeen = [];
    private bool _revived;

    public bool Shots { get; init; }

    private LevelWorld World => level.World;

    private Player P1 => World.Player;

    private Player P2 => World.PlayerAt(1) ?? throw new InvalidOperationException("sem J2 na sala");

    /// <summary>Fonte crua de cada jogador (ligada em <see cref="CoopSession.SourceOverride"/>).</summary>
    public IInputSource SourceFor(CoopMember member) => _inputs[Math.Clamp(member.Slot, 0, 1)];

    public async Task<List<string>> Run()
    {
        await CheckPraca();
        await GuestTalks();
        StoryService.SetFlag(StoryFlags.StarterBlade);
        RiftSession.Cheat = true;
        var p1Xp = Progression.State.Xp;
        var p2Xp = PartyProgression.Guest.Progress.Xp;
        var p1Wallet = Progression.State.Fragmentos;
        var p2Wallet = PartyProgression.Guest.Progress.Fragmentos;
        await EnterTogether();
        foreach (var room in RiftSession.Brecha.Rooms)
        {
            await ClearRoom(room);
            if (_failures.Count > 0)
            {
                break;
            }
        }

        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta à Praça");
        CheckRewards(p1Xp, p2Xp, p1Wallet, p2Wallet);
        await ShotOnce("hud_praca_depois", 0.8);
        if (_failures.Count == 0)
        {
            await WipeAndLantern();
        }

        RiftSession.Cheat = false;
        Release();
        return _failures;
    }

    private async Task CheckPraca()
    {
        Release();
        P1.Teleport(World.SpawnPoint);
        P2.Teleport(World.BesidePoint(World.SpawnPoint, 1));
        World.Camera.Snap();
        await host.PhysicsFrames(10);
        Check(P2.GlobalPosition.DistanceTo(P1.GlobalPosition) < 1.6f * World.Data.HumanHeight, "o J2 deveria nascer ao lado do J1");
        Check(P1.Flash.Outline > 0f && P2.Flash.Outline > 0f && P1.Flash.OutlineColor != P2.Flash.OutlineColor, "cada caçador deveria ter o contorno da cor dele");
        Check(P2.Flash.Cape == 0f, "classes diferentes não trocam a cor da capa");
        Check((P1.CollisionMask & Player.PlayerLayer) == 0 && (P2.CollisionMask & Player.PlayerLayer) == 0, "caçadores não colidem entre si");
        ExpectOnScreen("Praça, lado a lado");
        await ShotOnce("praca_dois", 0.8);

        // Juntos no mesmo ponto: o empurrão leve separa os dois.
        P2.Teleport(P1.GlobalPosition + new Vector2(2f, 0f));
        await host.PhysicsFrames(30);
        Check(P2.GlobalPosition.DistanceTo(P1.GlobalPosition) > 8f * World.Data.ActorScale, "caçadores sobrepostos deveriam se afastar um pouco");

        await Tether();
    }

    /// <summary>J2 anda para longe: a câmera abre só até o piso (≥ 85 px) e a coleira segura o J2 na borda; de longe, ele é puxado de volta.</summary>
    private async Task Tether()
    {
        var span = World.Camera.PartySpan;
        var area = World.PlayArea;
        var h = World.Data.HumanHeight;

        // J1 parado perto da borda esquerda; J2 sai do lado dele e anda para a direita até a coleira segurar.
        var left = World.ClampToFloor(new Vector2(area.Position.X + (2.5f * h), World.SpawnPoint.Y));
        P1.Teleport(left);
        P2.Teleport(World.ClampToFloor(left + new Vector2(span.X * 0.55f, 0f)));
        World.Camera.Snap();
        await host.PhysicsFrames(4);
        _inputs[1].Move = Vector2.Right;
        var zoomFloor = 1f;
        for (var i = 0; i < 60 * 9; i++)
        {
            zoomFloor = Mathf.Min(zoomFloor, World.Camera.Zoom.X / World.Camera.BaseZoom);
            await host.PhysicsFrames(1);
        }

        _inputs[1].Move = Vector2.Zero;
        await host.PhysicsFrames(90);
        var dx = Mathf.Abs(P2.GlobalPosition.X - P1.GlobalPosition.X);
        var factor = World.Camera.Zoom.X / World.Camera.BaseZoom;
        Log.Info($"co-op: J2 andou para longe: {dx:0} px do J1 (limite {span.X:0}), zoom ×{factor:0.000} (mín. visto ×{zoomFloor:0.000}, piso ×{CameraRig.PartyFloor:0.000})");
        Check(dx <= span.X + 6f, $"a coleira deveria segurar o J2 a até {span.X:0} px do J1 (está a {dx:0})");
        Check(dx >= span.X - 40f, $"andando 9 s o J2 deveria chegar ao limite da coleira ({dx:0} de {span.X:0})");
        Check(zoomFloor >= CameraRig.PartyFloor - 0.001f && factor < 0.95f, $"a câmera deveria abrir até o piso, não além (×{factor:0.000}, mín. ×{zoomFloor:0.000})");
        ExpectOnScreen("Praça, J2 no limite da coleira");
        await ShotOnce("praca_separados", 0.1);

        // Teleportado além do limite (recuo, por exemplo): o puxão suave traz de volta.
        var far = World.ClampToFloor(P1.GlobalPosition + new Vector2(Mathf.Min(span.X * 1.3f, area.End.X - P1.GlobalPosition.X - h), 0f));
        P2.Teleport(far);
        await host.PhysicsFrames(2);
        var before = P2.GlobalPosition.DistanceTo(P1.GlobalPosition);
        await host.PhysicsFrames(90);
        var after = P2.GlobalPosition.DistanceTo(P1.GlobalPosition);
        Log.Info($"co-op: J2 além do limite: {before:0} → {after:0} px em 1,5 s");
        Check(before <= span.Length() * 1.05f || after < before - 20f, $"o J2 além do limite deveria ser puxado de volta ({before:0} → {after:0})");
        P2.Teleport(World.BesidePoint(P1.GlobalPosition, 1));
        World.Camera.Snap();
        await host.PhysicsFrames(30);
    }

    /// <summary>O J2 puxa conversa no mural: a caixa diz quem conversa e o botão do J2 avança a fala.</summary>
    private async Task GuestTalks()
    {
        var board = World.Props.First(p => p.Type == "mural");
        P2.Teleport(board.GlobalPosition + new Vector2(0, 50 * World.Data.ActorScale));
        P1.Teleport(World.ClampToFloor(board.GlobalPosition + new Vector2(-1.4f * World.Data.HumanHeight, 1.4f * World.Data.HumanHeight)));
        World.Camera.Snap();
        await host.PhysicsFrames(6);
        Check(P2.Focus?.Kind == "board", $"perto do mural o foco do J2 deveria ser o mural, foi '{P2.Focus?.Kind}'");
        Check(level.Hud.PromptText.StartsWith("J2", StringComparison.Ordinal), $"o aviso deveria ser do J2, foi '{level.Hud.PromptText}'");
        await ShotOnce("aviso_j2", 0.3);
        _inputs[1].Tap(InputActions.Interact);
        await host.PhysicsFrames(4);
        Check(level.DialogueOpen && level.Dialogue.InitiatorText.StartsWith("J2", StringComparison.Ordinal),
            $"o J2 deveria abrir a conversa e a caixa mostrar quem conversa (aberta {level.DialogueOpen}, '{level.Dialogue.InitiatorText}')");
        Check(P1.Frozen && P2.Frozen, "durante a conversa os dois caçadores ficam parados");
        level.Dialogue.Complete();
        await ShotOnce("conversa_j2", 0.4);
        for (var i = 0; i < 40 && level.DialogueOpen; i++)
        {
            _inputs[1].Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
        }

        Check(!level.DialogueOpen, "o botão do J2 deveria avançar e fechar a conversa");
        await host.PhysicsFrames(30);
        Check(!P1.Frozen && !P2.Frozen, "ao fechar a conversa os dois voltam a andar");
    }

    private async Task EnterTogether()
    {
        var portal = World.Zones.First(z => z.Kind == "brecha");
        P1.Teleport(portal.GlobalPosition + new Vector2(0, 40f * World.Data.ActorScale));
        P2.Teleport(World.BesidePoint(P1.GlobalPosition, 1));
        await host.PhysicsFrames(4);
        _inputs[0].Tap(InputActions.Interact);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "primeira sala em grupo");
        Check(World.Players.Count == 2, $"os dois deveriam entrar na Brecha ({World.Players.Count})");
        Check(level.Players.Count == 2 && P2.Hunter == HunterClass.Arco && P1.Hunter == RiftSession.Hunter, "cada um entra com a classe que escolheu");
    }

    private async Task ClearRoom(string room)
    {
        await WaitUntil(() => !level.Switching && World.Data.Id == room && World.Director is not null, 600, $"sala {room}");
        var director = World.Director!;
        var frames = 0;
        var stuck = new int[2];
        var last = new[] { float.MaxValue, float.MaxValue };
        var scaleChecked = false;
        while (!director.Cleared && frames++ < RoomFrameLimit)
        {
            if (!scaleChecked && (director.LastWaveSize > 0 || director.Boss is not null))
            {
                scaleChecked = true;
                CheckScaling(director);
            }

            if (!_revived && room == RiftSession.Brecha.Rooms[1] && director.Alive.Any() && frames > 60)
            {
                await DownAndRevive();
            }

            for (var slot = 0; slot < 2; slot++)
            {
                Drive(slot, director, frames, stuck, last);
            }

            NoteLoot();
            await ShotMoments(room, frames, director);
            await host.PhysicsFrames(1);
        }

        Release();
        Check(director.Cleared, $"sala {room} não ficou limpa em {RoomFrameLimit / 60} s");
        await CollectAndLeave(director);
    }

    private void Drive(int slot, RoomDirector director, int frames, int[] stuck, float[] last)
    {
        var player = World.PlayerAt(slot);
        var input = _inputs[slot];
        if (player is null || player.Combat.IsDead)
        {
            input.Move = Vector2.Zero;
            return;
        }

        var target = director.Alive.OrderBy(e => e.GlobalPosition.DistanceTo(player.GlobalPosition)).FirstOrDefault();
        if (target is null)
        {
            input.Move = Vector2.Zero;
            return;
        }

        var s = World.Data.ActorScale;
        var to = target.GlobalPosition - player.GlobalPosition;
        var distance = to.Length();
        stuck[slot] = distance < last[slot] - 0.5f || distance < AttackReach * s ? 0 : stuck[slot] + 1;
        last[slot] = distance;
        if (stuck[slot] > StuckFrames)
        {
            player.Teleport(World.ClampToFloor(target.GlobalPosition + (Vector2.Down * 30f * s) + (Vector2.Right * (slot == 0 ? -20f : 20f) * s)));
            stuck[slot] = 0;
        }

        // O Arco ataca de longe; a Lâmina/Martelo chegam perto.
        var reach = player.Hunter == HunterClass.Arco ? 3.2f * World.Data.HumanHeight : AttackReach * s * 0.8f;
        input.Move = distance > reach ? to.Normalized() : Vector2.Zero;
        if (distance <= reach * 1.6f + (target.HitRadius * 1.6f) && (frames + (slot * 4)) % 8 == 0)
        {
            if (distance <= reach)
            {
                player.Face(to);
            }

            input.Tap(frames % 96 == 0 ? InputActions.Heavy : InputActions.Attack);
        }
    }

    /// <summary>Vida ×1,5, +1 monstro por onda e Guardião ×1,5 com dois jogadores (data/coop.json).</summary>
    private void CheckScaling(RoomDirector director)
    {
        var rules = CoopSession.Rules;
        if (director.LastWaveSize > 0)
        {
            Check(director.LastWaveSize == director.BaseWaveSize(0) + rules.ExtraEnemiesPerWavePerPlayer,
                $"a onda deveria ter +1 monstro com 2 jogadores ({director.LastWaveSize} vs base {director.BaseWaveSize(0)})");
        }

        if (director.Boss is { } boss)
        {
            Check(Math.Abs(boss.MaxHp - (boss.Stats.Hp * rules.EnemyHp(2))) < 0.01, $"a Lodo-Mãe deveria ter vida ×1,5 ({boss.MaxHp} vs {boss.Stats.Hp})");
        }

        Log.Info($"co-op: sala {World.Data.Id}: onda com {director.LastWaveSize} (base {director.BaseWaveSize(0)})");
    }

    private void CheckEnemyHp()
    {
        foreach (var enemy in World.Enemies.Where(e => GodotObject.IsInstanceValid(e) && e is not LodoMae && e.DropsLoot))
        {
            var expected = enemy.Stats.Hp * CoopSession.Rules.EnemyHp(2);
            if (Math.Abs(enemy.MaxHp - expected) > 0.01)
            {
                _failures.Add($"monstro {enemy.MonsterId} com vida {enemy.MaxHp}, esperado {expected} (×1,5)");
            }
        }
    }

    /// <summary>J2 cai no meio da luta (forçado), fica caído sem a caçada acabar, e o J1 segura interagir por 2 s ao lado dele.</summary>
    private async Task DownAndRevive()
    {
        _revived = true;
        CheckEnemyHp();
        var p2 = P2;
        p2.Combat.Digest(99999);
        await host.PhysicsFrames(20);
        Check(p2.Combat.IsDowned, $"no co-op o J2 deveria ficar caído, está {p2.Combat.State}");
        Check(RiftSession.InRift && !level.Switching, "um caído não termina a caçada");
        Check(World.Director!.Alive.All(e => World.NearestHunter(e.GlobalPosition) == P1), "os monstros deveriam mirar só o J1 de pé");
        await ShotOnce("caido", 0.3);

        // J1 vai até ele e segura interagir.
        _inputs[0].Move = Vector2.Zero;
        P1.Teleport(World.ClampToFloor(p2.GlobalPosition + new Vector2(0.5f * World.Data.HumanHeight, 0.1f * World.Data.HumanHeight)));
        await host.PhysicsFrames(2);
        _inputs[0].Hold(InputActions.Interact, true);
        await host.PhysicsFrames(60);
        Check(World.Party.ReviveFraction(p2) is > 0.35f and < 0.75f, $"depois de 1 s segurando, o anel deveria estar na metade ({World.Party.ReviveFraction(p2):0.00})");
        await ShotOnce("reanimando", 0);
        await host.PhysicsFrames(75);
        _inputs[0].Hold(InputActions.Interact, false);
        Check(!p2.Combat.IsDead && p2.Combat.Hp == CoopSession.Rules.RevivedHp(p2.Combat.MaxHp),
            $"segurar 2 s deveria reanimar o J2 com {CoopSession.Rules.RevivedHp(p2.Combat.MaxHp)} de vida (estado {p2.Combat.State}, vida {p2.Combat.Hp})");
        Log.Info($"co-op: J2 caiu e foi reanimado pelo J1 com {p2.Combat.Hp}/{p2.Combat.MaxHp}");
        await ShotOnce("reanimado", 0.3);
    }

    private async Task CollectAndLeave(RoomDirector director)
    {
        var s = World.Data.ActorScale;
        if (director.Chest is { } chest)
        {
            P1.Teleport(World.ClampToFloor(chest.GlobalPosition + (Vector2.Down * 40f * s)));
            P2.Teleport(World.BesidePoint(P1.GlobalPosition, 1));
            await host.PhysicsFrames(4);
            _inputs[0].Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
            Check(chest.IsOpen, "o J1 deveria abrir o baú");
            await ShotOnce("bau_saque", 0.25);
        }

        for (var i = 0; i < 90; i++)
        {
            NoteLoot();
            await host.PhysicsFrames(1);
        }

        if (director.ExitPortal is null && World.Data.Rift?.ExitPortal is not null)
        {
            await WaitUntil(() => director.ExitPortal is not null, 400, "fenda de saída");
        }

        await WaitUntil(() => RankUpScreen.Active is null && !host.GetTree().Paused, 400, "celebração de rank fechada");
        if (director.ExitPortal is { } portal)
        {
            P1.Teleport(World.ClampToFloor(portal.GlobalPosition + (Vector2.Down * 40f * s)));
            await host.PhysicsFrames(6);
            _inputs[0].Tap(InputActions.Interact);
            await host.PhysicsFrames(6);
            return;
        }

        if (World.Data.Rift?.Exit is { } exit)
        {
            // Basta o J2 chegar na passagem para levar o grupo (o J1 vem junto até perto, como numa caminhada de verdade).
            var t = exit.Trigger;
            var gate = new Vector2(t[0] + (t[2] / 2f), t[1] + (t[3] / 2f));
            P1.Teleport(World.ClampToFloor(gate + new Vector2(-1.2f * World.Data.HumanHeight, 0f)));
            P2.Teleport(gate);
            await host.PhysicsFrames(4);
        }
    }

    private async Task ShotMoments(string room, int frames, RoomDirector director)
    {
        if (!Shots)
        {
            return;
        }

        if (room == RiftSession.Brecha.Rooms[0] && frames == 100)
        {
            await ShotOnce("corredor_luta", 0);
        }

        if (director.Boss is { Engaged: true } && frames == 300)
        {
            ExpectOnScreen("arena com a Lodo-Mãe");
            await ShotOnce("chefe_dois", 0);
        }

        if (director.Boss is { Engaged: true } && frames == 520)
        {
            await ShotOnce("chefe_dois_b", 0);
        }
    }

    private void NoteLoot()
    {
        foreach (var node in World.Entities.GetChildren())
        {
            if (node is FragmentPickup pickup)
            {
                _lootOwnersSeen.Add(pickup.OwnerSlot);
            }
        }
    }

    private void CheckRewards(int p1Xp, int p2Xp, int p1Wallet, int p2Wallet)
    {
        var guest = PartyProgression.Guest;
        var gained1 = Progression.State.Xp - p1Xp;
        var gained2 = guest.Progress.Xp - p2Xp;
        var wallet1 = Progression.State.Fragmentos - p1Wallet;
        var wallet2 = guest.Progress.Fragmentos - p2Wallet;
        Log.Info($"co-op: XP J1 +{gained1}, J2 +{gained2}; carteira J1 +{wallet1}, J2 +{wallet2}; ranks J1 {Progression.Rank}, J2 {guest.Rank}; " +
            $"materiais J2 {guest.Progress.Materials.Values.Sum()}; donos de cristais vistos [{string.Join(",", _lootOwnersSeen)}]");
        Check(gained1 > 0 && gained1 == gained2, $"XP compartilhado: cada um deveria ganhar o valor cheio (J1 +{gained1}, J2 +{gained2})");
        Check(wallet1 > 0 && wallet2 > 0, $"os dois deveriam levar Fragmentos para casa (J1 +{wallet1}, J2 +{wallet2})");
        Check(_lootOwnersSeen.Contains(0) && _lootOwnersSeen.Contains(1), "deveriam cair cristais dos dois donos (saque individual)");
        Check(guest.Progress.GuardiansDefeated.Contains(RiftSession.Brecha.GuardianId), "a Lodo-Mãe deveria contar para o J2 também");
        Check(!guest.InRun && !Progression.Engine.InRun, "a caçada dos dois deveria ter terminado");
        Check(World.Players.Count == 2 && World.Players.All(p => !p.Combat.IsDead), "os dois deveriam voltar de pé à Praça");

        var saved = SaveService.Load(GameSession.Slot);
        Check(saved?.Guest is { } g && g.Progress.Xp == guest.Progress.Xp && g.Progress.Fragmentos == guest.Progress.Fragmentos && g.ClassId == "bow",
            $"o convidado deveria estar gravado no mesmo espaço (XP salvo {saved?.Guest?.Progress.Xp}, atual {guest.Progress.Xp})");
        Check(saved?.Story.Flags.Contains(StoryFlags.GuardianDefeatedLodoMae) == true, "a história (do J1) deveria registrar a Lodo-Mãe");
    }

    /// <summary>Os dois caem: uma caçada só, os Fragmentos de cada um ficam na Lanterna dele e o grupo acorda na Praça.</summary>
    private async Task WipeAndLantern()
    {
        RiftSession.Cheat = false;
        level.EnterRift(CoopSession.Members[0].Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "sala para a queda do grupo");
        var guest = PartyProgression.Guest;
        for (var i = 0; i < 6 && (Progression.Engine.RunFragmentos == 0 || guest.RunFragmentos == 0); i++)
        {
            PartyProgression.GrantLoot(RiftSession.BreachRank, LootSource.Chest);
        }

        var held1 = Progression.Engine.RunFragmentos;
        var held2 = guest.RunFragmentos;
        var wallet1 = Progression.State.Fragmentos;
        var wallet2 = guest.Progress.Fragmentos;
        Check(held1 > 0 && held2 > 0, $"cada um deveria ter Fragmentos da caçada ({held1}, {held2})");
        P1.Combat.Digest(99999);
        await host.PhysicsFrames(30);
        Check(RiftSession.InRift && P1.Combat.IsDowned, "com o J2 de pé, a queda do J1 não termina a caçada");
        P2.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, "volta à Praça depois da queda do grupo");
        var hood = RiftSession.Brecha.NeighborhoodId;
        Check(Progression.Engine.LanternFragmentos(hood) == held1 && guest.LanternFragmentos(hood) == held2,
            $"cada Lanterna deveria guardar os Fragmentos do dono (J1 {Progression.Engine.LanternFragmentos(hood)}/{held1}, J2 {guest.LanternFragmentos(hood)}/{held2})");
        Check(Progression.State.Fragmentos == wallet1 && guest.Progress.Fragmentos == wallet2, "na queda nada entra na carteira");
        Check(World.Players.Count == 2 && World.Players.All(p => !p.Combat.IsDead && p.Combat.Hp == p.Combat.MaxHp), "os dois deveriam acordar inteiros na Praça");
        Check(level.CombatHud.LastToast.Contains("Lanterna", StringComparison.Ordinal) || level.CombatHud.LastToast.Contains("caiu", StringComparison.Ordinal),
            $"a queda do grupo deveria ser avisada (último aviso: '{level.CombatHud.LastToast}')");
        Log.Info($"co-op: o grupo caiu; Lanternas J1 {held1} e J2 {held2} Fragmentos no bairro {hood}");

        // Na próxima entrada cada um recupera a própria Lanterna.
        level.EnterRift(CoopSession.Members[0].Hunter);
        await WaitUntil(() => !level.Switching && World.Director is not null, 600, "reentrada em grupo");
        Check(Progression.Engine.RunFragmentos >= held1 && guest.RunFragmentos >= held2 && RiftSession.GuestRecovered == held2,
            "entrar de novo deveria devolver a Lanterna de cada um");
        RiftSession.Cheat = true;
        var exitRoom = World.Data.Id;
        P1.Combat.Digest(99999);
        P2.Combat.Digest(99999);
        await WaitUntil(() => !level.Switching && World.Data.Id == Level.HubLevelId && !RiftSession.InRift, 900, $"volta final (de {exitRoom})");
        foreach (var p in World.Players)
        {
            p.Combat.RefreshStats(fullHeal: true);
        }
    }

    private void ExpectOnScreen(string where)
    {
        var view = new Rect2(Vector2.Zero, new Vector2(1920, 1080));
        foreach (var p in World.Players)
        {
            var h = CameraCheck.OnScreenHeight(level, p);
            var feet = level.WorldToScreen(p.GlobalPosition);
            Log.Info($"câmera co-op {where}: {Coop.CoopSession.TagOf(p.Slot)} com {h:0.0} px na tela, pé em {feet.Round()}");
            Check(h >= MinHunterPx, $"câmera co-op ({where}): {Coop.CoopSession.TagOf(p.Slot)} com {h:0.0} px (mín. {MinHunterPx})");
            Check(view.Grow(4f).HasPoint(feet), $"câmera co-op ({where}): {Coop.CoopSession.TagOf(p.Slot)} fora da tela (pé em {feet.Round()})");
        }
    }

    private async Task ShotOnce(string name, double settle)
    {
        if (!Shots || !_shot.Add(name))
        {
            return;
        }

        if (settle > 0)
        {
            await host.PhysicsFrames((int)(settle * 60));
        }

        await host.CaptureNow($"coop_{name}");
    }

    private void Release()
    {
        foreach (var input in _inputs)
        {
            input.Move = Vector2.Zero;
            input.Hold(InputActions.Interact, false);
        }
    }

    private async Task WaitUntil(Func<bool> condition, int frames, string what)
    {
        for (var i = 0; i < frames && !condition(); i++)
        {
            if (RankUpScreen.Active is { Revealed: true } celebration)
            {
                await ShotOnce($"rank_{celebration.Change.To}", 0);
                celebration.Close();
            }

            await host.PhysicsFrames(1);
        }

        Check(condition(), $"bot do co-op: tempo esgotado esperando {what} (sala {World.Data.Id}, trocando {level.Switching}, na Brecha {RiftSession.InRift}, " +
            $"pausado {host.GetTree().Paused}, caçadores {World.Players.Count}, estados [{string.Join(",", World.Players.Select(p => p.Combat.State))}])");
    }

    private void Check(bool ok, string failure)
    {
        if (!ok)
        {
            _failures.Add(failure);
        }
    }
}
