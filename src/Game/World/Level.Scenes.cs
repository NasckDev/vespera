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
    private const float ArrivalStartHumans = 4.5f;
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
    private PointLight2D? _lanternGlow;

    /// <summary>Uma cena de entrada está rodando (o jogador não controla nada).</summary>
    public bool SceneBusy => _sceneBusy;

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
        var npc = auto.Npc.Length > 0 ? World.Npcs.FirstOrDefault(n => n.NpcId == auto.Npc) : null;
        if (auto.Dialogue == "chegada")
        {
            await StageArrival();
        }
        else if (npc is not null)
        {
            await StageApproach(npc);
        }

        _sceneBusy = false;
        if (!StartDialogue(auto.Dialogue, npc))
        {
            Player.Frozen = false;
        }
    }

    /// <summary>Chegada: tela preta, a grande lanterna, e o caçador sobe a escadaria até o meio da praça.</summary>
    private async Task StageArrival()
    {
        var human = World.Data.HumanHeight;
        var start = World.ClampToFloor(World.SpawnPoint + new Vector2(0, human * ArrivalStartHumans));
        Player.Teleport(start);
        Player.Face(Vector2.Up);
        World.Camera.FixedFocus = World.Camera.ClampPoint(FocusPoint("lanterna") ?? PlayerChest);
        await FadeFrom(1f, ArrivalFadeSeconds);
        await SceneWait(ArrivalHold);
        World.Camera.FixedFocus = null;
        await WalkPlayerTo(World.SpawnPoint);
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
                var zone = World.Zones.FirstOrDefault(z => z.Kind == "brecha");
                return zone is null ? null : zone.GlobalPosition + new Vector2(0, -human * 0.4f);
            case "lanterna":
                return World.Props.FirstOrDefault(p => p.Type == "lanterna_bronze") is { } lantern ? lantern.GlobalPosition + new Vector2(0, -human * 1.2f) : null;
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
        if (!_sceneBusy)
        {
            return false;
        }

        _skipScene = true;
        return true;
    }
}
