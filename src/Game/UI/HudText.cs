using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Texto dos HUDs (combate, caçada, co-op): segue a opção "Tamanho do texto", que antes só valia para os menus.
/// Os tamanhos dos HUDs são dados na base de 1080p; <see cref="Bind"/> reaplica quando a opção muda no meio do jogo.
/// </summary>
public static class HudText
{
    private const float LargeFactor = 1.25f;
    private const float HugeFactor = 1.5f;

    public static float Factor(int textSize) => textSize switch
    {
        GameSettings.TextLarge => LargeFactor,
        GameSettings.TextHuge => HugeFactor,
        _ => 1f,
    };

    /// <summary>Tamanho de fonte de HUD para a opção atual de texto.</summary>
    public static int Px(int basePx, float maxFactor = float.MaxValue) =>
        Mathf.RoundToInt(basePx * Mathf.Min(Factor(GameSettings.Current.TextSize), maxFactor));

    /// <summary>Aplica <paramref name="basePx"/> ao rótulo e o mantém em dia enquanto estiver na árvore.</summary>
    /// <param name="maxFactor">Teto da ampliação para textos presos a uma barra ou caixa de tamanho fixo.</param>
    public static Label Bind(Label label, int basePx, float maxFactor = float.MaxValue)
    {
        void Apply() => label.AddThemeFontSizeOverride("font_size", Px(basePx, maxFactor));
        Apply();
        label.TreeEntered += () =>
        {
            Apply();
            GameSettings.Changed += Apply;
        };
        label.TreeExited += () => GameSettings.Changed -= Apply;
        return label;
    }
}
