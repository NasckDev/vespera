using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Base das cenas em vídeo (abertura e prólogo): toca um .ogv em tela cheia, respeitando o volume de música das Opções,
/// mostra a dica "pressione qualquer tecla para pular" depois de <see cref="SkipDelaySeconds"/> e encerra por fim natural ou pulo.
/// </summary>
public abstract partial class VideoCutscene : Control
{
    protected const double SkipDelaySeconds = 0.5;
    private const float HintFadeSeconds = 0.6f;
    private const float AudioFadeSeconds = 0.25f;
    private const float SilentDb = -60f;

    private VideoStreamPlayer? _player;
    private Label? _hint;
    private double _age;
    private bool _ended;

    protected abstract string VideoPath { get; }

    protected abstract bool ShowSkipHint { get; }

    /// <summary>Chamado uma única vez quando o vídeo acaba sozinho (<paramref name="skipped"/> falso) ou é pulado.</summary>
    protected abstract void OnEnded(bool skipped);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(black);

        var stream = GD.Load<VideoStream>(VideoPath);
        if (stream is null)
        {
            Log.Error($"Vídeo '{VideoPath}' não encontrado; pulando a cena.");
            CallDeferred(nameof(FinishNow));
            return;
        }

        _player = new VideoStreamPlayer
        {
            Stream = stream,
            Expand = true,
            Autoplay = false,
            VolumeDb = VolumeDbFromSettings(),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _player.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _player.Finished += () => End(skipped: false);
        AddChild(_player);
        AddExtras();

        if (ShowSkipHint)
        {
            _hint = UiKit.MakeLabel("pressione qualquer tecla para pular", UiTheme.DimVariation, HorizontalAlignment.Right);
            _hint.Position = new Vector2(1920 - 36 - 760, 1080 - 36 - 56);
            _hint.Size = new Vector2(760, 56);
            _hint.Modulate = Colors.Transparent;
            AddChild(_hint);
        }

        _player.Play();
    }

    public override void _Process(double delta)
    {
        _age += delta;
        if (_hint is null || _ended)
        {
            return;
        }

        var t = (float)Mathf.Clamp((_age - SkipDelaySeconds) / HintFadeSeconds, 0.0, 1.0);
        _hint.Modulate = new Color(1f, 1f, 1f, t * 0.9f);
    }

    public override void _Input(InputEvent @event)
    {
        if (_ended || _age < SkipDelaySeconds || !IsSkipEvent(@event))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        FadeAudio();
        End(skipped: true);
    }

    /// <summary>Ponto de extensão para sobrepor elementos ao vídeo (ex.: a imagem final da abertura).</summary>
    protected virtual void AddExtras()
    {
    }

    protected void StopPlayer()
    {
        if (_player is not null && IsInstanceValid(_player))
        {
            _player.Stop();
        }
    }

    protected void FadeAudio()
    {
        if (_player is null || !IsInstanceValid(_player))
        {
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(_player, "volume_db", SilentDb, AudioFadeSeconds);
    }

    private static bool IsSkipEvent(InputEvent @event) => @event switch
    {
        InputEventKey key => key.Pressed && !key.Echo,
        InputEventMouseButton mouse => mouse.Pressed,
        InputEventJoypadButton pad => pad.Pressed,
        _ => false,
    };

    private static float VolumeDbFromSettings()
    {
        var percent = GameSettings.Current.MusicVolume;
        return percent <= 0 ? SilentDb : Mathf.LinearToDb(percent / 100f);
    }

    private void FinishNow() => End(skipped: false);

    private void End(bool skipped)
    {
        if (_ended)
        {
            return;
        }

        _ended = true;
        if (_hint is not null)
        {
            _hint.Visible = false;
        }

        OnEnded(skipped);
    }
}
