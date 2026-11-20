using System.Text;

namespace Vespera.Core.Net;

/// <summary>Efeito de combate que o host manda ao convidado (o host decide, o convidado só toca o efeito).</summary>
public enum NetEffectKind : byte
{
    /// <summary>Estouro de faíscas no impacto.</summary>
    Spark = 1,

    /// <summary>Anel que se expande no chão (pouso, onda do Martelo).</summary>
    Ring = 2,

    /// <summary>Número de dano flutuante.</summary>
    Number = 3,

    /// <summary>Tremor de câmera.</summary>
    Shake = 4,

    /// <summary>Rastro do corte de uma arma corpo a corpo.</summary>
    Slash = 5,
}

/// <summary>
/// Um efeito de combate. Os campos usados dependem do <see cref="Kind"/>:
/// Spark (X, Y, <see cref="A"/> = velocidade, <see cref="B"/> = ângulo, <see cref="C"/> = abertura em graus, <see cref="N"/> = quantidade, cor),
/// Ring (X, Y, A = raio, B = segundos, C = largura, cor),
/// Number (X, Y, A = tamanho relativo, <see cref="Text"/>, cor),
/// Shake (A = trauma),
/// Slash (X, Y, A = ângulo, B = raio, C = abertura em rad, <see cref="D"/> = espessura, N = nível da arma 1..3).
/// </summary>
public readonly record struct NetEffect(
    NetEffectKind Kind,
    float X = 0,
    float Y = 0,
    float A = 0,
    float B = 0,
    float C = 0,
    float D = 0,
    byte R = 255,
    byte G = 255,
    byte Blue = 255,
    int N = 0,
    string Text = "")
{
    public const int MaxTextBytes = 16;
    public const int MaxSparks = 64;
    public const int MaxTier = 3;
}

/// <summary>
/// Lote de efeitos (host → convidado, canal confiável, no ritmo do snapshot). Puro: só bytes, leitura nunca lança e valida
/// tamanho, floats finitos, faixas e texto (um pacote ruim vira <c>false</c>, não um efeito gigante na tela do convidado).
/// </summary>
public static class EffectBatch
{
    /// <summary>Efeitos por lote (o host descarta os mais antigos se passar).</summary>
    public const int MaxEffects = 64;

    public static byte[] Write(IReadOnlyList<NetEffect> effects)
    {
        if (effects.Count > MaxEffects)
        {
            throw new ArgumentException($"lote com {effects.Count} efeitos (máximo {MaxEffects})", nameof(effects));
        }

        var w = new NetWriter();
        w.Byte((byte)NetMessageKind.Events);
        w.Byte((byte)effects.Count);
        foreach (var e in effects)
        {
            w.Byte((byte)e.Kind);
            switch (e.Kind)
            {
                case NetEffectKind.Spark:
                    w.Float(e.X);
                    w.Float(e.Y);
                    w.Float(e.A);
                    w.Float(e.B);
                    w.Float(e.C);
                    w.Byte((byte)Math.Clamp(e.N, 1, NetEffect.MaxSparks));
                    WriteColor(w, e);
                    break;
                case NetEffectKind.Ring:
                    w.Float(e.X);
                    w.Float(e.Y);
                    w.Float(e.A);
                    w.Float(e.B);
                    w.Float(e.C);
                    WriteColor(w, e);
                    break;
                case NetEffectKind.Number:
                    w.Float(e.X);
                    w.Float(e.Y);
                    w.Float(e.A);
                    WriteColor(w, e);
                    var text = TrimUtf8(e.Text);
                    w.Byte((byte)text.Length);
                    foreach (var b in text)
                    {
                        w.Byte(b);
                    }

                    break;
                case NetEffectKind.Shake:
                    w.Float(e.A);
                    break;
                case NetEffectKind.Slash:
                    w.Float(e.X);
                    w.Float(e.Y);
                    w.Float(e.A);
                    w.Float(e.B);
                    w.Float(e.C);
                    w.Float(e.D);
                    w.Byte((byte)Math.Clamp(e.N, 1, NetEffect.MaxTier));
                    break;
                default:
                    throw new ArgumentException($"efeito desconhecido: {e.Kind}", nameof(effects));
            }
        }

        return w.ToArray();
    }

    public static bool TryRead(ReadOnlySpan<byte> packet, out IReadOnlyList<NetEffect> effects)
    {
        effects = [];
        var r = new NetReader(packet);
        if (r.Byte() != (byte)NetMessageKind.Events)
        {
            return false;
        }

        var count = r.Byte();
        if (r.Failed || count > MaxEffects)
        {
            return false;
        }

        var list = new List<NetEffect>(count);
        for (var i = 0; i < count; i++)
        {
            var kind = (NetEffectKind)r.Byte();
            if (r.Failed || !TryReadOne(ref r, kind, out var effect))
            {
                return false;
            }

            list.Add(effect);
        }

        if (!r.AtEnd)
        {
            return false;
        }

        effects = list;
        return true;
    }

    private static bool TryReadOne(ref NetReader r, NetEffectKind kind, out NetEffect effect)
    {
        effect = default;
        float x, y, a, b, c, d;
        switch (kind)
        {
            case NetEffectKind.Spark:
                if (!r.TryFloat(out x) || !r.TryFloat(out y) || !r.TryFloat(out a) || !r.TryFloat(out b) || !r.TryFloat(out c))
                {
                    return false;
                }

                var amount = r.Byte();
                var (cr, cg, cb) = (r.Byte(), r.Byte(), r.Byte());
                if (r.Failed || amount is 0 or > NetEffect.MaxSparks || a is < 0 or > 5000 || c is < 0 or > 360)
                {
                    return false;
                }

                effect = new NetEffect(kind, x, y, a, b, c, 0, cr, cg, cb, amount);
                return true;
            case NetEffectKind.Ring:
                if (!r.TryFloat(out x) || !r.TryFloat(out y) || !r.TryFloat(out a) || !r.TryFloat(out b) || !r.TryFloat(out c))
                {
                    return false;
                }

                var (rr, rg, rb) = (r.Byte(), r.Byte(), r.Byte());
                if (r.Failed || a is < 0 or > 5000 || b is <= 0 or > 5 || c is < 0 or > 100)
                {
                    return false;
                }

                effect = new NetEffect(kind, x, y, a, b, c, 0, rr, rg, rb);
                return true;
            case NetEffectKind.Number:
                if (!r.TryFloat(out x) || !r.TryFloat(out y) || !r.TryFloat(out a))
                {
                    return false;
                }

                var (nr, ng, nb) = (r.Byte(), r.Byte(), r.Byte());
                var length = r.Byte();
                if (r.Failed || a is < 0 or > 10 || length is 0 or > NetEffect.MaxTextBytes || !r.TryBytes(length, out var raw))
                {
                    return false;
                }

                var text = Encoding.UTF8.GetString(raw);
                if (text.Any(char.IsControl))
                {
                    return false;
                }

                effect = new NetEffect(kind, x, y, a, 0, 0, 0, nr, ng, nb, 0, text);
                return true;
            case NetEffectKind.Shake:
                if (!r.TryFloat(out a) || a is < 0 or > 2)
                {
                    return false;
                }

                effect = new NetEffect(kind, A: a);
                return true;
            case NetEffectKind.Slash:
                if (!r.TryFloat(out x) || !r.TryFloat(out y) || !r.TryFloat(out a) || !r.TryFloat(out b) || !r.TryFloat(out c) || !r.TryFloat(out d))
                {
                    return false;
                }

                var tier = r.Byte();
                if (r.Failed || tier is 0 or > NetEffect.MaxTier || b is <= 0 or > 5000 || c is <= 0 or > 7 || d is < 0 or > 5000)
                {
                    return false;
                }

                effect = new NetEffect(kind, x, y, a, b, c, d, N: tier);
                return true;
            default:
                return false;
        }
    }

    private static void WriteColor(NetWriter w, NetEffect e)
    {
        w.Byte(e.R);
        w.Byte(e.G);
        w.Byte(e.Blue);
    }

    /// <summary>Texto em UTF-8 de até <see cref="NetEffect.MaxTextBytes"/> bytes, sem partir um caractere no meio.</summary>
    private static byte[] TrimUtf8(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (bytes.Length <= NetEffect.MaxTextBytes)
        {
            return bytes;
        }

        var end = NetEffect.MaxTextBytes;
        while (end > 0 && (bytes[end] & 0xC0) == 0x80)
        {
            end--;
        }

        return bytes[..end];
    }
}
