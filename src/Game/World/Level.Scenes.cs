using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Story;

namespace Vespera.Game.World;

/// <summary>
/// Cenas curtas da história, sem arte nova: panorâmicas da câmera, NPC andando até o jogador, fade e o brilho da
/// lanterna. Duas ferramentas: o campo <c>look</c>/<c>fx</c> de cada caixa de fala (a câmera olha para o que a fala cita)
/// e as cenas de entrada (chegada à Praça, Ravi vindo desafiar, Benício vindo entregar o selo). Dá para pular com Interagir
/// ou Esc/Pausa, e o texto tem seu próprio "pular".
/// </summary>
public partial class Level
{
    /// <summary>Acelera as pausas e as caminhadas das cenas (o teste automático usa um valor alto; no jogo é 1).</summary>
    public static float SceneSpeed { get; set; } = 1f;

    private const float PanSeconds = 1.0f;
    private const float ArrivalFadeSeconds = 1.4f;
    private const float ArrivalHold = 1.5f;
    private const float RoadStartHumans = 9f;
    private const float RoadLodoSeconds = 40f;
    private const float RoadWalkSeconds = 30f;
    private const float PlazaReachHumans = 2.5f;
    private const float MorningHold = 1.2f;
    private const float ApproachHumans = 1.15f;
    private const float WalkTimeout = 7f;
    private const float PlayerWalkFactor = 0.6f;
    private const float ChestHeightFactor = 60f;
    private const float GlowRadiusHumans = 8f;
    private const float GlowEnergy = 1.1f;
    private const float GlowPersistentEnergy = 0.7f;

    private Tween? _pan;
    private bool _sceneBusy;
    private bool _skipScene;
    private bool _freeRoam;
    private PointLight2D? _lanternGlow;

    /// <summary>Uma cena de entrada está rodando (o jogador não controla nada).</summary>
    public bool SceneBusy => _sceneBusy && !_freeRoam;

    private Vector2 PlayerChest => Player.GlobalPosition + new Vector2(0, -ChestHeightFactor * World.Data.ActorScale);

    /// <summary>Roda a fala automática com a cena que a acompanha; falso se não há nada a tocar agora.</summary>
    private bool BeginAutoplayScene(AutoplayDef auto)
    {
        if (_sceneBusy)
        {
            return false;
        }

        _ = RunAutoplayScene(auto);
        return true;
    }

    private async Task RunAutoplayScene(AutoplayDef auto)
    {
        _sceneBusy = true;
        _skipScene = false;
        Player.Frozen = true;
        Npc? npc = null;
        try
        {
            npc = await StageAutoplay(auto);
        }
        catch (System.Exception ex)
        {
            Log.Error($"cena '{auto.Dialogue}' falhou: {ex.Message}");
        }
        finally
        {
            _sceneBusy = false;
            _freeRoam = false;
        }

        if (!StartDialogue(auto.Dialogue, npc))
        {
            Player.Frozen = false;
        }
    }

    /// <summary>Encenação antes da fala (estrada/Brecha, manhã, NPC que se aproxima); devolve quem vai falar.</summary>
    private async Task<Npc?> StageAutoplay(AutoplayDef auto)
    {
        var npc = auto.Npc.Length > 0 ? World.Npcs.FirstOrDefault(n => n.NpcId == auto.Npc) : null;
        if (auto.Stage == "arrival")
        {
            await StageArrival();
        }
        else if (auto.Stage == "morning")
        {
            await StageMorning();
        }

        if (npc is not null)
        {
            await StageApproach(npc);
        }

        return npc;
    }

    /// <summary>
    /// Chegada (ROTEIRO-V2 §3, 0:10–1:20): o caçador entra pela estrada (a borda sul da Praça), já com a Lâmina
    /// enferrujada, e anda sozinho. Um Lodo pequeno cruza o caminho; perto do meio da praça a Brecha abre com estrondo
    /// e luz magenta, e a lanterna do caçador acende sozinha. Aí o Benício vem falar.
    /// </summary>
    private async Task StageArrival()
    {
        var human = World.Data.HumanHeight;
        StoryService.SetFlag(StoryFlags.StarterBlade);
        var start = World.ClampToFloor(World.SpawnPoint + new Vector2(0, human * RoadStartHumans));
        Player.Teleport(start);
        Player.Face(Vector2.Up);
        await FadeFrom(1f, ArrivalFadeSeconds);

        _freeRoam = true;
        Player.Frozen = false;
        var lodo = new Vespera.Game.Actors.Enemies.Lodo(World)
        {
            Position = World.ClampToFloor(start.Lerp(World.SpawnPoint, 0.45f) + new Vector2(-human * 2.5f, 0)),
        };
        World.AddEnemy(lodo);
        await WaitUntil(() => !IsInstanceValid(lodo) || lodo.IsQueuedForDeletion() || lodo.Dead, RoadLodoSeconds, () => KillIfAlive(lodo));
        await WaitUntil(() => Player.GlobalPosition.DistanceTo(World.SpawnPoint) < human * PlazaReachHumans, RoadWalkSeconds, null);
        _freeRoam = false;
        Player.Frozen = true;
        await WalkPlayerTo(World.SpawnPoint);
        await OpenRift();
    }

    private static void KillIfAlive(Vespera.Game.Actors.Enemies.Enemy enemy)
    {
        if (IsInstanceValid(enemy) && !enemy.Dead)
        {
            enemy.Die();
        }
    }

    /// <summary>A Brecha abre: tremor, clarão magenta na fenda e a lanterna do caçador acendendo sem ninguém tocar.</summary>
    private async Task OpenRift()
    {
        World.Camera.AddTrauma(1f);
        PanTo("brecha");
        if (FocusPoint("brecha") is { } at)
        {
            var flash = new PointLight2D
            {
                Name = "RiftFlash",
                Texture = Vespera.Game.Components.LightTexture.Get(),
                Color = new Color("e64a8e"),
                Energy = 2.2f,
                TextureScale = World.Data.HumanHeight * GlowRadiusHumans / Vespera.Game.Components.LightTexture.Radius,
                GlobalPosition = at,
            };
            World.AddChild(flash);
            var fadeOut = CreateTween();
            fadeOut.TweenProperty(flash, "energy", 0.5f, 1.6f / SceneSpeed);
            World.Feel.Spark(at, new Color("e64a8e"), 40, 420f, Vector2.Up, 180f);
        }

        await SceneWait(1.0f);
        LightPlayerLantern();
        PanTo("player");
        await SceneWait(1.2f);
    }

    /// <summary>A lanterna do caçador acende sozinha (primeira pista de "de quem era a lanterna"). Ninguém comenta.</summary>
    private void LightPlayerLantern()
    {
        if (Player.GetNodeOrNull<PointLight2D>("OwnLantern") is not null)
        {
            return;
        }

        var light = new PointLight2D
        {
            Name = "OwnLantern",
            Texture = Vespera.Game.Components.LightTexture.Get(),
            Color = new Color(1f, 0.78f, 0.42f),
            Energy = 0f,
            TextureScale = World.Data.HumanHeight * 3f / Vespera.Game.Components.LightTexture.Radius,
            Position = new Vector2(0, -ChestHeightFactor * World.Data.ActorScale),
        };
        Player.AddChild(light);
        CreateTween().TweenProperty(light, "energy", 0.9f, 0.8f / SceneSpeed);
    }

    /// <summary>A noite passa (ROTEIRO-V2 §3, 9:00): tela escura, um respiro, e a praça de manhã com o caçador no meio.</summary>
    private async Task StageMorning()
    {
        _fade.Color = _fade.Color with { A = 1f };
        StoryService.Dawn();
        Player.Teleport(World.SpawnPoint);
        await SceneWait(MorningHold);
        await FadeFrom(1f, ArrivalFadeSeconds);
    }

    /// <summary>Espera a condição valer, até <paramref name="seconds"/> (dividido por <see cref="SceneSpeed"/>); no fim do prazo roda <paramref name="onTimeout"/>.</summary>
    private async Task WaitUntil(System.Func<bool> done, float seconds, System.Action? onTimeout)
    {
        var left = seconds / SceneSpeed;
        while (left > 0f && !_skipScene && !done())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            left -= (float)GetProcessDeltaTime();
        }

        if (!done())
        {
            onTimeout?.Invoke();
        }
    }

    /// <summary>O NPC (Ravi, Benício) anda até ficar de frente para o jogador, e os dois se olham.</summary>
    private async Task StageApproach(Npc npc)
    {
        var away = npc.GlobalPosition - Player.GlobalPosition;
        var dir = away.LengthSquared() < 1f ? Vector2.Down : away.Normalized();
        var goal = World.ClampToFloor(Player.GlobalPosition + (dir * World.Data.HumanHeight * ApproachHumans));
        await WalkNpcTo(npc, goal);
        npc.FaceTowards(Player.GlobalPosition);
        Player.Face(npc.GlobalPosition - Player.GlobalPosition);
    }

    private async Task WalkNpcTo(Npc npc, Vector2 goal)
    {
        if (_skipScene)
        {
            npc.GlobalPosition = goal;
            return;
        }

        var arrived = npc.WalkTo(goal, WalkTimeout / SceneSpeed);
        while (!arrived.IsCompleted)
        {
            if (_skipScene)
            {
                npc.GlobalPosition = goal;
                await npc.WalkTo(npc.GlobalPosition, 0.05f);
                return;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task WalkPlayerTo(Vector2 goal)
    {
        var elapsed = 0f;
        var arriveDistance = 6f * World.Data.ActorScale;
        while (!_skipScene && elapsed < WalkTimeout / SceneSpeed)
        {
            var to = goal - Player.GlobalPosition;
            if (to.Length() <= arriveDistance)
            {
                break;
            }

            Player.ScriptedMove = to.Normalized() * PlayerWalkFactor * Mathf.Min(1.5f, SceneSpeed);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            elapsed += (float)GetPhysicsProcessDeltaTime();
        }

        Player.ScriptedMove = null;
        if (_skipScene || elapsed >= WalkTimeout / SceneSpeed)
        {
            Player.Teleport(goal);
        }
    }

    /// <summary>Espera <paramref name="seconds"/> (dividido por <see cref="SceneSpeed"/>), ou menos se o jogador pular.</summary>
    private async Task SceneWait(float seconds)
    {
        var left = seconds / SceneSpeed;
        while (left > 0f && !_skipScene)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            left -= (float)GetProcessDeltaTime();
        }
    }

    private async Task FadeFrom(float alpha, float seconds)
    {
        _fade.Color = _fade.Color with { A = alpha };
        var tween = CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(_fade, "color:a", 0f, _skipScene ? 0.01f : seconds / SceneSpeed);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>A caixa de fala mostrou uma caixa nova: a câmera olha para o que ela cita e o efeito dispara.</summary>
    private void OnDialogueNode(DialogueNode node)
    {
        if (node.Look.Length > 0)
        {
            PanTo(node.Look);
        }

        switch (node.Fx)
        {
            case "shake":
                World.Camera.AddTrauma(0.85f);
                break;
            case "glow":
                BrightenLantern(animated: true);
                break;
        }
    }

    /// <summary>Move a câmera suavemente para o alvo ("player", "brecha", "lanterna", "mural", id de NPC ou tipo de objeto).</summary>
    public void PanTo(string target)
    {
        if (FocusPoint(target) is not { } point)
        {
            return;
        }

        var camera = World.Camera;
        var from = camera.GlobalPosition;
        var to = camera.ClampPoint(point);
        _pan?.Kill();
        camera.FixedFocus = from;
        _pan = CreateTween();
        _pan.TweenMethod(Callable.From<float>(t => camera.FixedFocus = from.Lerp(to, t)), 0f, 1f, PanSeconds / SceneSpeed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>Devolve a câmera ao jogador (ela segue sozinha de novo).</summary>
    private void ReleaseCamera()
    {
        _pan?.Kill();
        _pan = null;
        if (IsInstanceValid(World))
        {
            World.Camera.FixedFocus = null;
        }
    }

    private Vector2? FocusPoint(string target)
    {
        var human = World.Data.HumanHeight;
        switch (target)
        {
            case "player":
                return PlayerChest;
            case "brecha":
                var zone = World.Zones.FirstOrDefault(z => z.Kind is "brecha" or LevelWorld.SealedBrechaKind);
                return zone is null ? null : zone.GlobalPosition + new Vector2(0, -human * 0.4f);
            case "lanterna":
                return World.Props.FirstOrDefault(p => p.Type == "lanterna_bronze") is { } lantern ? lantern.GlobalPosition + new Vector2(0, -human * 1.2f) : null;
        }

        // Zona sem objeto desenhado que serve de ponto de câmera (ex.: "sino" na Torre).
        if (World.Zones.FirstOrDefault(z => z.Kind == target) is { } spot)
        {
            return spot.GlobalPosition;
        }

        if (World.Npcs.FirstOrDefault(n => n.NpcId == target) is { } npc)
        {
            return npc.GlobalPosition + new Vector2(0, -npc.BodyHeight * 0.5f);
        }

        return World.Props.FirstOrDefault(p => p.Type == target) is { } prop ? prop.GlobalPosition + new Vector2(0, -human * 0.6f) : null;
    }

    /// <summary>A luz da lanterna da praça fica mais forte (o fim do Ato I). Com <paramref name="animated"/> acende devagar.</summary>
    private void BrightenLantern(bool animated)
    {
        if (_lanternGlow is null || !IsInstanceValid(_lanternGlow))
        {
            if (FocusPoint("lanterna") is not { } at)
            {
                return;
            }

            _lanternGlow = new PointLight2D
            {
                Name = "LanternGlow",
                Texture = Vespera.Game.Components.LightTexture.Get(),
                Color = new Color(1f, 0.82f, 0.45f),
                Energy = 0f,
                TextureScale = World.Data.HumanHeight * GlowRadiusHumans / Vespera.Game.Components.LightTexture.Radius,
                GlobalPosition = at,
            };
            World.AddChild(_lanternGlow);
        }

        if (animated)
        {
            CreateTween().TweenProperty(_lanternGlow, "energy", GlowEnergy, 2.0f / SceneSpeed)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        else
        {
            _lanternGlow.Energy = GlowPersistentEnergy;
        }

    }

    /// <summary>Ao montar a Praça depois do fim do Ato I, a lanterna já nasce mais brilhante.</summary>
    private void RestoreLanternGlow()
    {
        if (World.Data.Id == HubLevelId && StoryService.Engine.HasFlag(StoryFlags.Ato1FimDone))
        {
            BrightenLantern(animated: false);
        }
    }

    /// <summary>Interagir ou Esc/Pausa durante uma cena de entrada pula as esperas e as caminhadas.</summary>
    private bool SkipSceneIfBusy()
    {
        if (!_sceneBusy || _freeRoam)
        {
            return false;
        }

        _skipScene = true;
        return true;
    }
}
