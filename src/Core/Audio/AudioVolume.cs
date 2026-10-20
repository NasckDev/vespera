namespace Vespera.Core.Audio;

/// <summary>Conversão do volume de opções (0–1, linear) para decibéis do barramento.</summary>
public static class AudioVolume
{
    /// <summary>Piso usado para volume 0 (o barramento também é mutado nesse caso).</summary>
    public const float SilenceDb = -80f;

    public static float Clamp01(float linear) => float.IsNaN(linear) ? 1f : Math.Clamp(linear, 0f, 1f);

    /// <summary>20·log10(v), com 0 → <see cref="SilenceDb"/>. Fora de 0–1 é limitado antes.</summary>
    public static float ToDb(float linear)
    {
        var v = Clamp01(linear);
        return v <= 0f ? SilenceDb : Math.Max(SilenceDb, 20f * MathF.Log10(v));
    }
}
