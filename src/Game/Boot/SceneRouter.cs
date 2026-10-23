using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.UI;

namespace Vespera.Game.Boot;

/// <summary>
/// Autoload que troca a cena principal com fade (<see cref="GoTo"/>) e empilha telas sobrepostas
/// (<see cref="Push(Control)"/> / <see cref="Pop"/>), como Opções e Pausa. As telas são registradas por chave em Boot.
/// </summary>
public partial class SceneRouter : Node
{
    private const float FadeSeconds = 0.16f;
    private const int OverlayLayerIndex = 50;
    private const int FadeLayerIndex = 100;

    private readonly Dictionary<string, Func<Node>> _factories = [];
    private readonly Stack<Control> _overlays = new();
    private Control _stage = null!;
    private Control _overlayRoot = null!;
    private CanvasLayer _overlayLayer = null!;
    private ColorRect _fade = null!;
    private Node? _current;
    private bool _busy;

    public static SceneRouter Instance { get; private set; } = null!;

    public string CurrentKey { get; private set; } = string.Empty;

    public Node? Current => _current;

    public int OverlayDepth => _overlays.Count;

    public bool IsBusy => _busy;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        var stageLayer = new CanvasLayer { Name = "StageLayer", Layer = 0 };
        _stage = NewRoot("Stage", ProcessModeEnum.Pausable);
        stageLayer.AddChild(_stage);
        AddChild(stageLayer);

        _overlayLayer = new CanvasLayer { Name = "Overlays", Layer = OverlayLayerIndex };
        _overlayRoot = NewRoot("OverlayRoot", ProcessModeEnum.Always);
        _overlayLayer.AddChild(_overlayRoot);
        AddChild(_overlayLayer);

        UiSfx.Initialize(this);

        var fadeLayer = new CanvasLayer { Name = "Fade", Layer = FadeLayerIndex };
        _fade = new ColorRect { Color = Palette.Indigo with { A = 0f }, MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        fadeLayer.AddChild(_fade);
        AddChild(fadeLayer);
    }

    /// <summary>Tema aplicado às duas raízes de controles (CanvasLayer não propaga o tema da janela).</summary>
    public void ApplyTheme(Theme theme)
    {
        _stage.Theme = theme;
        _overlayRoot.Theme = theme;
    }

    public void Register(string key, Func<Node> factory) => _factories[key] = factory;

    /// <summary>Troca a cena principal com fade; descarta as telas sobrepostas e despausa a árvore.</summary>
    public async void GoTo(string key)
    {
        if (_busy)
        {
            return;
        }

        if (!_factories.TryGetValue(key, out var factory))
        {
            Log.Error($"Cena '{key}' não registrada.");
            return;
        }

        _busy = true;
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeTo(1f);

        while (_overlays.Count > 0)
        {
            _overlays.Pop().QueueFree();
        }

        GetTree().Paused = false;
        _current?.QueueFree();
        _current = factory();
        CurrentKey = key;
        Vespera.Game.Audio.MusicDirector.Play(key);
        _stage.AddChild(_current);

        await FadeTo(0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _busy = false;
    }

    /// <summary>
    /// Troca a cena principal com fundido cruzado (a nova aparece por cima da atual, sem passar pelo preto).
    /// Usado na abertura, onde a última imagem do vídeo é o início do título.
    /// </summary>
    public async void CrossfadeTo(string key, float seconds)
    {
        if (_busy)
        {
            return;
        }

        if (!_factories.TryGetValue(key, out var factory))
        {
            Log.Error($"Cena '{key}' não registrada.");
            return;
        }

        _busy = true;
        var previous = _current;
        var next = factory();
        if (next is CanvasItem item)
        {
            item.Modulate = item.Modulate with { A = 0f };
        }

        _current = next;
        CurrentKey = key;
        Vespera.Game.Audio.MusicDirector.Play(key);
        _stage.AddChild(next);

        var tween = CreateTween();
        if (next is CanvasItem fading)
        {
            tween.TweenProperty(fading, "modulate:a", 1f, seconds);
        }

        await ToSignal(tween, Tween.SignalName.Finished);
        previous?.QueueFree();
        _busy = false;
    }

    public void Push(string key)
    {
        if (_factories.TryGetValue(key, out var factory) && factory() is Control screen)
        {
            Push(screen);
            return;
        }

        Log.Error($"Tela '{key}' não registrada ou não é um Control.");
    }

    public void Push(Control screen)
    {
        var focus = GetViewport().GuiGetFocusOwner();
        if (_overlays.Count > 0)
        {
            var below = _overlays.Peek();
            (below as UiScreen)?.RememberFocus(focus);
            below.Visible = false;
        }
        else
        {
            (_current as UiScreen)?.RememberFocus(focus);
        }

        GetViewport().GuiReleaseFocus();
        _overlays.Push(screen);
        _overlayRoot.AddChild(screen);
    }

    public void Pop()
    {
        if (_overlays.Count == 0)
        {
            return;
        }

        var top = _overlays.Pop();
        top.QueueFree();
        GetViewport().GuiReleaseFocus();

        if (_overlays.Count > 0)
        {
            var below = _overlays.Peek();
            below.Visible = true;
            (below as UiScreen)?.RestoreFocus();
        }
        else
        {
            (_current as UiScreen)?.RestoreFocus();
        }
    }

    /// <summary>Verdadeiro se <paramref name="screen"/> é a tela que deve responder ao input agora.</summary>
    public bool IsActive(Node screen) => _overlays.Count > 0 ? _overlays.Peek() == screen : _current == screen;

    public void QuitGame() => GetTree().Quit(0);

    private static Control NewRoot(string name, ProcessModeEnum mode)
    {
        var root = new Control { Name = name, ProcessMode = mode, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return root;
    }

    private async System.Threading.Tasks.Task FadeTo(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "color:a", alpha, FadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
