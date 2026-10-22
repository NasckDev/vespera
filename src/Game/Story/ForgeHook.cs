using System;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Story;

/// <summary>
/// Gancho "OpenForge": chamado depois que a conversa com Odete termina na opção da forja. Se a tela de Forja da
/// economia já existe (<c>Vespera.Game.UI.ForgeScreen.Open</c>, estático), chama-a; senão avisa "A forja abre em breve".
/// TODO(forja): trocar a busca por reflexão por uma chamada direta a <c>ForgeScreen.Open()</c> quando a ramificação
/// da economia for integrada (este é o único ponto a mexer).
/// </summary>
public static class ForgeHook
{
    public const string ForgeScreenType = "Vespera.Game.UI.ForgeScreen";
    public const string ComingSoon = "A forja abre em breve.";

    public static void Open(Level level)
    {
        var method = Type.GetType(ForgeScreenType)?.GetMethod("Open", Type.EmptyTypes);
        if (method is not null && method.IsStatic)
        {
            method.Invoke(null, null);
            return;
        }

        level.ShowNotice(ComingSoon);
    }
}
