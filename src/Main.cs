using Godot;
using Vespera.Autoload;
using Vespera.Testing;

namespace Vespera;

/// <summary>Cena de boot. Hoje vai direto para Lumeira; o menu inicial entra aqui depois.</summary>
public partial class Main : Node
{
    private const string FirstScene = "res://scenes/World/City/Lumeira.tscn";

    public override void _Ready()
    {
        if (SmokeTest.IsRequested())
        {
            // O runner vive na raiz para sobreviver às trocas de cena.
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, new SmokeTest { Name = "SmokeTest" });
        }

        GameManager.Instance.ChangeScene(FirstScene);
    }
}
