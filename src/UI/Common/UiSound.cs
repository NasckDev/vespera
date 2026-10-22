using Vespera.Autoload;

namespace Vespera.UI.Common;

/// <summary>Sons da UI da v2 (<c>ui</c>, <c>open</c>, <c>coin</c>, <c>hurt</c> para opção bloqueada…) pelo AudioManager.</summary>
public static class UiSound
{
    /// <summary>Navegar/confirmar em menus.</summary>
    public static void Click() => Play("ui");

    /// <summary>Abrir inventário, mural, baú.</summary>
    public static void Open() => Play("open");

    /// <summary>Compra ou venda.</summary>
    public static void Coin() => Play("coin");

    /// <summary>Opção desabilitada ou ação recusada (<c>ui.js:50</c>).</summary>
    public static void Denied() => Play("hurt");

    public static void Play(string id) => AudioManager.Instance?.PlaySfx(id);
}
