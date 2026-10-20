namespace Vespera.Core.World;

/// <summary>
/// Enquadramento da câmera da v2 (spec 01 §3 e §9.1; <c>main.js:79,112</c>, <c>world.js:24-30</c>).
/// A câmera é o canto superior esquerdo da vista, em u.
/// </summary>
public static class CameraFraming
{
    /// <summary>Largura visível mínima em u.</summary>
    public const float MinVisibleWidth = 380f;

    /// <summary>Taxa do seguimento: <c>cam += (alvo - cam) · min(1, dt·7)</c>.</summary>
    public const float FollowRate = 7f;

    /// <summary>O foco é o jogador 14 u acima do pé.</summary>
    public const float FocusOffsetY = -14f;

    /// <summary>Altura de tela de referência para converter o tremor da v2 (px de tela) em u.</summary>
    public const float ShakeReferenceScreenHeight = 720f;

    /// <summary>
    /// Zoom (px de viewport por u): altura visível = <paramref name="viewHeight"/> u; se a largura visível ficar
    /// abaixo de <see cref="MinVisibleWidth"/>, o zoom reduz para caber 380 u.
    /// </summary>
    public static float Zoom(float viewportWidth, float viewportHeight, float viewHeight)
    {
        var zoom = viewportHeight / viewHeight;
        if (viewportWidth / zoom < MinVisibleWidth)
        {
            zoom = viewportWidth / MinVisibleWidth;
        }

        return zoom;
    }

    /// <summary>Foco: jogador (y −14) puxado para o chefe vivo, se houver (<c>lerp</c> 0,3 em x e 0,35 em y−30).</summary>
    public static MapPoint Focus(MapPoint player, MapPoint? boss)
    {
        var fx = player.X;
        var fy = player.Y + FocusOffsetY;
        if (boss is { } b)
        {
            fx = Lerp(fx, b.X, 0.3f);
            fy = Lerp(fy, b.Y - 30f, 0.35f);
        }

        return new MapPoint(fx, fy);
    }

    /// <summary>Por eixo: mapa menor (ou igual) à vista → centraliza (offset negativo); senão limita às bordas.</summary>
    public static float AxisTarget(float imageSize, float viewSize, float focus) =>
        imageSize <= viewSize ? (imageSize - viewSize) / 2f : Math.Clamp(focus - (viewSize / 2f), 0f, imageSize - viewSize);

    /// <summary>Canto superior esquerdo alvo da câmera.</summary>
    public static MapPoint Target(float imageWidth, float imageHeight, float viewWidth, float viewHeight, MapPoint focus) =>
        new(AxisTarget(imageWidth, viewWidth, focus.X), AxisTarget(imageHeight, viewHeight, focus.Y));

    public static float FollowFactor(float dt) => MathF.Min(1f, dt * FollowRate);

    /// <summary>Decaimento do tremor: <c>shake · 0,002^dt</c>, zerado abaixo de 0,2 (<c>main.js:88</c>).</summary>
    public static float DecayShake(float shake, float dt)
    {
        var next = shake * MathF.Pow(0.002f, dt);
        return next < 0.2f ? 0f : next;
    }

    /// <summary>Amplitude do tremor em u. A v2 só desloca acima de 0,3 (<c>render.js:24</c>).</summary>
    public static float ShakeAmplitude(float shake, float viewHeight) =>
        shake > 0.3f ? shake * viewHeight / ShakeReferenceScreenHeight : 0f;

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
