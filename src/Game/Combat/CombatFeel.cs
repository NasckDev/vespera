using System;
using Godot;
using Vespera.Game.Boot;
using Vespera.Game.World;

namespace Vespera.Game.Combat;

/// <summary>
/// "Peso" do combate de uma sala (PLANO-V2 §2): pausa no impacto (escala de tempo global por alguns ms reais),
/// tremor de câmera (respeita a opção de tremor), faíscas, anéis de impacto e pedidos de número de dano para a
/// camada de interface. Um por <see cref="LevelWorld"/>.
/// </summary>
public partial class CombatFeel : Node
{
    private const float StopScale = 0.04f;

    private static GradientTexture2D? _dot;
    private ulong _stopUntil;
    private ulong _slowUntil;
    private float _slowScale = 1f;

    public CameraRig? Camera { get; set; }

    /// <summary>Nó do mundo onde nascem faíscas e anéis (acima das entidades).</summary>
    public Node2D FxLayer { get; set; } = null!;

    /// <summary>Escala de personagens da sala (faíscas e anéis acompanham).</summary>
    public float ActorScale { get; set; } = 1f;

    /// <summary>Pedido de número flutuante: ponto no mundo, texto, cor, tamanho relativo.</summary>
    public event Action<Vector2, string, Color, float>? NumberRequested;

    /// <summary>Mensagem curta para o jogador (núcleo partido, sala limpa...).</summary>
    public event Action<string>? ToastRequested;

    public static bool ReduceFlashes => GameSettings.Current.ReduceFlashes;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _ExitTree() => Engine.TimeScale = 1.0;

    public override void _Process(double delta)
    {
        var now = Time.GetTicksMsec();
        if (_stopUntil > 0 && now >= _stopUntil)
        {
            _stopUntil = 0;
        }

        if (_slowUntil > 0 && now >= _slowUntil)
        {
            _slowUntil = 0;
            _slowScale = 1f;
        }

        Engine.TimeScale = _stopUntil > 0 ? StopScale : _slowScale;
    }

    /// <summary>Congela o jogo por <paramref name="seconds"/> de tempo real (0,04-0,12 s).</summary>
    public void HitStop(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        _stopUntil = Math.Max(_stopUntil, Time.GetTicksMsec() + (ulong)(seconds * 1000));
        Engine.TimeScale = StopScale;
    }

    /// <summary>Câmera lenta (morte do Guardião, queda do caçador).</summary>
    public void SlowMotion(float scale, double seconds)
    {
        _slowScale = scale;
        _slowUntil = Time.GetTicksMsec() + (ulong)(seconds * 1000);
    }

    public void Shake(float trauma) => Camera?.AddTrauma(trauma);

    public void Number(Vector2 world, string text, Color color, float size = 1f) => NumberRequested?.Invoke(world, text, color, size);

    public void Toast(string text) => ToastRequested?.Invoke(text);

    /// <summary>Estouro de faíscas: <paramref name="amount"/> pontos que saem em leque de <paramref name="dir"/>.</summary>
    public void Spark(Vector2 at, Color color, int amount, float speed, Vector2 dir = default, float spread = 180f)
    {
        var s = ActorScale;
        var particles = new CpuParticles2D
        {
            Position = at,
            Emitting = false,
            OneShot = true,
            Explosiveness = 1f,
            Amount = Math.Max(1, amount),
            Lifetime = 0.32f,
            Texture = Dot(),
            Direction = dir == Vector2.Zero ? Vector2.Up : dir.Normalized(),
            Spread = spread,
            InitialVelocityMin = speed * 0.45f * s,
            InitialVelocityMax = speed * s,
            Gravity = new Vector2(0, 260f * s),
            ScaleAmountMin = 0.12f * s,
            ScaleAmountMax = 0.32f * s,
            ColorRamp = Ramp(color),
            ZIndex = 20,
        };
        particles.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        FxLayer.AddChild(particles);
        particles.Emitting = true;
        particles.Finished += particles.QueueFree;
    }

    /// <summary>Anel que se expande no chão (pouso do Guardião, onda do Martelo).</summary>
    public void Ring(Vector2 at, float radius, Color color, float seconds = 0.35f, float width = 6f)
    {
        var ring = new ImpactRing(radius, color, seconds, width * ActorScale) { Position = at };
        FxLayer.AddChild(ring);
    }

    /// <summary>Ponto macio compartilhado (faíscas e brilhos).</summary>
    public static GradientTexture2D Dot()
    {
        if (_dot is not null)
        {
            return _dot;
        }

        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradient.AddPoint(0.45f, new Color(1, 1, 1, 0.8f));
        _dot = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 32,
            Height = 32,
        };
        return _dot;
    }

    public static void ClearCache() => _dot = null;

    private static Gradient Ramp(Color color)
    {
        var ramp = new Gradient();
        ramp.SetColor(0, Colors.White);
        ramp.SetColor(1, color with { A = 0f });
        ramp.AddPoint(0.25f, color);
        return ramp;
    }
}

/// <summary>Anel achatado (perspectiva 3/4) que cresce e some.</summary>
public partial class ImpactRing(float radius, Color color, float seconds, float width) : Node2D
{
    private float _t;

    public override void _Ready() => ZIndex = -4;

    public override void _Process(double delta)
    {
        _t += (float)delta / seconds;
        if (_t >= 1f)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var ease = 1f - Mathf.Pow(1f - _t, 3f);
        var r = radius * (0.25f + (0.75f * ease));
        var alpha = 1f - _t;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.45f));
        DrawCircle(Vector2.Zero, r, color with { A = 0.18f * alpha });
        DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, color with { A = 0.9f * alpha }, width * (1.2f - _t), true);
    }
}
