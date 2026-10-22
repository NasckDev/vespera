using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.UI.Common;

namespace Vespera.World;

/// <summary>Quem a câmera segue e onde ele aparece neste quadro (posição interpolada entre ticks de física).</summary>
public interface ICameraTarget
{
    /// <summary>Pé do alvo como desenhado neste quadro (com interpolação de física).</summary>
    Vector2 VisualPosition { get; }
}

/// <summary>
/// Câmera própria (spec 01 §3/§9.1, DECISIONS D3 → D39): canto superior esquerdo calculado à mão, sem <c>limit_*</c> nem
/// smoothing do Camera2D. Zoom 1 em todos os mapas: com <c>scale_mode=integer</c>, 1 u = N×N px de tela (3 em 1080p,
/// 4 em 1440p, 6 em 4K) e a pintura fica nítida. Segue a posição <b>interpolada</b> do alvo a cada quadro (não a cada
/// tick de física) com fator <c>1 − e^(−7·dt)</c> e arredonda ao px de tela, então mapa, oclusores e sprites andam juntos
/// sem "nadar". Mapas menores que a vista ficam centralizados. Tremor via <see cref="Shake"/> ou
/// <see cref="EventBus.CameraShakeRequested"/>; "olhar para" o chefe via <see cref="LookAtTarget"/>.
/// </summary>
[GlobalClass]
public partial class WorldCamera : Camera2D
{
    private const float MaxDelta = 0.05f; // main.js:18

    private readonly Random _shakeRandom = new();
    private Vector2 _topLeft;
    private Vector2 _mapSize = new(640, 360);
    private float _shake;

    /// <summary>Quem a câmera segue (o pé do jogador). Se implementar <see cref="ICameraTarget"/>, usa a posição interpolada.</summary>
    [Export]
    public Node2D? Target { get; set; }

    /// <summary>Se definido (chefe vivo), puxa o foco para ele (<c>world.js:26</c>).</summary>
    public Node2D? LookAtTarget { get; set; }

    /// <summary>Altura do foco acima do pé, em u (v2: −14 para um personagem de 32 u; cresce com <c>characterScale</c>).</summary>
    public float FocusOffsetY { get; set; } = CameraFraming.FocusOffsetY;

    /// <summary>Tremor atual na escala da v2.</summary>
    public float CurrentShake => _shake;

    /// <summary>Tamanho visível atual em u (zoom 1: o tamanho da viewport; 640×360 numa tela 16:9).</summary>
    public Vector2 VisibleSize => GetViewportRect().Size / Zoom;

    /// <summary>px de tela por u (escala inteira da janela). A posição da câmera é arredondada a 1/<see cref="PixelScale"/> u.</summary>
    public float PixelScale { get; private set; } = 1f;

    /// <summary>Canto superior esquerdo sem arredondar (o seguimento suave trabalha aqui).</summary>
    public Vector2 SmoothTopLeft => _topLeft;

    public override void _Ready()
    {
        AnchorMode = AnchorModeEnum.FixedTopLeft;
        PositionSmoothingEnabled = false;
        LimitEnabled = false;
        Zoom = Vector2.One;
        // A câmera é posta a cada quadro (_Process + ForceUpdateScroll) a partir da posição interpolada do alvo: ela mesma
        // não é interpolada. Com physics_interpolation o Godot exige o callback de física (só muda quando o Camera2D
        // atualiza sozinho; aqui o scroll é forçado no quadro).
        ProcessCallback = Camera2DProcessCallback.Physics;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        MakeCurrent();
        EventBus.Instance.CameraShakeRequested += Shake;
    }

    public override void _ExitTree() => EventBus.Instance.CameraShakeRequested -= Shake;

    /// <summary>Configura o mapa atual (tamanho da imagem em u) e pula direto para o alvo.</summary>
    public void SetMap(Vector2 imageSize, float viewHeight)
    {
        _ = viewHeight; // zoom 1 em todos os mapas (D39); a altura visível da v2 (400/300) não se aplica mais
        _mapSize = imageSize;
        LookAtTarget = null;
        Snap();
    }

    /// <summary>Pula direto para o alvo (<c>snapCam</c>), sem seguimento.</summary>
    public void Snap()
    {
        _topLeft = ComputeTarget();
        Apply();
    }

    /// <summary>Tremor: mantém o maior entre o atual e <paramref name="amount"/> (<c>G.shake = max(G.shake, n)</c>).</summary>
    /// <summary>Pede tremor (mantém o maior). Desligado na opção "Tremor de tela" (<c>OPT.shake</c>, <c>extra.js:162</c>).</summary>
    public void Shake(float amount) => _shake = GameSettings.ScreenShake ? MathF.Max(_shake, amount) : 0f;

    public void StopShake() => _shake = 0f;

    public override void _Process(double delta)
    {
        var dt = MathF.Min(MaxDelta, (float)delta);
        var target = ComputeTarget();
        _topLeft = _topLeft.Lerp(target, CameraFraming.SmoothFactor(dt));
        _shake = CameraFraming.DecayShake(_shake, dt);
        Apply();
    }

    private Vector2 ComputeTarget()
    {
        if (Target is null)
        {
            return _topLeft;
        }

        var p = Target is ICameraTarget visual ? visual.VisualPosition : Target.GlobalPosition;
        MapPoint? boss = LookAtTarget is { } b && IsInstanceValid(b) ? new MapPoint(b.GlobalPosition.X, b.GlobalPosition.Y) : null;
        var focus = CameraFraming.Focus(new MapPoint(p.X, p.Y), boss, FocusOffsetY);
        var view = VisibleSize;
        var t = CameraFraming.Target(_mapSize.X, _mapSize.Y, view.X, view.Y, focus, HudZones.BottomInset); // POLI-9: folga para a hotbar
        return new Vector2(t.X, t.Y);
    }

    private void Apply()
    {
        var stretch = GetViewport().GetStretchTransform().X.X;
        PixelScale = MathF.Max(1f, MathF.Round(stretch));
        var amplitude = CameraFraming.ShakeAmplitude(_shake, VisibleSize.Y) * GameSettings.ShakeIntensity; // C1-D9: 0–100% nas Opções
        var offset = amplitude > 0f
            ? new Vector2(((float)_shakeRandom.NextDouble() * 2f) - 1f, ((float)_shakeRandom.NextDouble() * 2f) - 1f) * amplitude
            : Vector2.Zero;
        var p = _topLeft + offset;
        Position = new Vector2(CameraFraming.SnapToPixel(p.X, PixelScale), CameraFraming.SnapToPixel(p.Y, PixelScale));
        if (IsInsideTree())
        {
            ForceUpdateScroll();
        }
    }
}
