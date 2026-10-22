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

    /// <summary>
    /// Por eixo: mapa menor (ou igual) à vista → centraliza (offset negativo); senão limita às bordas.
    /// <paramref name="endMargin"/> (u) é uma faixa no fim do eixo coberta pelo HUD (a hotbar embaixo): a câmera pode
    /// passar da borda do mapa por essa folga, para o jogador que anda até a borda não ficar escondido atrás dela (POLI-9).
    /// </summary>
    public static float AxisTarget(float imageSize, float viewSize, float focus, float endMargin = 0f)
    {
        var margin = Math.Max(0f, endMargin);
        return imageSize + margin <= viewSize
            ? (imageSize - viewSize) / 2f
            : Math.Clamp(focus - (viewSize / 2f), 0f, imageSize - viewSize + margin);
    }

    /// <summary>Canto superior esquerdo alvo da câmera (com folga no pé da vista para o HUD, <paramref name="bottomMargin"/>).</summary>
    public static MapPoint Target(float imageWidth, float imageHeight, float viewWidth, float viewHeight, MapPoint focus, float bottomMargin = 0f) =>
        new(AxisTarget(imageWidth, viewWidth, focus.X), AxisTarget(imageHeight, viewHeight, focus.Y, bottomMargin));

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

    /// <summary>Viewport base do projeto (u visíveis a zoom 1 numa tela 16:9 com escala inteira).</summary>
    public const int BaseWidth = 640;

    public const int BaseHeight = 360;

    /// <summary>
    /// Escala inteira da janela (DECISIONS D39): quantos px de tela por u. 3 em 1080p, 4 em 1440p, 6 em 4K; nunca abaixo de 1.
    /// É a conta do Godot com <c>scale_mode=integer</c> e <c>aspect=expand</c>.
    /// </summary>
    public static int IntegerScale(int windowWidth, int windowHeight, int baseWidth = BaseWidth, int baseHeight = BaseHeight) =>
        Math.Max(1, Math.Min(windowWidth / baseWidth, windowHeight / baseHeight));

    /// <summary>Menor área visível (u) que a escala inteira aceita: no máximo ~6% menos largura e ~8% menos altura que 640×360.</summary>
    public const float MinVisibleFitWidth = 600f;

    public const float MinVisibleFitHeight = 330f;

    /// <summary>
    /// Escala inteira que a janela usa (POLI-11): a maior N com área visível ≥ 600×330 u. A conta pura do Godot
    /// (<see cref="IntegerScale"/>) cai para 2× numa janela de 1920×1061 (a de 1080p com barra de título), renderiza em
    /// 1302×720 e deixa metade da tela preta; aqui ela fica em 3× e a vista encolhe de 360 para 353 u.
    /// </summary>
    public static int FitScale(int windowWidth, int windowHeight)
    {
        var n = 1;
        while (windowWidth / (float)(n + 1) >= MinVisibleFitWidth && windowHeight / (float)(n + 1) >= MinVisibleFitHeight)
        {
            n++;
        }

        return n;
    }

    /// <summary>
    /// Tamanho base (u) a pedir ao Godot para a escala inteira ser <paramref name="scale"/>: um pouco menor que
    /// janela ÷ escala, para a divisão da janela pelo base ficar sempre acima da escala e abaixo da seguinte.
    /// </summary>
    public static (int Width, int Height) ContentSizeFor(int windowWidth, int windowHeight, int scale)
    {
        var n = Math.Max(1, scale);
        if (windowWidth % n == 0 && windowHeight % n == 0)
        {
            return (windowWidth / n, windowHeight / n); // janela múltipla exata (1280×720, 1920×1080…): sem faixa nenhuma, como antes
        }

        var divisor = n + 0.02f;
        return (Math.Max(1, (int)MathF.Floor(windowWidth / divisor)), Math.Max(1, (int)MathF.Floor(windowHeight / divisor)));
    }

    /// <summary>Arredonda ao px de tela (1/<paramref name="pixelsPerUnit"/> u), como <c>floor(x + 0,5)</c>.</summary>
    public static float SnapToPixel(float value, float pixelsPerUnit) =>
        pixelsPerUnit <= 0f ? value : MathF.Floor((value * pixelsPerUnit) + 0.5f) / pixelsPerUnit;

    /// <summary>
    /// Fator de seguimento independente do FPS: <c>1 − e^(−taxa·dt)</c>. A 60 FPS e taxa 7 dá 0,110 (a v2 usava
    /// <c>min(1, dt·7)</c> = 0,117); a 144 FPS o movimento é o mesmo, só que em mais passos.
    /// </summary>
    public static float SmoothFactor(float dt, float rate = FollowRate) => dt <= 0f ? 0f : 1f - MathF.Exp(-rate * dt);

    /// <summary>Foco com deslocamento vertical próprio (personagens maiores que o da v2 olham mais alto, D41).</summary>
    public static MapPoint Focus(MapPoint player, MapPoint? boss, float focusOffsetY)
    {
        var f = Focus(new MapPoint(player.X, player.Y - FocusOffsetY + focusOffsetY), boss);
        return f;
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
