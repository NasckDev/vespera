namespace Vespera.Tests.Core.Support;

using Vespera.Core.Data;

/// <summary>Banco de dados real do jogo (src/Data/*.json copiado para a saída do teste), carregado uma vez.</summary>
public static class TestDb
{
    private static readonly Lazy<GameDatabase> Lazy = new(() => GameDatabase.Load(ReadFile));

    public static GameDatabase Instance => Lazy.Value;

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string ReadFile(string fileName) => File.ReadAllText(Path.Combine(DataDirectory, fileName));

    /// <summary>Carrega o banco com um arquivo alterado por <paramref name="patch"/> (para testar a validação).</summary>
    public static GameDatabase LoadPatched(string fileName, Func<string, string> patch)
        => GameDatabase.Load(name => name == fileName ? patch(ReadFile(name)) : ReadFile(name));
}
