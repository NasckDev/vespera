using Godot;

namespace Vespera.Game.World;

/// <summary>
/// Ponto único de entrada no jogo: "Nova história", "Continuar" e (depois) o fim do vídeo do prólogo vão todos para
/// a cena registrada em <c>ScreenKeys.Game</c>, que é criada aqui. Trocar a primeira fase = mudar <see cref="StartLevelId"/>.
/// </summary>
public static class GameEntry
{
    public const string StartLevelId = "praca";

    public static Node CreateStartScene() => Level.Load(StartLevelId);
}
