using Godot;

namespace Vespera.UI.Widgets;

/// <summary>
/// Barra do HUD (<c>bar()</c>, <c>core.js:126-133</c>): moldura e fundo do tema (<c>UiBar/background</c>), parte "lag"
/// creme que desce devagar (<c>hpLag</c>, <c>lerp dt·2.5</c>, <c>combat.js:51</c>), preenchimento do estilo
/// <see cref="FillStyle"/> e um marcador opcional (custo do Eco na Essência). Só processa enquanto o lag anima.
/// </summary>
[GlobalClass]
public partial class UiBar : Control
{
    private static readonly StringName ThemeType = "UiBar";
    private static readonly StringName Background = "background";
    private static readonly Color LagColor = new(244 / 255f, 231 / 255f, 198 / 255f, 0.55f);
    private static readonly Color MarkerColor = new(244 / 255f, 231 / 255f, 198 / 255f, 0.8f);

    private float _ratio = 1f;
    private float _lag = 1f;
    private float _marker = -1f;
    private StringName _fillStyle = "hp";

    /// <summary>Nome do estilo de preenchimento no tema: hp, energy, essence, xp, boss.</summary>
    [Export]
    public StringName FillStyle
    {
        get => _fillStyle;
        set
        {
            _fillStyle = value;
            QueueRedraw();
        }
    }

    /// <summary>Mostra a parte creme que acompanha a perda com atraso.</summary>
    [Export]
    public bool ShowLag { get; set; }

    /// <summary>Fração 0–1.</summary>
    public float Ratio => _ratio;

    /// <summary>Fração atrasada (≥ <see cref="Ratio"/> enquanto anima).</summary>
    public float LagRatio => _lag;

    public UiBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
    }

    public static UiBar Create(StringName style, Vector2 size, bool lag = false) => new()
    {
        FillStyle = style,
        ShowLag = lag,
        CustomMinimumSize = size,
    };

    /// <summary>Define o valor. Com lag, subir é instantâneo e descer deixa o rastro creme.</summary>
    public void SetRatio(float ratio, bool instant = false)
    {
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        if (Mathf.IsEqualApprox(ratio, _ratio) && !instant)
        {
            return;
        }

        _ratio = ratio;
        if (!ShowLag || instant || _lag < ratio)
        {
            _lag = ratio;
        }

        SetProcess(ShowLag && _lag > _ratio + 0.001f);
        QueueRedraw();
    }

    /// <summary>Marcador vertical em uma fração (ex.: custo do Eco); negativo esconde.</summary>
    public void SetMarker(float ratio)
    {
        if (Mathf.IsEqualApprox(ratio, _marker))
        {
            return;
        }

        _marker = ratio;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var dt = Mathf.Min(0.05f, (float)delta);
        _lag = Mathf.Max(_ratio, Mathf.Lerp(_lag, _ratio, Mathf.Min(1f, dt * 2.5f)));
        if (_lag <= _ratio + 0.001f)
        {
            _lag = _ratio;
            SetProcess(false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawStyleBox(GetThemeStylebox(Background, ThemeType), new Rect2(Vector2.Zero, size));
        if (ShowLag && _lag > _ratio)
        {
            DrawRect(new Rect2(0, 0, size.X * _lag, size.Y), LagColor);
        }

        var width = Mathf.Round(size.X * _ratio);
        if (width > 0)
        {
            DrawStyleBox(GetThemeStylebox(_fillStyle, ThemeType), new Rect2(0, 0, width, size.Y));
        }

        if (_marker >= 0f)
        {
            var x = Mathf.Round(size.X * Mathf.Clamp(_marker, 0f, 1f));
            DrawRect(new Rect2(x, -2, 1, size.Y + 4), MarkerColor);
        }
    }
}
