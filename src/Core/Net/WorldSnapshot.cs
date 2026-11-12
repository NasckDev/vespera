namespace Vespera.Core.Net;

/// <summary>Estado de um caçador no snapshot. <see cref="Facing"/>: 0 baixo, 1 cima, 2 lado direito, 3 lado esquerdo.</summary>
public readonly record struct NetHunter(byte Slot, float X, float Y, byte Facing, bool Downed, float Hp, float MaxHp);

/// <summary>Estado de um monstro no snapshot (id estável enquanto ele existir na sala).</summary>
public readonly record struct NetEnemy(int Id, float X, float Y, float Hp, float MaxHp);

/// <summary>
/// Foto do mundo que o host manda ~20 vezes por segundo: passo da simulação, sala atual (a troca de sala é a mudança deste
/// id), caçadores, monstros vivos e a fala aberta (vazia sem conversa). O convidado desenha a partir disto.
/// </summary>
public sealed record WorldSnapshot(uint Tick, string LevelId, IReadOnlyList<NetHunter> Hunters, IReadOnlyList<NetEnemy> Enemies, string Dialogue = "")
{
    public const int MaxHunters = 8;
    public const int MaxEnemies = 512;
    public const int MaxDialogueBytes = 600;

    public NetHunter? Hunter(int slot)
    {
        foreach (var h in Hunters)
        {
            if (h.Slot == slot)
            {
                return h;
            }
        }

        return null;
    }

    public byte[] Write()
    {
        if (Hunters.Count > MaxHunters || Enemies.Count > MaxEnemies)
        {
            throw new ArgumentException($"snapshot grande demais ({Hunters.Count} caçadores, {Enemies.Count} monstros)");
        }

        var w = new NetWriter();
        w.Byte((byte)NetMessageKind.Snapshot);
        w.UInt(Tick);
        w.Id(LevelId);
        w.Byte((byte)Hunters.Count);
        foreach (var h in Hunters)
        {
            w.Byte(h.Slot);
            w.Float(h.X);
            w.Float(h.Y);
            w.Byte(h.Facing);
            w.Byte(h.Downed ? (byte)1 : (byte)0);
            w.Float(h.Hp);
            w.Float(h.MaxHp);
        }

        w.UShort((ushort)Enemies.Count);
        foreach (var e in Enemies)
        {
            w.Int(e.Id);
            w.Float(e.X);
            w.Float(e.Y);
            w.Float(e.Hp);
            w.Float(e.MaxHp);
        }

        var dialogue = TrimUtf8(Dialogue, MaxDialogueBytes);
        w.UShort((ushort)dialogue.Length);
        foreach (var b in dialogue)
        {
            w.Byte(b);
        }

        return w.ToArray();
    }

    /// <summary>Lê e valida (tamanhos, limites, floats finitos, vida em 0..máx, slots únicos). Pacote ruim → false.</summary>
    public static bool TryRead(ReadOnlySpan<byte> packet, out WorldSnapshot snapshot)
    {
        snapshot = null!;
        var r = new NetReader(packet);
        if (r.Byte() != (byte)NetMessageKind.Snapshot || !r.TryUInt(out var tick) || !r.TryId(out var levelId))
        {
            return false;
        }

        var hunterCount = r.Byte();
        if (r.Failed || hunterCount > MaxHunters)
        {
            return false;
        }

        var hunters = new List<NetHunter>(hunterCount);
        var slots = new HashSet<byte>();
        for (var i = 0; i < hunterCount; i++)
        {
            var slot = r.Byte();
            if (!r.TryFloat(out var x) || !r.TryFloat(out var y))
            {
                return false;
            }

            var facing = r.Byte();
            var downed = r.Byte();
            if (!r.TryFloat(out var hp) || !r.TryFloat(out var maxHp) || facing > 3 || downed > 1 || !ValidHp(hp, maxHp) || !slots.Add(slot) || slot >= MaxHunters)
            {
                return false;
            }

            hunters.Add(new NetHunter(slot, x, y, facing, downed == 1, hp, maxHp));
        }

        if (!r.TryUShort(out var enemyCount) || enemyCount > MaxEnemies)
        {
            return false;
        }

        var enemies = new List<NetEnemy>(enemyCount);
        for (var i = 0; i < enemyCount; i++)
        {
            if (!r.TryInt(out var id) || !r.TryFloat(out var x) || !r.TryFloat(out var y) || !r.TryFloat(out var hp) || !r.TryFloat(out var maxHp) || !ValidHp(hp, maxHp))
            {
                return false;
            }

            enemies.Add(new NetEnemy(id, x, y, hp, maxHp));
        }

        if (!r.TryUShort(out var dialogueLength) || dialogueLength > MaxDialogueBytes)
        {
            return false;
        }

        var text = new byte[dialogueLength];
        for (var i = 0; i < dialogueLength; i++)
        {
            text[i] = r.Byte();
        }

        if (!r.AtEnd)
        {
            return false;
        }

        snapshot = new WorldSnapshot(tick, levelId, hunters, enemies, System.Text.Encoding.UTF8.GetString(text));
        return true;
    }

    private static bool ValidHp(float hp, float maxHp) => maxHp >= 0 && hp >= 0 && hp <= maxHp + 0.001f;

    private static byte[] TrimUtf8(string text, int maxBytes)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (bytes.Length <= maxBytes)
        {
            return bytes;
        }

        // Corta sem partir um caractere no meio (bytes de continuação começam com 10xxxxxx).
        var end = maxBytes;
        while (end > 0 && (bytes[end] & 0xC0) == 0x80)
        {
            end--;
        }

        return bytes[..end];
    }
}
