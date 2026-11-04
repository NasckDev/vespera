using System;
using Godot;
using Vespera.Core.Final;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Ilustração de história em tela cheia (issue #51, H-07 a H-09: quem tocou o sino): entra com fade do preto, fica
/// <see cref="HoldSeconds"/> e sai com fade. Interagir (depois de um instante) pula. Some sozinha da árvore ao terminar.
/// </summary>
public partial class BellScene(string texturePath) : Control
{
    public const float FadeSeconds = 0.9f;
    public const float HoldSeconds = 5f;
    private const float SkipAfter = 0.8f;

    private TextureRect _picture = null!;
    private float _time;
    private bool _leaving;

    /// <summary>Cena em exibição agora (testes).</summary>
    public static BellScene? Active { get; private set; }

    public string TexturePath => texturePath;

    public override void _Ready()
    {
        Active = this;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(black);
        _picture = new TextureRect
        {
            Texture = GD.Load<Texture2D>(texturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.Linear,
            Modulate = Colors.White with { A = 0f },
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_picture);
        Modulate = Colors.White with { A = 0f };
        var tween = CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(this, "modulate:a", 1f, FadeSeconds * 0.5f);
        tween.TweenProperty(_picture, "modulate:a", 1f, FadeSeconds);
    }

    public override void _ExitTree()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (!_leaving && _time >= FadeSeconds + HoldSeconds)
        {
            Leave();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_time >= SkipAfter && (@event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed("ui_accept")))
        {
            GetViewport().SetInputAsHandled();
            Leave();
        }
    }

    /// <summary>Sai com fade para o preto e some (Interagir, o tempo acabou ou o teste pediu).</summary>
    public void Leave()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        var tween = CreateTween().SetIgnoreTimeScale(true);
        tween.TweenProperty(this, "modulate:a", 0f, FadeSeconds);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}

/// <summary>
/// Encerramento da Parte 1 (ROTEIRO-V2 §13 FINAL, fala 8): tela índigo com a última fala da Lis ("Abriu uma no mar."),
/// os créditos curtos e "Interagir: voltar ao título". O save já foi marcado com <see cref="FinalStory.PartOneDoneFlag"/>.
/// </summary>
public partial class PartOneEnd : Control
{
    private const float FadeSeconds = 1.6f;
    private const float ContinueAfter = 2.5f;
    private const float AutoSeconds = 30f;

    private float _time;
    private bool _done;
    private Label _prompt = null!;

    public event Action? Finished;

    public static PartOneEnd? Active { get; private set; }

    public override void _Ready()
    {
        Active = this;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = Palette.Indigo, MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = UiKit.MakeColumn(22);
        column.Alignment = BoxContainer.AlignmentMode.Center;
        center.AddChild(column);

        var hook = UiKit.MakeLabel($"“{FinalStory.Hook}”", UiTheme.TitleVariation, HorizontalAlignment.Center);
        hook.AddThemeColorOverride("font_color", Palette.Amber);
        column.AddChild(hook);
        column.AddChild(UiKit.MakeLabel("Lis, na escada, de manhã. Longe, no mar, uma Brecha nova.", UiTheme.DimVariation, HorizontalAlignment.Center));
        var end = UiKit.MakeLabel("Fim da Parte 1", UiTheme.HeadingVariation, HorizontalAlignment.Center);
        end.AddThemeColorOverride("font_color", Palette.Cream);
        column.AddChild(end);
        column.AddChild(UiKit.MakeLabel("Véspera · um projeto NasckDev · obrigado por jogar", UiTheme.ReadingVariation, HorizontalAlignment.Center));
        _prompt = UiKit.MakeLabel("Interagir: voltar ao título", UiTheme.DimVariation, HorizontalAlignment.Center);
        _prompt.Modulate = Colors.White with { A = 0f };
        column.AddChild(_prompt);

        Modulate = Colors.White with { A = 0f };
        CreateTween().SetIgnoreTimeScale(true).TweenProperty(this, "modulate:a", 1f, FadeSeconds);
    }

    public override void _ExitTree()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_time >= ContinueAfter)
        {
            _prompt.Modulate = Colors.White with { A = Mathf.Min(1f, (_time - ContinueAfter) * 2f) };
        }

        if (_time >= AutoSeconds)
        {
            Finish();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_time >= ContinueAfter && (@event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed("ui_accept")))
        {
            GetViewport().SetInputAsHandled();
            Finish();
        }
    }

    /// <summary>Volta ao título (uma vez só).</summary>
    public void Finish()
    {
        if (_done)
        {
            return;
        }

        _done = true;
        Finished?.Invoke();
    }
}
