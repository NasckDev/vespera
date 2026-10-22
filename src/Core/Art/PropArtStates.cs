namespace Vespera.Core.Art;

/// <summary>Estado desenhado de um quebrável com arte aprovada (barril, caixote).</summary>
public enum BreakableStage
{
    Whole,
    Cracked,
    Broken,
}

/// <summary>Estado desenhado de uma flor do mundo.</summary>
public enum FlowerStage
{
    Flowering,
    Harvested,
    Sprouting,
}

/// <summary>Vista da esquiva desenhada (lado v4; frente/costas v3 provisórias).</summary>
public enum DodgeView
{
    Side,
    Front,
    Back,
}

/// <summary>
/// Regras dos estados dos objetos com arte aprovada (DECISIONS C2-D5): quando o barril racha, que quadro do baú mostrar
/// abrindo, quando a flor colhida volta a brotar e florir, qual esquiva usar pela direção do dash. Puras e testadas.
/// </summary>
public static class PropArtStates
{
    /// <summary>Fração de vida em que o quebrável passa a mostrar o rachado.</summary>
    public const float CrackAt = 0.5f;

    /// <summary>Quadros do baú: fechado, abrindo 1, abrindo 2, aberto (o vazio vem depois do saque).</summary>
    public const int ChestOpeningFrames = 4;

    /// <summary>Fração do tempo de rebrota em que a flor colhida vira broto.</summary>
    public const float SproutAt = 0.4f;

    public static BreakableStage Breakable(int hp, int maxHp)
    {
        if (hp <= 0)
        {
            return BreakableStage.Broken;
        }

        return maxHp > 0 && hp <= maxHp * CrackAt ? BreakableStage.Cracked : BreakableStage.Whole;
    }

    /// <summary>Quadro do baú abrindo (0 fechado … 3 aberto) para o progresso 0–1 da animação de abrir.</summary>
    public static int ChestFrame(float progress)
    {
        if (progress <= 0f)
        {
            return 0;
        }

        return progress >= 1f ? ChestOpeningFrames - 1 : Math.Clamp(1 + (int)(progress * (ChestOpeningFrames - 1)), 1, ChestOpeningFrames - 1);
    }

    /// <summary>
    /// Flor: florida enquanto não foi colhida; colhida logo depois; broto a partir de <see cref="SproutAt"/> do tempo de
    /// rebrota; florida de novo quando o tempo passa (o <c>WorldObjectRegistry</c> deixa de marcá-la como gasta).
    /// </summary>
    public static FlowerStage Flower(bool spent, double sinceSpent, double regrowSeconds)
    {
        if (!spent)
        {
            return FlowerStage.Flowering;
        }

        return regrowSeconds > 0 && sinceSpent >= regrowSeconds * SproutAt ? FlowerStage.Sprouting : FlowerStage.Harvested;
    }

    /// <summary>Esquiva desenhada pela direção do dash (x, y em u; y para baixo): lado se |x| ≥ 0,6·|y|.</summary>
    public static DodgeView Dodge(float dx, float dy)
    {
        if (MathF.Abs(dx) >= MathF.Abs(dy) * 0.6f)
        {
            return DodgeView.Side;
        }

        return dy < 0 ? DodgeView.Back : DodgeView.Front;
    }
}
