using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.World;

/// <summary>
/// Câmera da fase. O zoom NÃO é escolhido por mapa: sai da régua (PLANO-V2 §2.1) para o caçador ter
/// <see cref="HunterScreenHeight"/> px na tela de 1080p em todo mapa (zoom = 140 ÷ altura humana do mapa em px de
/// mundo). Segue o jogador com suavização e olha à frente do movimento; fica presa ao chão pintado e, se o mapa for menor
/// que a tela nesse zoom, centraliza o eixo (o resto vira moldura escura, ver <see cref="LevelVignette"/>).
/// O alvo fica no peito do caçador (não no pé), para o enquadramento não cortar a cabeça perto das bordas.
/// </summary>
public partial class CameraRig : Camera2D
{
    /// <summary>Altura do caçador na tela de 1080p (px), igual em todos os mapas.</summary>
    public const float HunterScreenHeight = 140f;

    /// <summary>Quanto a câmera pode se afastar no máximo (apresentação do Guardião).</summary>
    public const float MinPullBack = 0.8f;

    private const float LookAhead = 0.45f;
    private const float MaxLead = 140f;
    private const float LeadSmoothing = 3.5f;
    private const float ChestHeight = 60f;
    private const float MaxShake = 26f;
    private const float TraumaDecay = 1.8f;
    private const float FollowSpeed = 6f;
    private const float PullSeconds = 0.5f;

    private readonly Player _target;
    private readonly float _actorScale;
    private readonly Rect2 _bounds;
    private Vector2 _lead;
    private Vector2 _center;
    private float _trauma;
    private float _shakeTime;
    private float _zoomFactor = 1f;
    private Tween? _pull;

    /// <param name="humanHeight">Altura humana do mapa em px de mundo (1,0 × porta pintada).</param>
    public CameraRig(Player target, Rect2 bounds, float actorScale, float humanHeight)
    {
        _target = target;
        _actorScale = actorScale;
        _bounds = bounds;
        BaseZoom = ZoomFor(humanHeight);
        ProcessCallback = Camera2DProcessCallback.Physics;
        Zoom = Vector2.One * BaseZoom;
    }

    /// <summary>Zoom que deixa o caçador com <see cref="HunterScreenHeight"/> px na tela (sem afastamento).</summary>
    public float BaseZoom { get; }

    /// <summary>Multiplicador do zoom (1 = regra; até <see cref="MinPullBack"/> na apresentação do Guardião).</summary>
    public float ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            _zoomFactor = Mathf.Clamp(value, MinPullBack, 1f);
            Zoom = Vector2.One * BaseZoom * _zoomFactor;
        }
    }

    /// <summary>Ponto fixo para a câmera (capturas de revisão); null = segue o jogador.</summary>
    public Vector2? FixedFocus { get; set; }

    /// <summary>Prende um ponto ao chão pintado como a câmera faz (as cenas movem o foco sem mostrar o vazio).</summary>
    public Vector2 ClampPoint(Vector2 point) => Clamp(point);

    /// <summary>Zoom pela régua: 140 px de caçador na tela ÷ altura humana do mapa em px de mundo.</summary>
    public static float ZoomFor(float humanHeight) => humanHeight > 0f ? HunterScreenHeight / humanHeight : 1f;

    public override void _Ready()
    {
        // Os limites da Camera2D alinham à esquerda/topo quando o mapa é menor que a tela; o recorte é feito à mão em Clamp.
        LimitLeft = -10_000_000;
        LimitTop = -10_000_000;
        LimitRight = 10_000_000;
        LimitBottom = 10_000_000;
        PositionSmoothingEnabled = false;
        ProcessCallback = Camera2DProcessCallback.Physics;
        Snap();
    }

    /// <summary>Encaixa a câmera no jogador sem suavizar (início da fase, teleporte).</summary>
    public void Snap()
    {
        _lead = Vector2.Zero;
        _center = Clamp(Focus());
        GlobalPosition = _center;
        ResetSmoothing();
    }

    /// <summary>Tremor que decai (0-1); a intensidade segue a opção "Tremor de tela" (0-100%).</summary>
    public void AddTrauma(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

    /// <summary>Afasta a câmera até <paramref name="factor"/> (mín. 0,8×), segura <paramref name="hold"/> s e volta à regra.</summary>
    public void PullBack(float factor, float hold)
    {
        _pull?.Kill();
        _pull = CreateTween();
        _pull.TweenProperty(this, PropertyName.ZoomFactor.ToString(), Mathf.Max(MinPullBack, factor), PullSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _pull.TweenInterval(hold);
        _pull.TweenProperty(this, PropertyName.ZoomFactor.ToString(), 1f, PullSeconds * 1.4f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _trauma = Mathf.Max(0f, _trauma - (TraumaDecay * dt));
        _shakeTime += dt * 38f;
        var strength = _trauma * _trauma * MaxShake * (GameSettings.Current.ScreenShake / 100f) / Zoom.X;
        Offset = strength <= 0.01f ? Vector2.Zero : new Vector2(
            Mathf.Sin(_shakeTime * 1.13f) + (0.5f * Mathf.Sin(_shakeTime * 2.71f)),
            Mathf.Cos(_shakeTime * 0.97f) + (0.5f * Mathf.Sin(_shakeTime * 2.29f))) * strength * 0.67f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (FixedFocus is { } focus)
        {
            _center = focus;
            GlobalPosition = focus;
            return;
        }

        var dt = (float)delta;
        var wanted = (_target.Velocity * LookAhead).LimitLength(MaxLead * _actorScale);
        _lead = _lead.Lerp(wanted, Mathf.Min(1f, LeadSmoothing * dt));
        var goal = Clamp(Focus() + _lead);
        _center = _center.Lerp(goal, Mathf.Min(1f, FollowSpeed * dt));
        GlobalPosition = Clamp(_center);
    }

    /// <summary>
    /// Prende o centro da câmera ao chão pintado: em cada eixo, se o mapa cabe na tela nesse zoom, fica no meio dele;
    /// senão, a borda da tela não passa da borda do mapa.
    /// </summary>
    private Vector2 Clamp(Vector2 point)
    {
        var half = GetViewportRect().Size / Zoom / 2f;
        return new Vector2(Axis(point.X, _bounds.Position.X, _bounds.End.X, half.X), Axis(point.Y, _bounds.Position.Y, _bounds.End.Y, half.Y));
    }

    private static float Axis(float value, float min, float max, float half) =>
        max - min <= half * 2f ? (min + max) / 2f : Mathf.Clamp(value, min + half, max - half);

    private Vector2 Focus() => _target.GlobalPosition + new Vector2(0, -ChestHeight * _actorScale);
}
