using Godot;
using Vespera.Game.Boot;
using Vespera.Game.UI;

namespace Vespera.Game.World;

/// <summary>
/// Ponto único de entrada no jogo: "Nova história", "Continuar" e (depois) o fim do vídeo do prólogo vão todos para
/// a cena registrada em <c>ScreenKeys.Game</c>, que é criada aqui. A fase aberta é a pedida por <see cref="Travel"/>,
/// senão a última do save (<c>Story.LevelId</c>), senão <see cref="StartLevelId"/>.
/// </summary>
public static class GameEntry
{
    public const string StartLevelId = "praca";

    private static string? _nextLevel;
    private static float[]? _nextSpawn;

    /// <summary>Troca de fase com fade (saídas das ruas e portas). <paramref name="spawn"/> = pé na fase nova; vazio = ponto padrão dela.</summary>
    public static void Travel(string levelId, float[]? spawn)
    {
        _nextLevel = levelId;
        _nextSpawn = spawn is { Length: 2 } ? spawn : null;
        SceneRouter.Instance.GoTo(ScreenKeys.Game);
    }

    public static Node CreateStartScene()
    {
        var id = _nextLevel ?? SavedLevelId();
        var spawn = _nextSpawn;
        _nextLevel = null;
        _nextSpawn = null;
        return Level.Load(id, spawn);
    }

    private static string SavedLevelId()
    {
        var saved = GameSession.Data?.Story.LevelId;
        return !string.IsNullOrEmpty(saved) && Godot.FileAccess.FileExists($"{Level.LevelsFolder}/{saved}.json") ? saved : StartLevelId;
    }
}
