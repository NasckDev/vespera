using Godot;
using Vespera.Core.World;

namespace Vespera.Autoload;

/// <summary>
/// Escala inteira inteligente da janela (POLI-11, DECISIONS C1-D18). Com <c>scale_mode=integer</c> o Godot escolhe
/// <c>floor(min(janela ÷ base))</c>: numa janela de 1080p com barra de título (1920×1061) isso é 2× e o jogo ocupa
/// 1302×720, com metade da tela preta, a primeira impressão de quem não usa tela cheia. Aqui o tamanho base pedido ao
/// engine acompanha a janela para a escala ser a maior N que ainda deixa ≥ 600×330 u visíveis
/// (<see cref="CameraFraming.FitScale"/>): 1920×1061 passa a 3× com a vista em 640×353 u. A escala continua inteira
/// (pintura e texto nítidos); só a base muda, e o resto do jogo já trabalha com a área visível, não com 640×360 fixos.
/// </summary>
public static class WindowFit
{
    private static Window? _window;

    /// <summary>Escala inteira em uso (px de tela por u).</summary>
    public static int Scale { get; private set; } = 1;

    /// <summary>Passa a acompanhar a janela principal (chamado no boot).</summary>
    public static void Attach(Window window)
    {
        if (_window == window)
        {
            return;
        }

        if (_window is not null && GodotObject.IsInstanceValid(_window))
        {
            _window.SizeChanged -= Apply;
        }

        _window = window;
        window.SizeChanged += Apply;
        Apply();
    }

    private static void Apply()
    {
        // Sem janela de verdade (servidor headless do CI e do smoke) a base do projeto, 640×360, continua valendo.
        if (_window is not { } window || !GodotObject.IsInstanceValid(window) || window.ContentScaleMode != Window.ContentScaleModeEnum.CanvasItems
            || DisplayServer.GetName() == "headless")
        {
            return;
        }

        var size = window.Size;
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }

        Scale = CameraFraming.FitScale(size.X, size.Y);
        var (width, height) = CameraFraming.ContentSizeFor(size.X, size.Y, Scale);
        var wanted = new Vector2I(width, height);
        if (window.ContentScaleSize != wanted)
        {
            window.ContentScaleSize = wanted;
        }
    }
}
