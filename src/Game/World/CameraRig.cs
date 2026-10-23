using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.World;

/// <summary>
/// Câmera da fase. O zoom NÃO é escolhido por mapa: sai da régua (PLANO-V2 §2.1) para o caçador ter
/// <see cref="HunterScreenHeight"/> px na tela de 1080p em todo mapa (zoom = 110 ÷ altura humana do mapa em px de
/// mundo). Segue o jogador com suavização e olha à frente do movimento; fica presa ao chão pintado e, se o mapa for menor
/// que a tela nesse zoom, centraliza o eixo (o resto vira moldura escura, ver <see cref="LevelVignette"/>).
/// O alvo fica no peito do caçador (não no pé), para o enquadramento não cortar a cabeça perto das bordas.
/// No co-op local a câmera é compartilhada: centra no grupo e abre o zoom só o necessário para todos caberem, até o piso
/// em que o caçador ainda tem 85 px na tela (<see cref="Vespera.Core.Coop.CoopFraming"/>); passado isso, vale a coleira.
/// </summary>
public partial class CameraRig : Camera2D
{
    /// <summary>Altura do caçador na tela de 1080p (px), igual em todos os mapas.</summary>
    public const float HunterScreenHeight = 110f;

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

    private const float PartyZoomSmoothing = 2.2f;

    private readonly IReadOnlyList<Player> _targets;
    private readonly float _actorScale;
    private readonly float _humanHeight;
    private readonly Rect2 _bounds;
    private float _partyFactor = 1f;
    private Vector2 _lead;
    private Vector2 _center;
    private float _trauma;
    private float _shakeTime;
    private float _zoomFactor = 1f;
    private Tween? _pull;

    /// <param name="humanHeight">Altura humana do mapa em px de mundo (0,75 × porta pintada).</param>
    public CameraRig(Player target, Rect2 bounds, float actorScale, float humanHeight)
        : this([target], bounds, actorScale, humanHeight)
    {
    }

    /// <param name="targets">Caçadores enquadrados (lista viva: o co-op pode pôr e tirar jogadores).</param>
    public CameraRig(IReadOnlyList<Player> targets, Rect2 bounds, float actorScale, float humanHeight)
    {
        _targets = targets;
        _actorScale = actorScale;
        _humanHeight = humanHeight;
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
            ApplyZoom();
        }
    }

    /// <summary>Co-op: há mais de um caçador para enquadrar.</summary>
    public bool Shared => _targets.Count > 1;

    /// <summary>Fator do zoom aberto pelo grupo (1 = regra; até <see cref="PartyFloor"/>).</summary>
    public float PartyFactor => _partyFactor;

    /// <summary>Piso do zoom no co-op: o caçador nunca fica com menos de <c>minHunterPx</c> (85) na tela.</summary>
    public static float PartyFloor => (float)Coop.CoopSession.Rules.MinZoomFactor(HunterScreenHeight);

    /// <summary>Folga até a borda da tela no co-op, em px de mundo.</summary>
    public float PartyMargin => (float)Coop.CoopSession.Rules.Camera.EdgeMargin * _humanHeight;

    /// <summary>Distância máxima entre caçadores (por eixo, px de mundo) que cabe na tela no piso do zoom: o limite da coleira.</summary>
    public Vector2 PartySpan
    {
        get
        {
            var span = Vespera.Core.Coop.CoopFraming.MaxSpan(ToNumerics(GetViewportRect().Size), BaseZoom, PartyFloor, PartyMargin);
            return new Vector2(span.X, span.Y);
        }
    }

    /// <summary>Ponto fixo para a câmera (capturas de revisão); null = segue o jogador.</summary>
    public Vector2? FixedFocus { get; set; }

    /// <summary>Zoom pela régua: 110 px de caçador na tela ÷ altura humana do mapa em px de mundo.</summary>
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

        // Solo (inclusive logo depois de o J2 sair) volta ao zoom da regra; co-op enquadra o grupo na hora.
        _partyFactor = Shared ? Frame().ZoomFactor : 1f;
        ApplyZoom();

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
        if (Shared)
        {
            _partyFactor = Mathf.Lerp(_partyFactor, Frame().ZoomFactor, Mathf.Min(1f, PartyZoomSmoothing * dt));
            ApplyZoom();
        }

        var wanted = (LeadVelocity() * LookAhead).LimitLength(MaxLead * _actorScale);
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

    private Vector2 Focus()
    {
        if (!Shared)
        {
            return Chest(_targets[0]);
        }

        var c = Frame().Center;
        return new Vector2(c.X, c.Y);
    }

    private Vector2 Chest(Player p) => p.GlobalPosition + new Vector2(0, -ChestHeight * _actorScale);

    /// <summary>Velocidade média dos caçadores de pé (o olhar à frente do grupo).</summary>
    private Vector2 LeadVelocity()
    {
        if (!Shared)
        {
            return _targets[0].Velocity;
        }

        var sum = Vector2.Zero;
        var n = 0;
        foreach (var p in _targets)
        {
            if (IsInstanceValid(p) && !p.Combat.IsDead)
            {
                sum += p.Velocity;
                n++;
            }
        }

        return n == 0 ? Vector2.Zero : sum / n;
    }

    /// <summary>Enquadramento do grupo (peito de cada caçador, caídos inclusos: o aliado precisa vê-los para reanimar).</summary>
    private Vespera.Core.Coop.Framing Frame()
    {
        var points = new List<System.Numerics.Vector2>(_targets.Count);
        foreach (var p in _targets)
        {
            if (IsInstanceValid(p))
            {
                points.Add(ToNumerics(Chest(p)));
            }
        }

        return Vespera.Core.Coop.CoopFraming.Frame(points, ToNumerics(GetViewportRect().Size), BaseZoom, PartyFloor, PartyMargin);
    }

    /// <summary>Zoom final: regra × afastamento do Guardião; no co-op também × grupo, nunca abaixo do piso de 85 px.</summary>
    private void ApplyZoom()
    {
        var factor = Shared ? Mathf.Max(PartyFloor, _partyFactor * _zoomFactor) : _zoomFactor;
        Zoom = Vector2.One * BaseZoom * factor;
    }

    private static System.Numerics.Vector2 ToNumerics(Vector2 v) => new(v.X, v.Y);
}
