using Godot;
using Vespera.Autoload;
using Vespera.Testing;

namespace Vespera;

/// <summary>
/// Cena de boot: abre o título (spec 01 §1.4). No smoke test vai direto para o World, como antes, para os cenários
/// de todas as frentes começarem no mundo; o cenário do título abre o título por conta própria (DECISIONS B3-D39).
/// </summary>
public partial class Main : Node
{
    private const string WorldScene = "res://scenes/World/World.tscn";

    public override void _Ready()
    {
        GameSettings.Load();
        if (SmokeTest.IsRequested())
        {
            // O runner vive na raiz para sobreviver às trocas de cena.
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, new SmokeTest { Name = "SmokeTest" });
            GameManager.Instance.ChangeScene(WorldScene);
            return;
        }

        // Galeria de objetos (depuração, DECISIONS B5-4): só com -- --object-gallery[=tema]; senão, o título.
        var gallery = System.Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith(Debug.ObjectGallery.CommandLineFlag, System.StringComparison.Ordinal));
        GameManager.Instance.ChangeScene(gallery ? Debug.ObjectGallery.ScenePath : UI.Title.TitleScreen.ScenePath);
    }
}
