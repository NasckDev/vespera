using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Câmera própria da v2 (spec 01 §3/§9.1, DECISIONS D3): canto superior esquerdo calculado à mão, sem
/// <c>limit_*</c> nem smoothing do Camera2D. Zoom = altura da viewport / (400 u em mapas hd, 300 u nos demais),
/// largura visível mínima de 380 u, seguimento <c>lerp(min(1, dt·7))</c>, mapas menores que a vista ficam
/// centralizados. Tremor via <see cref="Shake"/> ou <see cref="EventBus.CameraShakeRequested"/>; "olhar para" o
/// chefe via <see cref="LookAtTarget"/>.
/// </summary>
[GlobalClass]
public partial class WorldCamera : Camera2D
{
    private const float MaxDelta = 0.05f; // main.js:18

    private readonly Random _shakeRandom = new();
    private Vector2 _topLeft;
    private Vector2 _mapSize = new(640, 360);
    private float _viewHeight = 300f;
    private float _shake;

    /// <summary>Quem a câmera segue (o pé do jogador).</summary>
    [Export]
    public Node2D? Target { get; set; }

    /// <summary>Se definido (chefe vivo), puxa o foco para ele (<c>world.js:26</c>).</summary>
    public Node2D? LookAtTarget { get; set; }

    /// <summary>Tremor atual na escala da v2.</summary>
    public float CurrentShake => _shake;

    /// <summary>Tamanho visível atual em u.</summary>
    public Vector2 VisibleSize => GetViewportRect().Size / Zoom;

    public override void _Ready()
    {
        AnchorMode = AnchorModeEnum.FixedTopLeft;
        PositionSmoothingEnabled = false;
        LimitEnabled = false;
        ProcessCallback = Camera2DProcessCallback.Physics;
        MakeCurrent();
        EventBus.Instance.CameraShakeRequested += Shake;
    }

    public override void _ExitTree() => EventBus.Instance.CameraShakeRequested -= Shake;

    /// <summary>Configura o mapa atual (tamanho da imagem e altura visível em u) e pula direto para o alvo.</summary>
    public void SetMap(Vector2 imageSize, float viewHeight)
    {
        _mapSize = imageSize;
        _viewHeight = viewHeight;
        LookAtTarget = null;
        Snap();
    }

    /// <summary>Pula direto para o alvo (<c>snapCam</c>), sem seguimento.</summary>
    public void Snap()
    {
        UpdateZoom();
        _topLeft = ComputeTarget();
        Apply();
    }

    /// <summary>Tremor: mantém o maior entre o atual e <paramref name="amount"/> (<c>G.shake = max(G.shake, n)</c>).</summary>
    public void Shake(float amount) => _shake = MathF.Max(_shake, amount);

    public void StopShake() => _shake = 0f;

    public override void _PhysicsProcess(double delta)
    {
        var dt = MathF.Min(MaxDelta, (float)delta);
        UpdateZoom();
        var target = ComputeTarget();
        _topLeft = _topLeft.Lerp(target, CameraFraming.FollowFactor(dt));
        _shake = CameraFraming.DecayShake(_shake, dt);
        Apply();
    }

    private void UpdateZoom()
    {
        var viewport = GetViewportRect().Size;
        var zoom = CameraFraming.Zoom(viewport.X, viewport.Y, _viewHeight);
        Zoom = new Vector2(zoom, zoom);
    }

    private Vector2 ComputeTarget()
    {
        if (Target is null)
        {
            return _topLeft;
        }

        var p = Target.GlobalPosition;
        MapPoint? boss = LookAtTarget is { } b && IsInstanceValid(b) ? new MapPoint(b.GlobalPosition.X, b.GlobalPosition.Y) : null;
        var focus = CameraFraming.Focus(new MapPoint(p.X, p.Y), boss);
        var view = VisibleSize;
        var t = CameraFraming.Target(_mapSize.X, _mapSize.Y, view.X, view.Y, focus);
        return new Vector2(t.X, t.Y);
    }

    private void Apply()
    {
        var amplitude = CameraFraming.ShakeAmplitude(_shake, _viewHeight);
        var offset = amplitude > 0f
            ? new Vector2(((float)_shakeRandom.NextDouble() * 2f) - 1f, ((float)_shakeRandom.NextDouble() * 2f) - 1f) * amplitude
            : Vector2.Zero;
        Position = _topLeft + offset;
    }
}
