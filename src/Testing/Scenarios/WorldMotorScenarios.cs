using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Testing;

/// <summary>
/// Cenários do motor do mundo (frente B4a): nitidez, fluidez, proporção, animação do caçador, oclusão, mapa vivo,
/// quebráveis, baús, decoração, objetos do catálogo, sons ambiente e minimapa. Usam o layout de teste
/// <c>src/Testing/Fixtures/fixture_forge.json</c> na Rua da Forja e o catálogo <c>objects_fixture.json</c> (os layouts
/// reais são das frentes de mapa). Prints: <c>motor-*.png</c> com <c>--smoke-shots</c>.
/// </summary>
public partial class SmokeTest
{
    private const string FixtureLayout = "res://src/Testing/Fixtures/fixture_forge.json";
    private const string FixtureObjects = "res://src/Testing/Fixtures/objects_fixture.json";

    [SmokeScenario("motor: nitidez com escala inteira e câmera no pixel", order: 30)]
    private async Task MotorSharpness()
    {
        var world = await MotorWorld("forge", new Vector2(600, 500));
        Require((string)ProjectSettings.GetSetting("display/window/stretch/scale_mode") == "integer", "scale_mode deve ser integer");
        Require((string)ProjectSettings.GetSetting("display/window/stretch/aspect") == "expand", "aspect deve ser expand");
        Require(world.MapSprite.TextureFilter == CanvasItem.TextureFilterEnum.Nearest, $"mapa deve ser Nearest, é {world.MapSprite.TextureFilter}");
        Require(world.Entities.TextureFilter == CanvasItem.TextureFilterEnum.LinearWithMipmaps, "sprites ilustrados: Linear com mipmaps");
        Require(world.Dressing.Occluders.All(o => o.TextureFilter == CanvasItem.TextureFilterEnum.Nearest), "recortes de oclusão em Nearest, como o mapa");
        Require(Mathf.IsEqualApprox(world.Camera.Zoom.X, 1f), "zoom 1");

        await Hold(InputActions.MoveRight, 20);
        var cam = world.Camera;
        var n = cam.PixelScale;
        Require(Mathf.IsEqualApprox(cam.Position.X * n, MathF.Round(cam.Position.X * n), 0.01f) && Mathf.IsEqualApprox(cam.Position.Y * n, MathF.Round(cam.Position.Y * n), 0.01f), $"câmera fora do px de tela: {cam.Position} (×{n})");
        var window = DisplayServer.WindowGetSize();
        await Shot($"motor-nitidez-{window.X}x{window.Y}");
        await WaitSeconds(0.1);
        world.GoMap("guild", 250, 330, instant: true);
        await WaitMapSettled(world);
        await Shot($"motor-nitidez-guilda-{window.X}x{window.Y}");
        Log.Info($"motor: janela {window}, escala ×{cam.PixelScale}, vista {cam.VisibleSize}");
        EndMotor();
    }

    [SmokeScenario("motor: andar fluido (aceleração, interpolação, câmera)", order: 31)]
    private async Task MotorFluidity()
    {
        var world = await MotorWorld("forge", new Vector2(800, 500));
        var player = FindPlayer()!;
        Require(GetTree().PhysicsInterpolation, "physics_interpolation deve estar ligada");
        Require(player.VisualPosition.IsEqualApprox(player.GlobalPosition), "depois do teleporte a posição desenhada é a real (sem rastro)");

        // aceleração: em accelSeconds + 1 tick já está na velocidade máxima; para em decelSeconds
        var rules = WorldScale.Rules!.Movement;
        Input.ActionPress(InputActions.MoveRight);
        var ticks = (int)Math.Ceiling(rules.AccelSeconds * Engine.PhysicsTicksPerSecond) + 1;
        await Frames(ticks);
        Require(player.Velocity.X > Player.Player.BaseSpeed * 0.98f, $"depois de {ticks} ticks deveria estar a ~78 u/s, está {player.Velocity.X:0.0}");

        // câmera: andando reto, o x do canto sobe sempre (nunca volta), em passos pequenos
        var xs = new List<float>();
        for (var i = 0; i < 40; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            xs.Add(world.Camera.SmoothTopLeft.X);
        }

        Input.ActionRelease(InputActions.MoveRight);
        var backwards = 0;
        var maxStep = 0f;
        for (var i = 1; i < xs.Count; i++)
        {
            var d = xs[i] - xs[i - 1];
            backwards += d < -0.001f ? 1 : 0;
            maxStep = MathF.Max(maxStep, d);
        }

        Require(backwards == 0, $"câmera voltou {backwards} vez(es) andando reto (tremor)");
        Require(maxStep < 6f, $"câmera deu salto de {maxStep:0.0} u num quadro");
        var stopTicks = (int)Math.Ceiling(rules.DecelSeconds * Engine.PhysicsTicksPerSecond) + 1;
        await Frames(stopTicks);
        Require(player.Velocity.Length() < 0.01f, $"deveria ter parado em {stopTicks} ticks, velocidade {player.Velocity.Length():0.00}");

        // troca de mapa: a interpolação é zerada (sem deslizar do ponto velho)
        world.GoMap("inn", 250, 520, instant: true);
        await NextPhysicsFrame();
        Require(player.VisualPosition.DistanceTo(player.GlobalPosition) < 0.01f, "troca de mapa deixou rastro de interpolação");

        if (DisplayServer.GetName() != "headless")
        {
            world.GoMap("forge", 800, 500, instant: true);
            await WaitMapSettled(world);
            Input.ActionPress(InputActions.MoveLeft);
            await WaitSeconds(1.5);
            Input.ActionRelease(InputActions.MoveLeft);
            var fps = Engine.GetFramesPerSecond();
            Log.Info($"motor: fps andando na Rua da Forja vestida = {fps:0} (process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00} ms, " +
                     $"física {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.00} ms, draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, " +
                     $"objetos {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}, vsync {DisplayServer.WindowGetVsyncMode()}, tela {DisplayServer.ScreenGetRefreshRate():0} Hz)");
            Require(fps >= 45, $"fps {fps:0} muito abaixo de 60 andando (máquina carregada?)");
        }

        EndMotor();
    }

    [SmokeScenario("motor: proporção do jogador, NPCs e monstros", order: 32)]
    private async Task MotorProportion()
    {
        var world = await MotorWorld("forge", new Vector2(250, 312));
        var rules = WorldScale.Rules!;
        var ratio = WorldScale.CharacterHeight / (float)rules.Calibration.DoorHeight;
        Require(ratio is >= 0.85f and <= 0.9f, $"jogador/porta = {ratio:0.000}, esperado 0,85–0,90");
        Require(WorldScale.Classify("npc_bras_f", SpriteScaleClass.Auto) == SpriteScaleClass.Character, "NPC usa characterScale");
        Require(WorldScale.Classify("wolf_f", SpriteScaleClass.Auto) == SpriteScaleClass.Character, "monstro usa characterScale");
        Require(WorldScale.Classify("pr_barrel", SpriteScaleClass.Auto) == SpriteScaleClass.Prop, "objeto usa propScale");
        Require(WorldScale.Classify("i_coin", SpriteScaleClass.Auto) == SpriteScaleClass.Fixed, "ícone não escala");
        var npc = new AtlasSprite { Key = "npc_bras_f", Position = new Vector2(290, 318) };
        world.Entities.AddChild(npc);
        Require(AtlasLibrary.TryGet("npc_bras_f", out var f) && Mathf.IsEqualApprox(npc.Scale.X, WorldScale.Character / f.Density, 0.0001f), "escala do NPC");
        await Frames(2);
        await Shot("motor-proporcao-porta-estalagem");
        EndMotor();
    }

    [SmokeScenario("motor: animação do caçador (kit, procedural, poeira, passos)", order: 33)]
    private async Task MotorHunterAnimation()
    {
        var world = await MotorWorld("forge", new Vector2(800, 500));
        var player = FindPlayer()!;
        var anim = player.Animator;
        player.ClassId = "sword";
        await WaitSeconds(0.6);
        Require(anim.UsesKitFrames && anim.CurrentAnimation == "idle_front", $"parado: idle_front do kit, está {anim.CurrentAnimation}");
        await Shot("motor-cacador-parado");

        var frames = new HashSet<int>();
        var steps = anim.StepsTaken;
        var dust = anim.DustBursts;
        Input.ActionPress(InputActions.MoveRight);
        for (var i = 0; i < 70; i++)
        {
            await NextPhysicsFrame();
            if (anim.CurrentAnimation == "walk_side")
            {
                frames.Add(anim.Hunter.Frame);
            }

            if (i == 35)
            {
                await Shot("motor-cacador-andando-lado");
            }
        }

        Input.ActionRelease(InputActions.MoveRight);
        Require(frames.Count >= 3, $"caminhada de lado deveria passar por 3+ quadros do kit, passou por {frames.Count}");
        Require(anim.StepsTaken - steps >= 3, $"passos contados: {anim.StepsTaken - steps}");
        Require(anim.DustBursts > dust, "poeira nos pés ao andar");
        Require(AudioManager.Instance.HasSfx("step"), "sfx step carregado");

        // costas: procedural (o corpo quica e balança)
        var ys = new List<float>();
        Input.ActionPress(InputActions.MoveUp);
        for (var i = 0; i < 40; i++)
        {
            await NextPhysicsFrame();
            ys.Add(anim.Body.Position.Y);
        }

        Input.ActionRelease(InputActions.MoveUp);
        Require(anim.CurrentAnimation == "idle_back", $"andando para cima usa idle_back com passo procedural, está {anim.CurrentAnimation}");
        Require(ys.Max() - ys.Min() > 1f, $"passo procedural das costas sem quique ({ys.Max() - ys.Min():0.00} u)");
        await Shot("motor-cacador-costas");

        player.PlayAttack();
        await WaitSeconds(0.1); // tempo real: o pose é aplicado no _Process

        Require(anim.CurrentAnimation == "attack", $"PlayAttack toca attack, está {anim.CurrentAnimation}");
        await Shot("motor-cacador-golpe");
        await WaitSeconds(0.4);
        if (Combat.CombatDirector.Instance is null)
        {
            player.AttackPose = true; // a API antiga do combate continua valendo
            await WaitSeconds(0.08);
            Require(anim.CurrentAnimation == "attack", "AttackPose mostra o golpe");
        }
        else
        {
            // Integração v0.1: o combate é dono do AttackPose (reescreve a cada quadro); o golpe de verdade mostra o quadro.
            Input.ActionPress(InputActions.Attack);
            var sawAttack = false;
            for (var i = 0; i < 20 && !sawAttack; i++)
            {
                await NextPhysicsFrame();
                sawAttack = anim.CurrentAnimation == "attack";
            }

            Input.ActionRelease(InputActions.Attack);
            Require(sawAttack, $"o golpe do combate mostra o quadro de ataque, está {anim.CurrentAnimation}");
        }
        player.AttackPose = false;
        player.PlayHurt();
        await WaitSeconds(0.08);
        Require(anim.CurrentAnimation == "hurt" && anim.IsHurt, "PlayHurt mostra o dano");
        await Shot("motor-cacador-dano");
        await WaitSeconds(0.4);
        player.PlayDeath();
        await WaitSeconds(0.6);
        Require(anim.IsDead && anim.CurrentAnimation == "death" && anim.Hunter.Frame == 1, "PlayDeath termina caído (death 1)");
        Require(player.IsDead, "jogador morto não anda");
        await Shot("motor-cacador-morte");
        player.Revive();
        await Frames(2);
        Require(!anim.IsDead, "Revive");

        // outra classe: atlas + procedural
        player.ClassId = "bow";
        Input.ActionPress(InputActions.MoveLeft);
        await Frames(20);
        Input.ActionRelease(InputActions.MoveLeft);
        Require(!anim.UsesKitFrames && player.Sprite.Visible && player.Sprite.Key == "c_bow_side", $"arco usa o atlas c_bow_side, está {player.Sprite.Key}");
        await Shot("motor-arco-andando");
        player.ClassId = "sword";
        EndMotor();
    }

    [SmokeScenario("motor: oclusão e transparência dos recortes", order: 34)]
    private async Task MotorOcclusion()
    {
        var world = await MotorWorld("forge", new Vector2(583, 540));
        var lamp = world.Dressing.Occluders.FirstOrDefault(o => o.Def.Note == "lampião da praça") ?? throw new SmokeFailure("fixture sem o lampião da praça");
        await WaitSeconds(0.4);
        Require(lamp.Alpha > 0.99f && !lamp.IsCovering, $"na frente do lampião ele fica opaco (alpha {lamp.Alpha:0.00})");

        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2(584, 506)));
        await WaitSeconds(0.6);
        Require(lamp.IsCovering, $"pé em {player.GlobalPosition} atrás do lampião (base {lamp.Def.BaseY}) deveria estar coberto");
        var alpha = (float)WorldScale.Rules!.Occlusion.CoveredAlpha;
        Require(MathF.Abs(lamp.Alpha - alpha) < 0.05f, $"coberto: alpha ~{alpha}, está {lamp.Alpha:0.00}");
        Require(lamp.ZIndex == 0 && lamp.GetParent() == world.Entities, "recorte entra no y-sort das entidades");
        await Shot("motor-oclusao-atras-do-lampiao");

        var swaying = world.Dressing.Occluders.Count(o => o.Material is ShaderMaterial);
        Require(swaying == world.Layout.Occluders.Count(o => o.Sway), $"recortes com sway: {swaying}");
        player.Teleport(world.CorrectToWalkable(new Vector2(752, 600)));
        await WaitSeconds(0.5);
        await Shot("motor-oclusao-bandeira");
        EndMotor();
    }

    [SmokeScenario("motor: mapa vivo (água, luzes, fumaça, vagalumes)", order: 35)]
    private async Task MotorAlive()
    {
        var world = await MotorWorld("forge", new Vector2(820, 830));
        var d = world.Dressing;
        Require(d.WaterCount == world.Layout.Water.Count && d.WaterCount > 0, $"formas de água: {d.WaterCount}");
        Require(d.Lights.Count >= LightGlows.LightsOf(world.Map).Count && d.Lights.Count > 0, $"luzes: {d.Lights.Count}");
        Require(d.Fx.SmokeCount == world.Layout.Fx.Count(f => f.Kind == "smoke"), "fumaça de chaminé");

        var state = GameManager.Instance.State;
        var clock = state.Save.ClockMinutes;
        state.Save.ClockMinutes = 21 * 60;
        await WaitSeconds(1.2);
        Require(d.Fx.FirefliesOn, "vagalumes acendem à noite");
        await Shot("motor-mapa-vivo-canal-noite");
        state.Save.ClockMinutes = 10 * 60;
        await WaitSeconds(1.2);
        Require(!d.Fx.FirefliesOn, "vagalumes apagam de dia");
        state.Save.ClockMinutes = clock;
        await Shot("motor-mapa-vivo-canal-dia");
        EndMotor();
    }

    [SmokeScenario("motor: quebráveis com vida, estilhaços, saque e dia seguinte", order: 36)]
    private async Task MotorBreakables()
    {
        var world = await MotorWorld("forge", new Vector2(628, 540));
        var d = world.Dressing;
        var barrel = d.Breakables.First(b => b.Kind == "barrel" && b.Name.ToString().Contains("628"));
        Require(barrel.IsInGroup(Breakable.HittableGroup), "quebrável no grupo hittable");
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(barrel.GlobalPosition + new Vector2(0, 16)));
        await NextPhysicsFrame();
        Require(!world.WalkArea.CanStand(barrel.GlobalPosition.X, barrel.GlobalPosition.Y) || TestSolid(player, barrel.GlobalPosition), "barril é sólido");

        string? broken = null;
        void OnBroken(string map, string kind, Vector2 at) => broken = kind;
        EventBus.Instance.PropBroken += OnBroken;
        try
        {
            // olha para cima (o barril) e golpeia com o ataque provisório até quebrar
            await Hold(InputActions.MoveUp, 2);
            for (var i = 0; i < 6 && !barrel.IsBroken; i++)
            {
                Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = true });
                await NextPhysicsFrame();
                Input.ParseInputEvent(new InputEventAction { Action = InputActions.Attack, Pressed = false });
                if (i == 0)
                {
                    await Frames(2);
                    Require(barrel.IsShaking && barrel.Hp < barrel.MaxHp, "golpe tira vida e faz tremer");
                    await Shot("motor-barril-golpe");
                }

                await WaitSeconds(0.35);
            }
        }
        finally
        {
            EventBus.Instance.PropBroken -= OnBroken;
        }

        Require(barrel.IsBroken && broken == "barrel", $"barril deveria quebrar em 3 golpes (hp {barrel.Hp}/{barrel.MaxHp})");
        Require(d.Popups.Active > 0, "saque ou 'vazio' sobe em texto");
        await Shot("motor-barril-quebrado");

        // continua quebrado ao sair e voltar; volta no dia seguinte
        world.GoMap("inn", 250, 520, instant: true);
        await WaitMapSettled(world);
        world.GoMap("forge", 628, 540, instant: true);
        await WaitMapSettled(world);
        var again = world.Dressing.Breakables.First(b => b.Key == barrel.Key);
        Require(again.IsBroken, "quebrado até o dia seguinte");
        EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
        await Frames(2);
        Require(!again.IsBroken && again.IsInGroup(Breakable.HittableGroup), "dia seguinte: inteiro de novo");
        EndMotor();
    }

    [SmokeScenario("motor: baús abrem com animação e saque", order: 37)]
    private async Task MotorChests()
    {
        var world = await MotorWorld("forge", new Vector2(632, 470));
        var chest = world.Dressing.Chests.First(c => c.Tier == 1);
        var player = FindPlayer()!;
        await FocusOn(world, chest.Interactable);
        Require(world.Dressing.Prompt.ShownText == "Abrir baú" && world.Dressing.Prompt.KeyGlyph.Length > 0, $"prompt com ícone: '{world.Dressing.Prompt.ShownText}' [{world.Dressing.Prompt.KeyGlyph}]");
        await Shot("motor-bau-prompt");

        var lumes = GameManager.Instance.Lumes;
        var opened = false;
        void OnOpened(string map, int tier, Vector2 at) => opened = true;
        EventBus.Instance.ChestOpened += OnOpened;
        try
        {
            await TapInteract();
            await Frames(6);
            Require(chest.IsOpening, "abrindo (animação)");
            await Shot("motor-bau-abrindo");
            await WaitUntil(() => chest.IsOpen, 2, "baú aberto");
        }
        finally
        {
            EventBus.Instance.ChestOpened -= OnOpened;
        }

        Require(opened && GameManager.Instance.Lumes > lumes, $"saque do baú (Lumes {lumes} → {GameManager.Instance.Lumes})");
        await WaitSeconds(0.3);
        await Shot("motor-bau-aberto");
        Require(WorldDressing.IsDiscovered(chest), "baú visto entra no minimapa");
        EventBus.Instance.EmitSignal(EventBus.SignalName.DayChanged, GameManager.Instance.Day + 1);
        await Frames(2);
        Require(!chest.IsOpen && chest.Interactable.Enabled, "dia seguinte: baú fechado de novo");
        EndMotor();
    }

    [SmokeScenario("motor: decoração (lampião, flor, placa, banco, balsa, pesca)", order: 38)]
    private async Task MotorDecor()
    {
        var world = await MotorWorld("forge", new Vector2(600, 540));
        var d = world.Dressing;
        var used = new List<string>();
        void OnUsed(string kind, string map, int index) => used.Add(kind);
        EventBus.Instance.WorldObjectUsed += OnUsed;
        try
        {
            // lampião: apaga e acende o halo
            var lamp = DecorOf(d, "lamp");
            await FocusDecor(world, lamp);
            var lit = d.Lights.LitCount;
            await TapInteract();
            Require(d.Lights.LitCount == lit - 1 && lamp.Interactable.Prompt == "Acender lampião", "lampião apagou");
            await TapInteract();
            Require(d.Lights.LitCount == lit, "lampião acendeu");

            // flor: colhe (erva), some e volta a crescer
            var flower = DecorOf(d, "flower");
            await FocusDecor(world, flower);
            var herb = WorldScale.Rules!.Flower.Material;
            var before = GameManager.Instance.State.MaterialCount(herb);
            await TapInteract();
            Require(GameManager.Instance.State.MaterialCount(herb) == before + 1, $"flor dá {herb}");
            Require(!flower.Interactable.Enabled, "flor colhida não pode ser colhida de novo na hora");
            await Shot("motor-flor-colhida");

            // placa: texto curto no mundo
            var sign = DecorOf(d, "sign");
            await FocusDecor(world, sign);
            await TapInteract();
            Require(d.Lore.Text.StartsWith("Rua da Forja", StringComparison.Ordinal), $"placa mostra o texto, mostra '{d.Lore.Text}' (foco: {FindPlayer()!.FocusedInteractable?.Prompt}, usos: {string.Join(",", used)})");
            await Frames(3);
            await Shot("motor-placa");

            // banco: senta (corpo mais baixo, não anda) e levanta ao andar
            var bench = DecorOf(d, "bench");
            await FocusDecor(world, bench);
            await TapInteract();
            Require(d.InActivity && d.ActivityName == "Sitting" && FindPlayer()!.Animator.Sitting, "sentou no banco");
            await Frames(4);
            await Shot("motor-banco");
            await WaitSeconds(0.5);
            await Hold(InputActions.MoveDown, 6);
            await Frames(2);
            Require(!d.InActivity && !FindPlayer()!.Animator.Sitting, "levantou ao andar");

            // balsa: desliza até o outro cais
            var boat = DecorOf(d, "boat");
            await FocusDecor(world, boat);
            await TapInteract();
            Require(d.InActivity && d.ActivityName == "Ferry", "balsa em andamento");
            await WaitSeconds(1.0);
            await Shot("motor-balsa-atravessando");
            await WaitUntil(() => !d.InActivity, 8, "fim da travessia");
            var to = boat.Def.To!.Value;
            var player = FindPlayer()!;
            Require(player.GlobalPosition.DistanceTo(new Vector2(to.X, to.Y)) < 24f, $"balsa chegou em {player.GlobalPosition}, esperava ~({to.X}, {to.Y})");
            Require(player.CollisionMask != 0 && !player.MovementLocked, "colisão e movimento voltam depois da balsa");

            // pesca: espera morder, puxa com a agulha na faixa
            var fishing = DecorOf(d, "fishing");
            world.GoMap("forge", fishing.Def.X, fishing.Def.Y + 4, instant: true);
            await WaitMapSettled(world);
            fishing = DecorOf(world.Dressing, "fishing");
            await FocusDecor(world, fishing);
            var fishId = WorldScale.Rules.Fishing.Material;
            var fish = GameManager.Instance.State.MaterialCount(fishId);
            await TapInteract();
            var game = world.Dressing.Fishing ?? throw new SmokeFailure("pesca não começou");
            await WaitUntil(() => game.State == FishingState.Biting, 5, "peixe morder");
            await Shot("motor-pesca-mordeu");
            await WaitUntil(() => game.NeedleInZone, 4, "agulha na faixa");
            await TapInteract();
            Require(game.State == FishingState.Caught, $"puxou na faixa e deveria fisgar, ficou {game.State}");
            Require(GameManager.Instance.State.MaterialCount(fishId) == fish + 1, "peixe na bolsa");
            await WaitUntil(() => !world.Dressing.InActivity, 3, "fim da pesca");
        }
        finally
        {
            EventBus.Instance.WorldObjectUsed -= OnUsed;
        }

        foreach (var kind in new[] { "lamp", "flower", "sign", "bench", "boat", "fishing" })
        {
            Require(used.Contains(kind), $"WorldObjectUsed não veio para {kind}");
        }

        EndMotor();
    }

    [SmokeScenario("motor: objetos do catálogo (sprite, colisão, luz, sway, interação)", order: 39)]
    private async Task MotorCatalogObjects()
    {
        var world = await MotorWorld("forge", new Vector2(676, 530));
        var d = world.Dressing;
        Require(WorldDressing.Catalog.TryGet("fx_brazier", out _), "catálogo de teste carregado");
        var brazier = d.Objects.FirstOrDefault(o => o.Def.Id == "fx_brazier") ?? throw new SmokeFailure("braseiro do catálogo não apareceu");
        Require(brazier.GetParent() == world.Entities && brazier.GetNodeOrNull("Footprint") is not null, "objeto no y-sort com colisão");
        Require(d.Objects.Any(o => o.Def.Id == "fx_crystal" && o.Sprite.Material is ShaderMaterial), "sway no cristal");
        Require(d.Breakables.Any(b => b.Key.Contains(":object:", StringComparison.Ordinal)), "barril do catálogo virou quebrável");
        Require(d.Decor.Any(s => s.Def.Kind == "sign" && s.Def.Text!.StartsWith("Placa de teste", StringComparison.Ordinal)), "placa do catálogo tem interação");
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2(676, 488)));
        await WaitSeconds(0.5);
        Require(brazier.IsCovering && brazier.Alpha < 0.6f, $"atrás do braseiro ele fica translúcido (alpha {brazier.Alpha:0.00})");
        await Shot("motor-objetos-catalogo");
        EndMotor();
    }

    [SmokeScenario("motor: sons ambiente posicionais com crossfade", order: 40)]
    private async Task MotorAmbientSound()
    {
        var world = await MotorWorld("forge", new Vector2(800, 800));
        var audio = AudioManager.Instance;
        Require(audio.AmbientEmitterCount == world.Layout.Ambient.Count, $"emissores: {audio.AmbientEmitterCount}");
        foreach (var kind in AudioManager.AmbientIds)
        {
            Require(audio.HasAmbient(kind), $"loop de ambiente '{kind}' carregado");
        }

        await WaitSeconds(1.6);
        Require(audio.AmbientLevel("water") > 0.2f, $"perto do canal a água soa (nível {audio.AmbientLevel("water"):0.00})");
        Require(audio.ActiveAmbientVoices <= AudioManager.AmbientVoices, "pool de ambiente fixo");
        var player = FindPlayer()!;
        player.Teleport(world.CorrectToWalkable(new Vector2(1165, 240)));
        await WaitSeconds(1.6);
        Require(audio.AmbientLevel("forge") > 0.2f && audio.AmbientLevel("water") < 0.05f, $"na forja: forja {audio.AmbientLevel("forge"):0.00}, água {audio.AmbientLevel("water"):0.00}");

        // troca de mapa: os do mapa velho saem com fade
        world.GoMap("inn", 250, 520, instant: true);
        await WaitMapSettled(world);
        Require(audio.AmbientEmitterCount == world.Layout.Ambient.Count, "emissores do mapa novo");
        await WaitSeconds(1.6);
        Require(audio.AmbientLevel("forge") < 0.01f, "o ambiente do mapa velho saiu");
        EndMotor();
    }

    [SmokeScenario("motor: minimapa no canto e em tela cheia", order: 41)]
    private async Task MotorMinimap()
    {
        var world = await MotorWorld("forge", new Vector2(632, 470));
        var map = world.Minimap;
        await WaitSeconds(0.3);
        Require(!map.Expanded && map.MarkerCount >= world.Map.Exits.Count + 1, $"canto: {map.MarkerCount} marcadores");
        await Shot("motor-minimapa-canto");
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Map, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Map, Pressed = false });
        Require(map.Expanded && GameManager.Instance.IsGameplayBlocked, "M abre o mapa em tela cheia e trava o jogo");
        await Frames(3);
        await Shot("motor-minimapa-tela-cheia");
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Map, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Map, Pressed = false });
        Require(!map.Expanded, "M de novo fecha");
        Require(!GameManager.Instance.IsGameplayBlocked, "fechar o mapa destrava o jogo");
        foreach (var e in InputMap.ActionGetEvents(InputActions.Map))
        {
            Log.Info($"motor: mapa em {e.AsText()}");
        }

        Require(InputMap.ActionGetEvents(InputActions.Map).Count >= 2, "M e L3 do gamepad (Tab/Back são do inventário, INT-D3)");
        EndMotor();
    }

    [SmokeScenario("motor (tester): andar no mapa vestido não aloca", order: 42)]
    private async Task MotorAllocation()
    {
        var world = await MotorWorld("forge", new Vector2(700, 520));
        Input.ActionPress(InputActions.MoveLeft);
        await WaitSeconds(0.5);
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var physics = Engine.GetPhysicsFrames();
        await WaitSeconds(2.0);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Input.ActionRelease(InputActions.MoveLeft);
        var perFrame = allocated / (long)Math.Max(1, Engine.GetPhysicsFrames() - physics);
        Log.Info($"motor (tester): {allocated} B em 2 s andando na Rua da Forja vestida ({perFrame} B/frame de física), fps {Engine.GetFramesPerSecond():0}");
        Require(perFrame < 2048, $"andar aloca {perFrame} B por frame de física");
        EndMotor();
    }

    // --- utilidades ---

    private async Task<World.World> MotorWorld(string mapId, Vector2 at)
    {
        await WaitForScene("World", timeoutSeconds: 10);
        World.World.LayoutOverrides[CityMapIdForFixture] = FixtureLayout;
        WorldDressing.ExtraCatalogPath = FixtureObjects;
        WorldDressing.ReloadCatalog();
        var world = FindWorld();
        world.GoMap(mapId, at, instant: true);
        await WaitMapSettled(world);
        FindPlayer()!.Teleport(world.CorrectToWalkable(at));
        await Frames(2);
        return world;
    }

    private const string CityMapIdForFixture = "forge";

    private void EndMotor()
    {
        World.World.LayoutOverrides.Remove(CityMapIdForFixture);
        WorldDressing.ExtraCatalogPath = null;
        WorldDressing.ReloadCatalog();
    }

    private static DecorSpot DecorOf(WorldDressing d, string kind) =>
        d.Decor.FirstOrDefault(s => s.Def.Kind == kind) ?? throw new SmokeFailure($"layout sem decor '{kind}'");

    private Task FocusDecor(World.World world, DecorSpot spot) => FocusOn(world, spot.Interactable);

    /// <summary>Põe o jogador num ponto andável dentro do raio de <paramref name="target"/> em que ele é o foco (o mais próximo).</summary>
    private async Task FocusOn(World.World world, Interactable target)
    {
        var player = FindPlayer()!;
        var center = target.GlobalPosition;
        var r = MathF.Max(6f, target.Radius * 0.85f);
        var candidates = new List<Vector2>();
        for (var dy = -r; dy <= r; dy += 3f)
        {
            for (var dx = -r; dx <= r; dx += 3f)
            {
                var p = center + new Vector2(dx, dy);
                if (dx * dx + dy * dy <= r * r && world.WalkArea.CanStand(p.X, p.Y))
                {
                    candidates.Add(p);
                }
            }
        }

        // perto primeiro, preferindo ficar na frente (abaixo) do objeto
        candidates.Sort((a, b) => (a.DistanceSquaredTo(center) + (a.Y < center.Y ? 40f : 0f)).CompareTo(b.DistanceSquaredTo(center) + (b.Y < center.Y ? 40f : 0f)));
        foreach (var p in candidates.Take(40))
        {
            player.Teleport(p);
            await Frames(3);
            if (player.FocusedInteractable == target)
            {
                return;
            }
        }

        throw new SmokeFailure($"não deu para focar '{target.Prompt}' em {center} ({candidates.Count} pontos andáveis no raio)");
    }

    private async Task TapInteract()
    {
        // tempo real entre apertar e soltar (como uma tecla): depois de um print lento a física recupera vários ticks
        // num quadro só, e apertar+soltar no mesmo quadro não chega como toque
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventAction { Action = InputActions.Interact, Pressed = false });
        await NextPhysicsFrame();
    }

    private static bool TestSolid(Player.Player player, Vector2 target) =>
        player.TestMove(player.GlobalTransform, target - player.GlobalPosition);
}
