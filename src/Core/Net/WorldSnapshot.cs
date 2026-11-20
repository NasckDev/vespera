namespace Vespera.Core.Net;

/// <summary>
/// Estado de um caçador no snapshot. <see cref="Facing"/>: 0 baixo, 1 cima, 2 lado direito, 3 lado esquerdo.
/// <see cref="Kind"/>: classe (0 Lâmina, 1 Arco, 2 Martelo), para o convidado desenhar o sprite certo.
/// <see cref="Stamina"/>: fôlego em 0..255 (255 = cheio). <see cref="Anim"/>/<see cref="Frame"/>: animação que o host está
/// tocando (índice na lista ordenada de animações do sprite, <see cref="WorldSnapshot.NoAnim"/> = nenhuma) e o quadro dela.
/// </summary>
public readonly record struct NetHunter(
    byte Slot,
    float X,
    float Y,
    byte Facing,
    bool Downed,
    float Hp,
    float MaxHp,
    byte Kind = 0,
    byte Stamina = 255,
    byte Anim = WorldSnapshot.NoAnim,
    byte Frame = 0)
{
    public const byte MaxKind = 7;
}

/// <summary>
/// Estado de um monstro no snapshot (id estável enquanto ele existir na sala; <see cref="Type"/> = id do monstro, vazio se
/// desconhecido). <see cref="Anim"/>/<see cref="Frame"/>/<see cref="FlipH"/>: a pose que o host está tocando.
/// </summary>
public readonly record struct NetEnemy(
    int Id,
    float X,
    float Y,
    float Hp,
    float MaxHp,
    string Type = "",
    byte Anim = WorldSnapshot.NoAnim,
    byte Frame = 0,
    bool FlipH = false);

/// <summary>Flecha em voo (posição na sala, ângulo em radianos e se é a flecha de luz que atravessa).</summary>
public readonly record struct NetProjectile(float X, float Y, float Angle, bool Pierce);

/// <summary>NPC da sala com o pé em (X, Y). <see cref="Facing"/> como nos caçadores.</summary>
public readonly record struct NetNpc(string Id, float X, float Y, byte Facing, bool Walking);

/// <summary>
/// Foto do mundo que o host manda ~20 vezes por segundo: passo da simulação, sala atual (a troca de sala é a mudança deste
/// id), caçadores, monstros vivos, flechas, NPCs, a fala aberta (vazia sem conversa) e bandeiras (host pausado). O convidado
/// desenha a partir disto.
/// </summary>
public sealed record WorldSnapshot(
    uint Tick,
    string LevelId,
    IReadOnlyList<NetHunter> Hunters,
    IReadOnlyList<NetEnemy> Enemies,
    string Dialogue = "",
    IReadOnlyList<NetProjectile>? Projectiles = null,
    IReadOnlyList<NetNpc>? Npcs = null,
    byte Flags = 0)
{
    public const int MaxHunters = 8;
    public const int MaxEnemies = 512;
    public const int MaxDialogueBytes = 600;
    public const int MaxProjectiles = 64;
    public const int MaxNpcs = 32;

    /// <summary>Sem animação (índice reservado).</summary>
    public const byte NoAnim = 255;

    /// <summary>O host está com o jogo pausado (menu, pausa).</summary>
    public const byte FlagHostPaused = 1;

    private const byte KnownFlags = FlagHostPaused;

    public IReadOnlyList<NetProjectile> Arrows => Projectiles ?? [];

    public IReadOnlyList<NetNpc> NpcList => Npcs ?? [];

    public bool HostPaused => (Flags & FlagHostPaused) != 0;

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
        var projectiles = Arrows;
        var npcs = NpcList;
        if (Hunters.Count > MaxHunters || Enemies.Count > MaxEnemies || projectiles.Count > MaxProjectiles || npcs.Count > MaxNpcs)
        {
            throw new ArgumentException($"snapshot grande demais ({Hunters.Count} caçadores, {Enemies.Count} monstros, {projectiles.Count} flechas, {npcs.Count} NPCs)");
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
            w.Byte(h.Kind);
            w.Byte(h.Stamina);
            w.Byte(h.Anim);
            w.Byte(h.Frame);
        }

        w.UShort((ushort)Enemies.Count);
        foreach (var e in Enemies)
        {
            w.Int(e.Id);
            w.Float(e.X);
            w.Float(e.Y);
            w.Float(e.Hp);
            w.Float(e.MaxHp);
            w.OptionalId(e.Type ?? string.Empty);
            w.Byte(e.Anim);
            w.Byte(e.Frame);
            w.Byte(e.FlipH ? (byte)1 : (byte)0);
        }

        var dialogue = TrimUtf8(Dialogue, MaxDialogueBytes);
        w.UShort((ushort)dialogue.Length);
        foreach (var b in dialogue)
        {
            w.Byte(b);
        }

        w.Byte((byte)(Flags & KnownFlags));
        w.Byte((byte)projectiles.Count);
        foreach (var p in projectiles)
        {
            w.Float(p.X);
            w.Float(p.Y);
            w.Float(p.Angle);
            w.Byte(p.Pierce ? (byte)1 : (byte)0);
        }

        w.Byte((byte)npcs.Count);
        foreach (var n in npcs)
        {
            w.Id(n.Id);
            w.Float(n.X);
            w.Float(n.Y);
            w.Byte(n.Facing);
            w.Byte(n.Walking ? (byte)1 : (byte)0);
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

        if (!TryReadHunters(ref r, out var hunters) || !TryReadEnemies(ref r, out var enemies))
        {
            return false;
        }

        if (!r.TryUShort(out var dialogueLength) || dialogueLength > MaxDialogueBytes || !r.TryBytes(dialogueLength, out var text))
        {
            return false;
        }

        var flags = r.Byte();
        if (r.Failed || (flags & ~KnownFlags) != 0 || !TryReadProjectiles(ref r, out var projectiles) || !TryReadNpcs(ref r, out var npcs) || !r.AtEnd)
        {
            return false;
        }

        snapshot = new WorldSnapshot(tick, levelId, hunters, enemies, System.Text.Encoding.UTF8.GetString(text), projectiles, npcs, flags);
        return true;
    }

    private static bool TryReadHunters(ref NetReader r, out List<NetHunter> hunters)
    {
        hunters = [];
        var count = r.Byte();
        if (r.Failed || count > MaxHunters)
        {
            return false;
        }

        var slots = new HashSet<byte>();
        for (var i = 0; i < count; i++)
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

            var kind = r.Byte();
            var stamina = r.Byte();
            var anim = r.Byte();
            var frame = r.Byte();
            if (r.Failed || kind > NetHunter.MaxKind)
            {
                return false;
            }

            hunters.Add(new NetHunter(slot, x, y, facing, downed == 1, hp, maxHp, kind, stamina, anim, frame));
        }

        return true;
    }

    private static bool TryReadEnemies(ref NetReader r, out List<NetEnemy> enemies)
    {
        enemies = [];
        if (!r.TryUShort(out var count) || count > MaxEnemies)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!r.TryInt(out var id) || !r.TryFloat(out var x) || !r.TryFloat(out var y) || !r.TryFloat(out var hp) || !r.TryFloat(out var maxHp) || !ValidHp(hp, maxHp)
                || !r.TryOptionalId(out var type))
            {
                return false;
            }

            var anim = r.Byte();
            var frame = r.Byte();
            var flip = r.Byte();
            if (r.Failed || flip > 1)
            {
                return false;
            }

            enemies.Add(new NetEnemy(id, x, y, hp, maxHp, type, anim, frame, flip == 1));
        }

        return true;
    }

    private static bool TryReadProjectiles(ref NetReader r, out List<NetProjectile> projectiles)
    {
        projectiles = [];
        var count = r.Byte();
        if (r.Failed || count > MaxProjectiles)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!r.TryFloat(out var x) || !r.TryFloat(out var y) || !r.TryFloat(out var angle))
            {
                return false;
            }

            var pierce = r.Byte();
            if (r.Failed || pierce > 1)
            {
                return false;
            }

            projectiles.Add(new NetProjectile(x, y, angle, pierce == 1));
        }

        return true;
    }

    private static bool TryReadNpcs(ref NetReader r, out List<NetNpc> npcs)
    {
        npcs = [];
        var count = r.Byte();
        if (r.Failed || count > MaxNpcs)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!r.TryId(out var id) || !r.TryFloat(out var x) || !r.TryFloat(out var y))
            {
                return false;
            }

            var facing = r.Byte();
            var walking = r.Byte();
            if (r.Failed || facing > 3 || walking > 1)
            {
                return false;
            }

            npcs.Add(new NetNpc(id, x, y, facing, walking == 1));
        }

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
