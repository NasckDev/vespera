using Godot;
using Vespera.Autoload;
using Vespera.Testing;

namespace Vespera;

/// <summary>Cena de boot. Hoje vai direto para o World (novo jogo da v2: Guilda); o menu inicial entra aqui depois.</summary>
public partial class Main : Node
{
    private const string FirstScene = "res://scenes/World/World.tscn";

    public override void _Ready()
    {
        if (SmokeTest.IsRequested())
        {
            // O runner vive na raiz para sobreviver às trocas de cena.
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, new SmokeTest { Name = "SmokeTest" });
        }

        // Galeria de objetos (depuração, DECISIONS B5-4): só com -- --object-gallery[=tema].
        var gallery = System.Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith(Debug.ObjectGallery.CommandLineFlag, System.StringComparison.Ordinal));
        GameManager.Instance.ChangeScene(gallery ? Debug.ObjectGallery.ScenePath : FirstScene);
    }
}
