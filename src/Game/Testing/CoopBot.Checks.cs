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

/// <summary>Bot do co-op, parte 2: conferências de escala, recompensas, câmera e a queda do grupo inteiro.</summary>
public sealed partial class CoopBot
{
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

    /// <summary>Vida ×1,5, +1 monstro por onda e Guardião ×1,5 com dois jogadores (data/coop.json).</summary>
    private void CheckScaling(RoomDirector director)
    {
        var rules = CoopSession.Rules;
        if (director.Boss is not null)
        {
            // Apresentação da Lodo-Mãe com os dois na arena: a câmera compartilhada ainda deixa os dois com ≥ 85 px.
            ExpectOnScreen("arena, apresentação da Lodo-Mãe");
        }

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
}
