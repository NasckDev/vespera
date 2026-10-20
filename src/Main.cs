using Godot;
using Vespera.Autoload;

namespace Vespera;

/// <summary>Cena de boot. Hoje vai direto para Lumeira; o menu inicial entra aqui depois.</summary>
public partial class Main : Node
{
    private const string FirstScene = "res://scenes/World/City/Lumeira.tscn";

    public override void _Ready() => GameManager.Instance.ChangeScene(FirstScene);
}
