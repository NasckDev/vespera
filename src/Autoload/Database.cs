using System.IO;
using Godot;
using Vespera.Core.Data;

namespace Vespera.Autoload;

/// <summary>
/// Carrega os dados do jogo (<c>res://src/Data/*.json</c>, DECISIONS D5) uma única vez, antes do <see cref="GameManager"/>,
/// e expõe o <see cref="GameDatabase"/> validado. Acesse por <c>Database.Instance.Db</c> ou <see cref="Db"/>.
/// </summary>
public partial class Database : Node
{
    public const string DataDirectory = "res://src/Data/";

    private static GameDatabase? _db;

    public static Database Instance { get; private set; } = null!;

    /// <summary>Banco de dados do jogo. Carregado no <c>_EnterTree</c> deste autoload.</summary>
    public static GameDatabase Db => _db ?? throw new System.InvalidOperationException("Database ainda não foi carregado (autoload fora de ordem?).");

    /// <summary>False no editor (sem autoloads) ou se a carga falhou.</summary>
    public static bool IsLoaded => _db is not null;

    public override void _EnterTree()
    {
        Instance = this;
        if (_db is not null)
        {
            return;
        }

        var started = Time.GetTicksMsec();
        try
        {
            _db = GameDatabase.Load(ReadFile);
        }
        catch (GameDataException ex)
        {
            Log.Error(ex.Message);
            throw;
        }

        Log.Info($"Dados carregados: {_db.Monsters.Count} monstros, {_db.Gear.Count} equipamentos, {_db.Materials.Count} materiais ({Time.GetTicksMsec() - started} ms).");
    }

    private static string ReadFile(string fileName)
    {
        var path = DataDirectory + fileName;
        if (!Godot.FileAccess.FileExists(path))
        {
            throw new FileNotFoundException($"Arquivo de dados ausente: {path}");
        }

        return Godot.FileAccess.GetFileAsString(path);
    }
}
