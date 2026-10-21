using Godot;

namespace Vespera.UI;

/// <summary>Interface dentro do jogo (telas modais e serviços da cidade). Esqueleto; completado nas próximas partes.</summary>
public partial class GameUi : CanvasLayer
{
    /// <summary>O título liga antes de entrar no World em um jogo novo: o tutorial da Odete roda após 0,9 s.</summary>
    public static bool PendingTutorial { get; set; }
}
