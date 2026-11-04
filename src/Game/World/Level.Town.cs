using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Hunt;
using Vespera.Core.Story;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Story;

namespace Vespera.Game.World;

/// <summary>
/// Lugares novos do Ato I (issue #40): a estrada onde o jogo começa (ROTEIRO-V2 §3, 0:10–0:40: o caçador anda e um
/// Lodo pequeno cruza o caminho), o quarto em cima da taverna (dormir passa a noite; depois de cair na Brecha o caçador
/// acorda ali e ouve a fala de morte, §6.1) e os painéis ilustrados curtos da história (issue #46).
/// </summary>
public partial class Level
{
    public const string RoadLevelId = "estrada";
    public const string RoomLevelId = "quarto";

    /// <summary>A estrada já foi andada (o Lodo da estrada caiu): a chegada na Praça só abre a Brecha.</summary>
    public const string RoadDoneFlag = "estrada_done";

    private const string IllustrationFolder = "res://assets/v2/ilustracoes";
    private const string RiftOpeningSheet = "res://assets/v2/efeitos/fx_brecha_abrindo.png";
    private const int RiftOpeningFrames = 10;
    private const float RiftOpeningFps = 9f;
    private const float RiftOpeningHumans = 2.6f;
    private const float PanelSeconds = 3.5f;
    private const float PanelFade = 0.45f;
    private const float RoadLodoAheadHumans = 3.2f;
    private const float RoadLodoSideHumans = 1.6f;
    private const float DeathVisitorSideHumans = 1.2f;
    private static readonly Vector2 RoomWakeSpawn = new(560, 650);
    private static readonly Vector2 RoomStairs = new(1300, 520);

    /// <summary>Cair numa Brecha da Praça acorda no quarto (falso nos bots de combate, que esperam voltar à Praça).</summary>
    public static bool WakeInRoom { get; set; } = true;

    private static bool _sleptInRoom;
    private static CharacterLine? _pendingDeathLine;

    /// <summary>Na estrada, antes da chegada: tela preta, o caçador aparece andando e um Lodo pequeno cruza o caminho.</summary>
    private void ScheduleRoad()
    {
        var story = StoryService.Engine;
        if (World.Data.Id != RoadLevelId || StoryService.SuppressAutoplay || story.HasFlag(StoryFlags.ArrivalDone) || story.HasFlag(RoadDoneFlag))
        {
            return;
        }

        _fade.Color = _fade.Color with { A = 1f };
        GetTree().CreateTimer(AutoplayDelay).Timeout += () => _ = RunRoad();
    }

    private async Task RunRoad()
    {
        if (_sceneBusy || !IsInsideTree())
        {
            return;
        }

        _sceneBusy = true;
        _skipScene = false;
        try
        {
            StoryService.SetFlag(StoryFlags.StarterBlade);
            Player.Face(Vector2.Up);
            await FadeFrom(1f, ArrivalFadeSeconds);
            _freeRoam = true;
            Player.Frozen = false;
            var human = World.Data.HumanHeight;
            var lodo = new Vespera.Game.Actors.Enemies.Lodo(World)
            {
                Position = World.ClampToFloor(Player.GlobalPosition + new Vector2(-human * RoadLodoSideHumans, -human * RoadLodoAheadHumans)),
            };
            World.AddEnemy(lodo);
            await WaitUntil(() => !IsInstanceValid(lodo) || lodo.IsQueuedForDeletion() || lodo.Dead, RoadLodoSeconds, () => KillIfAlive(lodo));
            StoryService.SetFlag(RoadDoneFlag);
        }
        catch (System.Exception ex)
        {
            Log.Error($"cena da estrada falhou: {ex.Message}");
        }
        finally
        {
            _sceneBusy = false;
            _freeRoam = false;
        }
    }

    /// <summary>Vindo da estrada (o Lodo já caiu lá): o caçador sobe a escadaria sul, anda até o meio e a Brecha abre.</summary>
    private async Task<bool> ArriveFromRoad()
    {
        if (!StoryService.Engine.HasFlag(RoadDoneFlag))
        {
            return false;
        }

        Player.Frozen = true;
        await WalkPlayerTo(World.SpawnPoint);
        await OpenRift();
        return true;
    }

    /// <summary>A Brecha abrindo na Praça (issue #45): a fenda cresce em 10 quadros no lugar do portal e fica no último.</summary>
    private void PlayRiftOpening(Vector2 at)
    {
        if (!ResourceLoader.Exists(RiftOpeningSheet))
        {
            return;
        }

        var texture = GD.Load<Texture2D>(RiftOpeningSheet);
        var cell = new Vector2(texture.GetWidth() / (float)RiftOpeningFrames, texture.GetHeight());
        var frames = new SpriteFrames();
        frames.SetAnimationLoop("default", false);
        frames.SetAnimationSpeed("default", RiftOpeningFps * SceneSpeed);
        for (var i = 0; i < RiftOpeningFrames; i++)
        {
            frames.AddFrame("default", new AtlasTexture { Atlas = texture, Region = new Rect2(i * cell.X, 0, cell.X, cell.Y) });
        }

        var sprite = new AnimatedSprite2D
        {
            Name = "RiftOpening",
            SpriteFrames = frames,
            Scale = Vector2.One * (World.Data.HumanHeight * RiftOpeningHumans / cell.Y),
            GlobalPosition = at,
            ZIndex = 2,
        };
        World.AddChild(sprite);
        sprite.Play();
        sprite.AnimationFinished += () =>
        {
            var fade = sprite.CreateTween();
            fade.TweenProperty(sprite, "modulate:a", 0f, 1.2f / SceneSpeed);
            fade.TweenCallback(Callable.From(sprite.QueueFree));
        };
    }

    /// <summary>
    /// Painel ilustrado 16:9 (issue #46) por cima de tudo, com fade, por <see cref="PanelSeconds"/>: a Brecha abrindo, o
    /// selo D, a Odete na forja, a maçã roxa. Não bloqueia nada (a fala continua por baixo).
    /// </summary>
    private void ShowIllustration(string name)
    {
        var path = $"{IllustrationFolder}/{name}.png";
        if (!ResourceLoader.Exists(path))
        {
            Log.Error($"ilustração '{name}' não existe em {IllustrationFolder}");
            return;
        }

        GetNodeOrNull<Control>("Illustration")?.QueueFree();
        var panel = new TextureRect
        {
            Name = "Illustration",
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = Colors.Transparent,
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var tween = panel.CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(panel, "modulate:a", 1f, PanelFade);
        tween.TweenInterval(PanelSeconds / SceneSpeed);
        tween.TweenProperty(panel, "modulate:a", 0f, PanelFade);
        tween.TweenCallback(Callable.From(panel.QueueFree));
        Log.Info($"ilustração: {name}");
    }

    /// <summary>Dormir na cama do quarto: só quando a manhã é o próximo passo da história (senão "ainda não é hora").</summary>
    private void Sleep(Components.Interactable bed)
    {
        if (StoryService.Engine.AutoplayFor(HubLevelId)?.Stage != "morning")
        {
            OpenReading(bed, string.Empty, freeze: false);
            return;
        }

        _ = SleepNight();
    }

    private async Task SleepNight()
    {
        _sceneBusy = true;
        _skipScene = false;
        Player.Frozen = true;
        try
        {
            await FadeTo(1f);
            _sleptInRoom = true;
            Player.Teleport(RoomWakeSpawn);
            await SceneWait(MorningHold);
            await FadeFrom(1f, ArrivalFadeSeconds);
            CombatHud.ShowTitle(World.Data.Name, "Manhã");
        }
        finally
        {
            _sceneBusy = false;
            Player.Frozen = false;
        }
    }

    /// <summary>A manhã da Praça depois de dormir no quarto: a noite já passou lá em cima, só tira o preto.</summary>
    private async Task<bool> WokeInRoom()
    {
        if (!_sleptInRoom)
        {
            return false;
        }

        _sleptInRoom = false;
        await FadeFrom(1f, ArrivalFadeSeconds * 0.5f);
        return true;
    }

    /// <summary>Caiu numa Brecha da Praça: acorda no quarto (ROTEIRO-V2 §6.1). Devolve a fase/ponto de volta.</summary>
    private static (string Hub, Vector2? Spawn) DeathReturn(string hub, Vector2? spawn, Hunter.RunSummary? summary)
    {
        if (!WakeInRoom || hub != HubLevelId || summary is null || summary.Report.Survived)
        {
            return (hub, spawn);
        }

        _pendingDeathLine = summary.Line;
        return (RoomLevelId, RoomWakeSpawn);
    }

    /// <summary>No quarto, depois da tela de resultado: quem disse a fala de morte sobe a escada e fala ali.</summary>
    private async Task PlayDeathLine()
    {
        if (_pendingDeathLine is not { } line || World.Data.Id != RoomLevelId)
        {
            return;
        }

        _pendingDeathLine = null;
        var speaker = SpeakerKey(line.Who);
        Npc? visitor = null;
        if (speaker.Length > 0 && ScaleRegistry.Actors.ContainsKey(speaker))
        {
            var human = World.Data.HumanHeight;
            var placement = new NpcPlacement { Id = speaker, At = [RoomStairs.X, RoomStairs.Y], Routine = "idle" };
            visitor = new Npc(speaker, placement, human) { Name = "DeathVisitor", Position = RoomStairs };
            World.Entities.AddChild(visitor);
            visitor.Interaction.Enabled = false;
            _sceneBusy = true;
            try
            {
                var goal = World.ClampToFloor(Player.GlobalPosition + new Vector2(human * DeathVisitorSideHumans, 0));
                await WalkNpcTo(visitor, goal);
                visitor.FaceTowards(Player.GlobalPosition);
                Player.Face(visitor.GlobalPosition - Player.GlobalPosition);
            }
            finally
            {
                _sceneBusy = false;
            }
        }

        var file = new DialogueFile { Id = "morte_quarto" };
        file.Entries.Add(new DialogueEntry { Node = "a" });
        file.Nodes["a"] = new DialogueNode { Speaker = speaker, Text = line.Text, Label = speaker.Length == 0 ? line.Who : null };
        var runner = new DialogueRunner(file, StoryService.Engine, StoryService.PlayerName, StoryService.WeaponTokens());
        if (!runner.Start() || _box.IsOpen)
        {
            return;
        }

        CloseReading();
        Player.Frozen = true;
        _speaking = visitor;
        visitor?.Hold(true, Player.GlobalPosition);
        var deviceId = (Player.Input as MergedInput)?.DeviceId ?? Player.Input.DeviceId;
        _box.Open(runner, StoryService.Script, InteractionHud.InteractKeyName(deviceId));
    }

    /// <summary>"Nilza" → "nilza", "Benício" → "benicio" (chave de story.json); vazio se não é falante cadastrado.</summary>
    private static string SpeakerKey(string who)
    {
        var key = who.Trim().ToLowerInvariant().Replace("í", "i").Replace("é", "e").Replace("á", "a");
        return StoryService.Script.Speakers.ContainsKey(key) ? key : string.Empty;
    }
}
