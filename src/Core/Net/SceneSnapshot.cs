namespace Vespera.Core.Net;

/// <summary>Um objeto de cenário (do catálogo <c>props.json</c>) com o pé em (X, Y).</summary>
public readonly record struct SceneEntry(string Type, float X, float Y, bool Flip);

/// <summary>
/// Cenário da sala do host (host → convidado, confiável, só quando muda): id da sala (o convidado carrega o chão e o
/// catálogo pelo id), variante do chão (0 = o padrão do JSON; n = variante n−1) e os objetos que existem agora no host
/// (objetos fixos, quebráveis ainda inteiros e o que a sala montou em código). Puro; leitura nunca lança.
/// </summary>
public sealed record SceneSnapshot(string LevelId, byte GroundVariant, IReadOnlyList<SceneEntry> Entries)
{
    public const int MaxEntries = 400;

    public byte[] Write()
    {
        if (Entries.Count > MaxEntries)
        {
            throw new ArgumentException($"cenário grande demais ({Entries.Count} objetos)");
        }

        var w = new NetWriter();
        w.Byte((byte)NetMessageKind.Scene);
        w.Id(LevelId);
        w.Byte(GroundVariant);
        w.UShort((ushort)Entries.Count);
        foreach (var e in Entries)
        {
            w.Id(e.Type);
            w.Float(e.X);
            w.Float(e.Y);
            w.Byte(e.Flip ? (byte)1 : (byte)0);
        }

        return w.ToArray();
    }

    public static bool TryRead(ReadOnlySpan<byte> packet, out SceneSnapshot scene)
    {
        scene = null!;
        var r = new NetReader(packet);
        if (r.Byte() != (byte)NetMessageKind.Scene || !r.TryId(out var levelId))
        {
            return false;
        }

        var variant = r.Byte();
        if (!r.TryUShort(out var count) || count > MaxEntries)
        {
            return false;
        }

        var entries = new List<SceneEntry>(count);
        for (var i = 0; i < count; i++)
        {
            if (!r.TryId(out var type) || !r.TryFloat(out var x) || !r.TryFloat(out var y))
            {
                return false;
            }

            var flip = r.Byte();
            if (r.Failed || flip > 1)
            {
                return false;
            }

            entries.Add(new SceneEntry(type, x, y, flip == 1));
        }

        if (!r.AtEnd)
        {
            return false;
        }

        scene = new SceneSnapshot(levelId, variant, entries);
        return true;
    }
}
