using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Art;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Enemies;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Cenários da arte aprovada do designer (frente C2, DECISIONS C2-D1 a C2-D7): andar desenhado das 5 classes, esquiva no
/// tempo do dash, chefes trocando de pose pela IA, objetos com estados (barril, baú, lampião, flor, alavanca, porta,
/// barco, vara), bustos no diálogo e ícones do HUD. Prints <c>arte-*.png</c> com <c>--smoke-shots</c>.
/// </summary>
public partial class SmokeTest
{
    private const string ArtFixture = "res://src/Testing/Fixtures/fixture_art.json";
    private static readonly string[] ArtClasses = ["sword", "bow", "staff", "hammer", "dag"];

    [SmokeScenario("arte: andar desenhado das 5 classes, 9 fps por vista, sem quique por cima", order: 140)]
    private async Task ArtWalkAllClasses()
    {
        var (world, _) = await CombatSetup();
        foreach (var cls in ArtClasses)
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            await WaitSeconds(0.2);
            Require(anim.HasWalkArt, $"{cls}: sem SpriteFrames da arte aprovada ({Player.PlayerAnimator.WalkFramesPath(cls)})");
            foreach (var (action, expected, back) in new[]
                     {
                         (InputActions.MoveRight, "walk_side", InputActions.MoveLeft),
                         (InputActions.MoveDown, "walk_front", InputActions.MoveUp),
                         (InputActions.MoveUp, "walk_back", InputActions.MoveDown),
                     })
            {
                var changes = anim.WalkFrameChanges;
                var frames = new HashSet<int>();
                var maxBob = 0f;
                var t0 = Time.GetTicksMsec();
                Input.ActionPress(action);
                for (var i = 0; (Time.GetTicksMsec() - t0) < 800; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (anim.CurrentAnimation == expected)
                    {
                        frames.Add(anim.CurrentFrame);
                        maxBob = MathF.Max(maxBob, MathF.Abs(anim.Body.Position.Y));
                    }

                    if (i == 30 && action == InputActions.MoveRight)
                    {
                        await Shot($"arte-andar-{cls}-lado");
                    }
                }

                Input.ActionRelease(action);
                var seconds = (Time.GetTicksMsec() - t0) / 1000.0;
                var fps = (anim.WalkFrameChanges - changes) / seconds;
                Require(anim.UsesDrawnFrames && anim.CurrentAnimation == expected, $"{cls}: andando deveria tocar {expected}, está '{anim.CurrentAnimation}'");
                Require(frames.Count >= 5, $"{cls} {expected}: passou por {frames.Count} quadros desenhados (esperado os 6)");
                Require(fps is > 6 and < 12.5, $"{cls} {expected}: {fps:0.0} trocas de quadro/s (esperado ~9 fps)");
                Require(maxBob < 0.01f, $"{cls} {expected}: quique procedural por cima do quadro desenhado ({maxBob:0.00} u)");
                Log.Info($"arte: {cls} {expected} {fps:0.0} fps, {frames.Count} quadros");
                Input.ActionPress(back);
                await Frames(10);
                Input.ActionRelease(back);
            }

            await WaitSeconds(0.6);
            Require(!anim.UsesDrawnFrames, $"{cls}: parado volta à pose parada (kit/atlas)");
            if (cls == "sword")
            {
                Require(anim.UsesKitFrames && anim.CurrentAnimation == "idle_front", $"Lâmina parada: idle_front do kit, está {anim.CurrentAnimation}");
            }

            await Shot($"arte-parado-{cls}");
        }

        GameManager.Instance.NewGame("Bot", "sword");
    }

    [SmokeScenario("arte: esquiva desenhada no tempo do dash, sem deslocamento duplo", order: 141)]
    private async Task ArtDodge()
    {
        var (world, combat) = await CombatSetup();
        foreach (var (cls, dir, expected) in new[]
                 {
                     ("sword", InputActions.MoveRight, "dodge_side"),
                     ("sword", InputActions.MoveLeft, "dodge_side"),
                     ("sword", InputActions.MoveDown, "dodge_front"),
                     ("sword", InputActions.MoveUp, "dodge_back"),
                     ("hammer", InputActions.MoveRight, "walk_side"),
                 })
        {
            GameManager.Instance.NewGame("Bot", cls);
            world.GoMap("guild", 250, 600, instant: true); // piso aberto: o dash inteiro cabe em qualquer direção
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            var anim = player.Animator;
            await WaitSeconds(0.3);
            var start = player.GlobalPosition;
            Input.ActionPress(dir);
            Input.ActionPress(InputActions.Dodge);
            await Frames(2);
            Require(combat.PlayerCombat.State.IsDashing && anim.IsDodging, $"{cls}: esquiva não começou junto com o dash");
            var seen = new HashSet<int>();
            var maxOffset = 0f;
            var dashTicks = 0;
            var shot = false;
            for (var i = 0; i < 30 && (combat.PlayerCombat.State.IsDashing || anim.IsDodging); i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (anim.IsDodging)
                {
                    Require(anim.CurrentAnimation == expected, $"{cls}: na esquiva para {dir} deveria tocar {expected}, está '{anim.CurrentAnimation}'");
                    seen.Add(anim.CurrentFrame);
                    maxOffset = MathF.Max(maxOffset, anim.Body.Position.Length());
                    if (!shot && seen.Count >= 3 && (dir == InputActions.MoveRight || dir == InputActions.MoveDown))
                    {
                        shot = true;
                        await Shot($"arte-esquiva-{cls}-{expected}");
                    }
                }

                dashTicks += combat.PlayerCombat.State.IsDashing ? 1 : 0;
            }

            Input.ActionRelease(InputActions.Dodge);
            Input.ActionRelease(dir);
            Require(!anim.IsDodging && !combat.PlayerCombat.State.IsDashing, $"{cls}: esquiva e dash deveriam acabar juntos");
            Require(maxOffset < 0.01f, $"{cls}: o sprite andou {maxOffset:0.00} u sozinho na esquiva (deslocamento duplo)");
            if (expected.StartsWith("dodge_", StringComparison.Ordinal))
            {
                Require(seen.Count >= 4, $"{expected}: passou por {seen.Count} quadros na esquiva de 0,17 s");
            }

            var moved = player.GlobalPosition.DistanceTo(start);
            Require(moved > 25f, $"{cls}: o dash deveria levar o corpo ~46 u, levou {moved:0.0}");
            await WaitSeconds(0.45);
        }

        GameManager.Instance.NewGame("Bot", "sword");
    }

    [SmokeScenario("arte: chefes trocam de pose pelo estado de IA e respiram", order: 142)]
    private async Task ArtBosses()
    {
        var (world, combat) = await CombatSetup();
        world.GoMap("forge", 800, 520, instant: true);
        await WaitMapSettled(world);
        var player = FindPlayer()!;
        var db = GameManager.Db;
        var rng = new SeededRandom(7);
        var placed = new List<Enemy>();
        player.Teleport(world.CorrectToWalkable(new Vector2(900, 470)));
        var origin = player.GlobalPosition;
        var slot = 0;
        try
        {
            foreach (var id in new[] { "angler", "reaper", "colosso", "rainha", "knight", "wolfE" })
            {
                // duas fileiras de três na frente do caçador (cabem na vista de 640×360 u)
                var x = origin.X + (((slot % 3) - 1) * 190f);
                var y = origin.Y + (slot < 3 ? 60f : 175f);
                slot++;
                var def = db.Monsters[id];
                Require(Enemy.ArtPathFor(def) is not null, $"{id}: sem arte aprovada");
                Enemy.Preload(def);
                var body = new MonsterBody(new ScaledMonster(def, def.Hp, def.Damage, []), x, y, Enemy.HitHeightFor(def), rng) { SpawnT = 0 };
                var enemy = Enemy.Create(body);
                world.Entities.AddChild(enemy);
                placed.Add(enemy);
            }

            var foot = player.GlobalPosition + new Vector2(0, 400); // "jogador" abaixo de todos: olham para a câmera
            var minY = placed.ToDictionary(e => e, _ => 9f);
            var maxY = placed.ToDictionary(e => e, _ => 0f);
            for (var i = 0; i < 60; i++)
            {
                var t = i / 30f;
                foreach (var e in placed)
                {
                    e.Body.ForceState(MonsterState.Chase);
                    e.Sync(t, foot);
                    minY[e] = MathF.Min(minY[e], e.LiveScale.Y);
                    maxY[e] = MathF.Max(maxY[e], e.LiveScale.Y);
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            foreach (var e in placed)
            {
                Require(e.UsesArt && e.ArtPose == "front", $"{e.Body.Def.Id}: perseguindo deveria mostrar a pose 'front' da arte, está '{e.ArtPose}'");
                Require(maxY[e] - minY[e] > 0.015f, $"{e.Body.Def.Id}: parado/andando sem respirar ({minY[e]:0.000}–{maxY[e]:0.000})");
            }

            await Shot("arte-chefes-frente");

            // telegrafia → golpe → especial → morte (o AI desses corpos não roda: o estado é posto à mão)
            foreach (var e in placed)
            {
                e.Body.ForceState(MonsterState.Wind);
                e.Body.T = 0.1;
                e.Sync(3f, foot);
            }

            var crouch = placed.All(e => e.LiveScale.Y < 0.99f);
            Require(crouch, "telegrafia deveria encolher o corpo (antecipação)");
            foreach (var e in placed)
            {
                e.Body.ForceState(MonsterState.Slam);
                e.Body.T = 0.5;
                e.Sync(3.1f, foot);
                e.Sync(3.1f, foot);
                Require(e.Body.Def.Id == "wolfE" ? !e.UsesArt : e.ArtPose == "attack", $"{e.Body.Def.Id}: golpe deveria mostrar 'attack' (Alfa: atlas), está '{e.ArtPose}'");
            }

            await Shot("arte-chefes-golpe");
            foreach (var e in placed)
            {
                e.Body.ForceState(MonsterState.Summon);
                e.Body.T = 0.3;
                e.Sync(3.3f, foot);
                var art = Enemy.ArtPathFor(e.Body.Def)!;
                ArtFrames.TryLoad(art, out var frames);
                var expected = frames.Has("special") ? "special" : frames.Has("attack") ? "attack" : "front";
                Require(e.ArtPose == expected || !e.UsesArt, $"{e.Body.Def.Id}: especial deveria mostrar '{expected}', está '{e.ArtPose}'");
            }

            await Shot("arte-chefes-especial");
            foreach (var e in placed)
            {
                e.Body.Kill();
                e.Body.DeadT = 0.3;
                e.Sync(3.5f, foot);
                if (e.Body.IsBoss)
                {
                    var expected = e.Body.Def.Id == "angler" ? "front" : "death";
                    Require(e.ArtPose == expected, $"{e.Body.Def.Id}: morte deveria mostrar '{expected}', está '{e.ArtPose}'");
                }
            }

            await Shot("arte-chefes-morte");
        }
        finally
        {
            foreach (var e in placed)
            {
                e.QueueFree();
            }
        }
    }

    [SmokeScenario("arte: objetos com estados (barril, baú, lampião, flor, alavanca, porta)", order: 143)]
    private async Task ArtProps()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        World.World.LayoutOverrides["forge"] = ArtFixture;
        WorldDressing.ExtraCatalogPath = FixtureObjects;
        WorldDressing.ReloadCatalog();
        try
        {
            var world = FindWorld();
            world.GoMap("forge", 1120, 500, instant: true);
            await WaitMapSettled(world);
            var player = FindPlayer()!;
            player.Teleport(world.CorrectToWalkable(new Vector2(1120, 500)));
            await Frames(3);
            var d = world.Dressing;
            await Shot("arte-objetos-inteiros");

            // barril e caixote: inteiro → rachado → quebrado + estilhaços, e o destroço fica
            var stateful = d.Breakables.Where(b => b.HasStateArt).ToList();
            Require(stateful.Count >= 4, $"barris/caixotes com arte de estados: {stateful.Count} (layout + catálogo)");
            foreach (var b in stateful)
            {
                Require(b.Kind is "barrel" or "crate" && b.Stage == BreakableStage.Whole, $"{b.Name}: barril/caixote com arte de estados inteiro");
                var hp = b.Hp;
                b.Hit((int)MathF.Ceiling(hp * 0.55f), b.GlobalPosition + new Vector2(-10, 0));
                Require(b.Stage == BreakableStage.Cracked, $"{b.Name}: depois de meia vida deveria estar rachado, está {b.Stage}");
            }

            await Shot("arte-objetos-rachados");
            foreach (var b in stateful)
            {
                b.Hit(999, b.GlobalPosition + new Vector2(-10, 0));
                Require(b.IsBroken && b.Stage == BreakableStage.Broken && b.ShardsFlying, $"{b.Name}: quebrado com estilhaços voando");
            }

            await WaitSeconds(0.15);
            await Shot("arte-objetos-estilhacos");
            await WaitSeconds(1.0);
            Require(stateful.All(b => !b.ShardsFlying && b.Stage == BreakableStage.Broken), "estilhaços assentam e o destroço fica");

            // baús: os quadros desenhados de abrir, aberto e vazio
            var chests = d.Chests.Where(c => c.HasStateArt).ToList();
            Require(chests.Count >= 5, $"baús com arte: {chests.Count}");
            var frames = new HashSet<string>();
            foreach (var c in chests)
            {
                Require(c.ArtFrame == "closed", $"{c.Name}: fechado");
                c.Open();
            }

            var opened = Time.GetTicksMsec();
            for (var i = 0; Time.GetTicksMsec() - opened < 700; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                foreach (var c in chests)
                {
                    frames.Add(c.ArtFrame);
                }

                if (i == 12)
                {
                    await Shot("arte-baus-abrindo");
                }
            }

            Require(frames.IsSupersetOf(new[] { "opening_1", "opening_2", "open" }), $"quadros vistos abrindo: {string.Join(", ", frames)}");
            await WaitSeconds(1.0);
            Require(chests.All(c => c.IsOpen && c.ArtFrame == "empty"), "depois do saque o baú fica vazio");
            await Shot("arte-baus-vazios");

            // lampião e braseiro: chama em 4 quadros acesa; apagado com o halo
            var lamp = d.Objects.FirstOrDefault(o => o.ArtRole == PropArtRole.Lamp) ?? throw new SmokeFailure("sem lampião com arte");
            var brazier = d.Objects.FirstOrDefault(o => o.ArtRole == PropArtRole.Brazier) ?? throw new SmokeFailure("sem braseiro com arte");
            var flames = new HashSet<Texture2D>();
            var watched = Time.GetTicksMsec();
            while (Time.GetTicksMsec() - watched < 700)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (lamp.Sprite.Texture is { } t)
                {
                    flames.Add(t);
                }
            }

            Require(lamp.IsLit && lamp.ArtState == "on" && flames.Count >= 3, $"lampião aceso com a chama animada ({flames.Count} quadros)");
            Require(brazier.ArtState == "on", "braseiro aceso");
            var lampSpot = d.Decor.First(s => s.Object == lamp);
            await FocusDecor(world, lampSpot);
            await TapInteract();
            await Frames(3);
            Require(!lamp.IsLit && lamp.ArtState == "off", $"apagar o lampião mostra o poste apagado, está '{lamp.ArtState}'");
            await Shot("arte-lampiao-apagado");
            await TapInteract();
            await Frames(3);
            Require(lamp.IsLit && lamp.ArtState == "on", "acender de novo");

            // flores: colher → colhida → brotando → florida
            var flower = d.Objects.First(o => o.ArtRole == PropArtRole.Flower);
            var flowerSpot = d.Decor.First(s => s.Object == flower);
            Require(flower.ArtState == "flowering", "flor florida");
            await FocusDecor(world, flowerSpot);
            await TapInteract();
            await WaitSeconds(0.4);
            Require(flower.ArtState == "harvested", $"colhida, está '{flower.ArtState}'");
            await Shot("arte-flor-colhida");
            var regrow = WorldScale.Rules?.Flower.RegrowSeconds ?? 90.0;
            World.World.Objects.MarkSpent(flowerSpot.Key, GameManager.Instance.Day, (Time.GetTicksMsec() / 1000.0) - (regrow * 0.6));
            await WaitSeconds(0.4);
            Require(flower.ArtState == "sprouting", $"brotando no meio da rebrota, está '{flower.ArtState}'");
            World.World.Objects.Clear(flowerSpot.Key);
            await WaitSeconds(0.4);
            Require(flower.ArtState == "flowering", "florida de novo");

            // alavanca e porta alternam ao usar
            foreach (var role in new[] { PropArtRole.Lever, PropArtRole.Door })
            {
                var o = d.Objects.First(x => x.ArtRole == role);
                var spot = d.Decor.First(s => s.Object == o);
                var before = o.ArtState;
                await FocusDecor(world, spot);
                await TapInteract();
                await Frames(3);
                Require(o.ArtState != before && o.Toggled, $"{role}: usar troca o estado ({before} → {o.ArtState})");
                d.Lore.HideCard();
            }

            await Shot("arte-alavanca-porta");
            Require(d.Objects.Any(o => o.ArtRole == PropArtRole.Board && o.ArtState == "board"), "quadro de avisos com a arte nova");
        }
        finally
        {
            World.World.LayoutOverrides.Remove("forge");
            WorldDressing.ExtraCatalogPath = null;
            WorldDressing.ReloadCatalog();
            FindWorld().GoMap("forge", 800, 520, instant: true);
            await WaitMapSettled(FindWorld());
        }
    }

    [SmokeScenario("arte: barco remando na travessia e vara na mão ao pescar", order: 144)]
    private async Task ArtFerryAndFishing()
    {
        var world = await MotorWorld("forge", new Vector2(800, 500));
        try
        {
            var d = world.Dressing;
            var boat = d.Decor.First(s => s.Def.Kind == "boat");
            await FocusDecor(world, boat);
            await TapInteract();
            await Frames(4);
            Require(d.InActivity && d.ActivityName == "Ferry", "travessia começou");
            Require(d.FerryBoat is "raft" or "rowboat", $"barco desenhado na travessia, está '{d.FerryBoat}'");
            var oars = new HashSet<int>();
            var sequence = new List<int> { d.FerryFrame };
            for (var i = 0; i < 900 && d.InActivity; i++)
            {
                await NextPhysicsFrame();
                oars.Add(d.FerryFrame);
                if (d.InActivity && d.FerryFrame != sequence[^1])
                {
                    sequence.Add(d.FerryFrame);
                }

                if (i == 30)
                {
                    await Shot("arte-barco-remando");
                }
            }

            Require(oars.Count >= 3, $"remos passaram por {oars.Count} quadros");
            // os remos avançam sempre no mesmo sentido (ARTE-8): cada troca de quadro é +1 no ciclo, do começo ao fim da travessia
            var backwards = 0;
            for (var i = 1; i < sequence.Count; i++)
            {
                if (sequence[i] != (sequence[i - 1] + 1) % 4)
                {
                    backwards++;
                }
            }

            Require(backwards == 0, $"remos tocaram fora de ordem {backwards} vez(es) na travessia: {string.Join(",", sequence)}");
            await WaitUntil(() => !d.InActivity, 8, "fim da travessia");

            var fishing = d.Decor.First(s => s.Def.Kind == "fishing");
            await FocusDecor(world, fishing);
            await TapInteract();
            await Frames(4);
            Require(d.InActivity && d.ActivityName == "Fishing" && d.RodInHand, "pescando com a vara desenhada na mão");
            await WaitSeconds(0.5);
            await Shot("arte-pesca-vara");
            Input.ActionPress(InputActions.MoveDown);
            await WaitSeconds(0.5);
            Input.ActionRelease(InputActions.MoveDown);
            await WaitUntil(() => !d.InActivity, 3, "desistir da pesca");
            Require(!d.RodInHand, "a vara sai da mão ao parar de pescar");
        }
        finally
        {
            EndMotor();
        }
    }

    [SmokeScenario("arte: bustos no diálogo e ícones do designer no HUD", order: 145)]
    private async Task ArtPortraitsAndHud()
    {
        await WaitForScene("World", timeoutSeconds: 10);
        var ui = UiOrFail();
        foreach (var npc in new[] { "Odete", "Brás", "Lúcia", "Vesna", "Ivo", "Sargento Lauro", "Marta", "Faísca" })
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.DialogueRequested, npc, new[] { "…" });
            await WaitUntil(() => ui.Dialog.IsOpen, 2, $"fala de {npc}");
            Require(ui.Dialog.PortraitIsBust, $"{npc}: retrato deveria ser o busto do designer");
            if (npc == "Odete")
            {
                await SkipTyping(ui);
                await Shot("arte-busto-odete");
            }

            await AdvanceDialog();
            await WaitUntil(() => !ui.Dialog.IsOpen, 2, $"fechar a fala de {npc}");
        }

        var hud = Hud.Current ?? throw new SmokeFailure("sem HUD");
        GameManager.Instance.NewGame("Bot", "staff");
        await Frames(3);
        Require(hud.Ultimate.UsesArt, "Supremo com o ícone do designer");
        Require(hud.PotionSlot.FramedIcon is not null && hud.DodgeSlot.FramedIcon is not null, "poção e esquiva com ícone do designer");
        foreach (var id in Hud.StatusIds)
        {
            Require(UiIcons.Hud($"status_{id}") is not null, $"ícone status_{id}");
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatusChanged, "shield", true);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatusChanged, "slow", true);
        await Frames(2);
        Require(hud.IsStatusShown("shield") && hud.IsStatusShown("slow"), "estados ligados aparecem embaixo das barras");
        await Shot("arte-hud-icones");
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatusChanged, "slow", false);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatusChanged, "shield", false);
        await Frames(2);
        Require(!hud.IsStatusShown("shield") && !hud.IsStatusShown("slow"), "estados desligados somem");
        GameManager.Instance.NewGame("Bot", "sword");
    }
}
