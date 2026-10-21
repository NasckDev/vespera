namespace Vespera.Core.World;

/// <summary>Vista do personagem para a animação procedural.</summary>
public enum GaitView
{
    Front,
    Back,
    Side,
}

/// <summary>Pose procedural de um quadro: deslocamentos em u, escala e rotação do corpo em volta do pé.</summary>
public readonly record struct GaitPose(float OffsetX, float OffsetY, float ScaleX, float ScaleY, float Rotation)
{
    public static GaitPose Identity => new(0f, 0f, 1f, 1f, 0f);
}

/// <summary>
/// Caminhada procedural (DECISIONS D43) para as vistas sem quadros desenhados (frente/costas do caçador e todas as
/// vistas das outras classes): passo com squash leve no contato, quique, balanço lateral para o pé de apoio e
/// inclinação na direção do movimento. Frequência e altura do quique vêm da v2 (<c>render.js:161</c>: <c>|sin(walkT·11)|·1,6</c>),
/// escaladas pela altura do personagem. Tudo é função pura do tempo: sem estado, sem alocação.
/// </summary>
public static class ProceduralGait
{
    /// <summary>Radianos de fase por segundo de caminhada a 78 u/s (<c>render.js:161</c>).</summary>
    public const float StepFrequency = 11f;

    /// <summary>Altura do quique em u para um personagem de escala 1 (<c>render.js:161</c>).</summary>
    public const float BobHeight = 1.6f;

    /// <summary>Respiração parado (<c>render.js:163</c>: <c>sin(t·2,4)·0,35</c>).</summary>
    public const float IdleFrequency = 2.4f;

    public const float IdleBobHeight = 0.35f;

    private const float SquashAtContact = 0.05f;
    private const float StretchAtPeak = 0.025f;
    private const float SideSway = 0.9f;
    private const float SwayRoll = 0.035f;
    private const float LeanMax = 0.075f;
    private const float BreathScale = 0.012f;

    /// <summary>Passos completos dados até a fase <paramref name="walkPhase"/> (um a cada contato do pé).</summary>
    public static int StepIndex(float walkPhase) => (int)MathF.Floor(walkPhase * StepFrequency / MathF.PI);

    /// <summary>Quadro de um ciclo de caminhada de <paramref name="frames"/> quadros (dois quadros por passo).</summary>
    public static int CycleFrame(float walkPhase, int frames) =>
        frames <= 0 ? 0 : (int)(((long)MathF.Floor(walkPhase * StepFrequency * 2f / MathF.PI) % frames + frames) % frames);

    /// <summary>
    /// Pose do corpo. <paramref name="walkPhase"/> avança com <c>dt · velocidade/78</c>; <paramref name="speed"/> (0–1) mistura
    /// respiração parado ↔ caminhada; <paramref name="leanX"/> (−1–1) é a velocidade horizontal relativa (inclina para lá);
    /// <paramref name="scale"/> é a escala do personagem (amplitudes em u crescem com ele).
    /// </summary>
    public static GaitPose Sample(GaitView view, float walkPhase, float time, float speed, float leanX, float scale)
    {
        speed = Math.Clamp(speed, 0f, 1f);
        var idle = 1f - speed;

        var s = MathF.Abs(MathF.Sin(walkPhase * StepFrequency)); // 0 no contato do pé, 1 no alto do passo
        var walkBob = -s * BobHeight * scale;
        var breath = MathF.Sin(time * IdleFrequency);
        var idleBob = breath * IdleBobHeight * scale;

        // squash no contato, leve estica no alto; respiração parado mexe só um pouco a escala
        var walkScaleY = 1f - (SquashAtContact * (1f - s)) + (StretchAtPeak * s);
        var idleScaleY = 1f + (breath * BreathScale);
        var scaleY = (walkScaleY * speed) + (idleScaleY * idle);
        var scaleX = 1f + ((1f - scaleY) * 0.6f); // conserva volume

        // frente/costas: o corpo vai para o pé de apoio (um balanço por par de passos) e rola um pouco
        var swayPhase = MathF.Sin(walkPhase * StepFrequency * 0.5f);
        var offsetX = view == GaitView.Side ? 0f : swayPhase * SideSway * scale * speed;
        var roll = view == GaitView.Side ? 0f : swayPhase * SwayRoll * speed;
        var lean = Math.Clamp(leanX, -1f, 1f) * LeanMax;

        return new GaitPose(offsetX, (walkBob * speed) + (idleBob * idle), scaleX, scaleY, roll + lean);
    }
}
