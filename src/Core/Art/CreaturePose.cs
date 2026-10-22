namespace Vespera.Core.Art;

/// <summary>Pose desenhada de um chefe/elite (as poses autorais da v3/v4; <c>special</c> é golpe especial, nunca vista lateral).</summary>
public enum CreaturePoseKind
{
    Front,
    Back,
    Attack,
    Special,
    Death,
}

/// <summary>
/// Estado de IA lido pelo visual (vem do <c>MonsterBody</c> do combate). <c>StateTime</c>: na telegrafia, o tempo no
/// estado; no golpe, o tempo desde que a telegrafia acabou (o estalo é a soltura do golpe).
/// </summary>
public readonly record struct CreatureMotionInput(
    bool Dead,
    bool Special,
    bool Attacking,
    bool Telegraphing,
    bool FacingAway,
    bool Moving,
    bool Fly,
    float Time,
    float StateTime);

/// <summary>Pose escolhida e o corpo vivo em volta dela: escala em x/y e deslocamento em y (u × escala do sprite).</summary>
public readonly record struct CreaturePoseSample(CreaturePoseKind Pose, float ScaleX, float ScaleY, float OffsetY);

/// <summary>
/// Troca de pose por estado de IA dos chefes com arte aprovada (DECISIONS C2-D4) e o "corpo vivo" que impede que fiquem
/// estáticos com uma pose só: respiração parada, passada pesada andando, antecipação (encolhe e estica) na telegrafia,
/// estalo de impacto no início do golpe e afundar na morte. Amplitudes pequenas (≤ 6%) para não deformar a pintura.
/// Função pura do tempo, sem estado e sem alocação.
/// </summary>
public static class CreaturePose
{
    /// <summary>Respiração: ciclo de ~2,9 s, 1,8% em y (e 0,8% ao contrário em x, para o volume parecer constante).</summary>
    public const float BreathFrequency = 2.2f;
    public const float BreathAmount = 0.018f;

    /// <summary>Passada andando: mais rápida e um pouco mais funda que a respiração.</summary>
    public const float StrideFrequency = 7.5f;
    public const float StrideAmount = 0.022f;

    /// <summary>Antecipação máxima na telegrafia (encolhe em y, alarga em x).</summary>
    public const float WindupAmount = 0.06f;

    /// <summary>Estalo do golpe: estica 6% e volta em <see cref="ImpactSeconds"/>.</summary>
    public const float ImpactAmount = 0.06f;
    public const float ImpactSeconds = 0.22f;

    public static CreatureMotionInput Input(bool dead, bool special, bool attacking, bool telegraphing, bool facingAway,
        bool moving, bool fly, float time, float stateTime) =>
        new(dead, special, attacking, telegraphing, facingAway, moving, fly, time, stateTime);

    /// <summary>Pose desenhada para o estado (sem pose de costas, <see cref="CreaturePoseKind.Back"/> vira frente fora daqui).</summary>
    public static CreaturePoseKind PoseFor(in CreatureMotionInput m)
    {
        if (m.Dead)
        {
            return CreaturePoseKind.Death;
        }

        if (m.Special)
        {
            return CreaturePoseKind.Special;
        }

        if (m.Attacking)
        {
            return CreaturePoseKind.Attack;
        }

        return m.FacingAway ? CreaturePoseKind.Back : CreaturePoseKind.Front;
    }

    public static CreaturePoseSample Evaluate(in CreatureMotionInput m)
    {
        var pose = PoseFor(m);
        float sx = 1f, sy = 1f, oy = 0f;
        if (m.Dead)
        {
            // afunda um pouco nos primeiros 0,5 s (o fade da morte é do combate)
            var k = Math.Clamp(m.StateTime / 0.5f, 0f, 1f);
            sy = 1f - (0.05f * k);
            sx = 1f + (0.03f * k);
            return new CreaturePoseSample(pose, sx, sy, 0f);
        }

        // respiração sempre; passada por cima quando anda (voadores só flutuam: o bob é do combate)
        var breath = MathF.Sin(m.Time * BreathFrequency);
        sy += breath * BreathAmount;
        sx -= breath * BreathAmount * 0.45f;
        if (m.Moving && !m.Fly)
        {
            var stride = MathF.Abs(MathF.Sin(m.Time * StrideFrequency));
            sy -= stride * StrideAmount;
            sx += stride * StrideAmount * 0.5f;
        }

        if (m.Telegraphing)
        {
            // encolhe juntando força (ease-out) e treme de leve no fim
            var k = 1f - MathF.Exp(-m.StateTime * 5f);
            sy -= WindupAmount * k;
            sx += WindupAmount * 0.6f * k;
        }
        else if (m.Attacking || m.Special)
        {
            // estalo: estica e volta
            var k = Math.Clamp(1f - (m.StateTime / ImpactSeconds), 0f, 1f);
            sy += ImpactAmount * k;
            sx -= ImpactAmount * 0.5f * k;
            oy = -1.5f * k;
        }

        return new CreaturePoseSample(pose, sx, sy, oy);
    }
}
