namespace Vespera.Core.Story;

/// <summary>
/// Relógio de noites da história (ROTEIRO-V2 §13 Atos IV e V): cada véspera vivida vira uma noite. A noite cai quando o
/// caçador volta de uma Brecha (a caçada é na véspera) ou espera anoitecer no banco do Cemitério; a manhã volta com a
/// cena "morning" (a noite passa). O Vigia só aparece à noite e faz uma pergunta por noite; a Brecha B das Ruas Baixas
/// conta as vésperas até romper (§8.1, <see cref="RaviRescue"/>). Vai no save dentro de <see cref="StoryProgress"/>.
/// </summary>
public sealed class NightState
{
    /// <summary>Quantas noites já caíram (0 = nenhuma ainda).</summary>
    public int Night { get; set; }

    /// <summary>Verdadeiro entre a noite cair e a próxima manhã.</summary>
    public bool IsNight { get; set; }

    /// <summary>Noite em que o Vigia fez a última pergunta respondida (-1 = nunca).</summary>
    public int VigiaAskedOn { get; set; } = -1;

    /// <summary>Noite em que a Brecha B das Ruas Baixas abriu (-1 = ainda não abriu).</summary>
    public int RuasOpenedOn { get; set; } = -1;

    /// <summary>A véspera acabou: começa uma noite nova.</summary>
    public void Fall()
    {
        Night++;
        IsNight = true;
    }

    /// <summary>A noite passou.</summary>
    public void Dawn() => IsNight = false;

    /// <summary>Normaliza depois de carregar um save (nada negativo além do -1 de "nunca").</summary>
    public void Sanitize()
    {
        Night = Math.Max(0, Night);
        VigiaAskedOn = Math.Clamp(VigiaAskedOn, -1, Night);
        RuasOpenedOn = Math.Clamp(RuasOpenedOn, -1, Night);
    }
}

/// <summary>
/// Flags calculadas (não gravadas) que as falas e as fases podem usar nas condições, como qualquer flag:
/// <list type="bullet">
/// <item><c>noite</c>: é noite agora (o Vigia está no banco).</item>
/// <item><c>vigia_perguntou_hoje</c>: o Vigia já fez a pergunta desta noite (uma por visita).</item>
/// <item><c>ravi_no_avesso</c>: a Brecha B rompeu e o Ravi ainda não voltou (ele some da cidade).</item>
/// </list>
/// </summary>
public static class NightFlags
{
    public const string Night = "noite";
    public const string VigiaAskedTonight = "vigia_perguntou_hoje";
    public const string RaviMissing = "ravi_no_avesso";

    /// <summary>Prefixo das respostas às perguntas do Vigia (<c>vigia_p1_sim</c>, <c>vigia_p2_nao</c>...).</summary>
    public const string VigiaAnswerPrefix = "vigia_p";

    /// <summary>Primeira conversa com o Vigia: o Ato IV começou de verdade (a Brecha B das Ruas Baixas abre).</summary>
    public const string MetVigia = "met_vigia";

    /// <summary>Valor de uma flag calculada; null quando a flag é comum (gravada).</summary>
    public static bool? Computed(string flag, StoryProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var night = progress.Night;
        return flag switch
        {
            Night => night.IsNight,
            VigiaAskedTonight => night.IsNight && night.VigiaAskedOn == night.Night,
            RaviMissing => progress.Has(RaviRescue.Ruptured) && !progress.Has(RaviRescue.LanternRecovered),
            _ => null,
        };
    }

    public static bool IsComputed(string flag) => flag is Night or VigiaAskedTonight or RaviMissing;

    /// <summary>Resposta a uma pergunta do Vigia (<c>vigia_p&lt;n&gt;_&lt;resposta&gt;</c>).</summary>
    public static bool IsVigiaAnswer(string flag) =>
        flag.Length > VigiaAnswerPrefix.Length + 2
        && flag.StartsWith(VigiaAnswerPrefix, StringComparison.Ordinal)
        && char.IsDigit(flag[VigiaAnswerPrefix.Length])
        && flag[VigiaAnswerPrefix.Length + 1] == '_';
}
