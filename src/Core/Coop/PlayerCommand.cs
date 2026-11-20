using System.Buffers.Binary;

namespace Vespera.Core.Coop;

/// <summary>Botões de um comando de jogador (uma ação nomeada do jogo cada).</summary>
[Flags]
public enum CommandButtons : ushort
{
    None = 0,
    Attack = 1 << 0,
    Heavy = 1 << 1,
    Dodge = 1 << 2,
    Skill = 1 << 3,
    Interact = 1 << 4,
    Lantern = 1 << 5,
    Inventory = 1 << 6,
    Journal = 1 << 7,
    Pause = 1 << 8,
    Potion = 1 << 9,
    Skill2 = 1 << 10,
    Supremo = 1 << 11,
}

/// <summary>
/// O que um jogador quer neste passo da simulação: direção (-1..1) e botões segurados e recém-apertados. É a única coisa
/// que sai de uma fonte de input e entra na simulação; no co-op local vem do teclado/controle, no online virá da rede
/// (o host recebe o comando de cada cliente e o aplica no caçador dele). Serializa em <see cref="WireSize"/> bytes.
/// </summary>
public readonly record struct PlayerCommand(uint Tick, byte Slot, float MoveX, float MoveY, CommandButtons Held, CommandButtons Pressed)
{
    /// <summary>tick(4) + slot(1) + moveX(2) + moveY(2) + held(2) + pressed(2).</summary>
    public const int WireSize = 13;

    private const float AxisScale = 32767f;

    public static PlayerCommand Idle(uint tick, byte slot) => new(tick, slot, 0f, 0f, CommandButtons.None, CommandButtons.None);

    public bool IsHeld(CommandButtons button) => (Held & button) == button && button != CommandButtons.None;

    public bool WasPressed(CommandButtons button) => (Pressed & button) == button && button != CommandButtons.None;

    /// <summary>Direção limitada ao círculo unitário (diagonal do teclado não anda mais rápido).</summary>
    public PlayerCommand Normalized()
    {
        var length = MathF.Sqrt((MoveX * MoveX) + (MoveY * MoveY));
        return length <= 1f ? this : this with { MoveX = MoveX / length, MoveY = MoveY / length };
    }

    /// <summary>Junta um comando que chegou depois no mesmo passo: direção nova, botões somados (nenhum aperto se perde).</summary>
    public PlayerCommand Merge(PlayerCommand later) =>
        later with { Pressed = Pressed | later.Pressed };

    public void Write(Span<byte> buffer)
    {
        if (buffer.Length < WireSize)
        {
            throw new ArgumentException($"buffer menor que {WireSize} bytes", nameof(buffer));
        }

        var n = Normalized();
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Tick);
        buffer[4] = Slot;
        BinaryPrimitives.WriteInt16LittleEndian(buffer[5..], Quantize(n.MoveX));
        BinaryPrimitives.WriteInt16LittleEndian(buffer[7..], Quantize(n.MoveY));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[9..], (ushort)Held);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[11..], (ushort)Pressed);
    }

    public static PlayerCommand Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < WireSize)
        {
            throw new ArgumentException($"comando precisa de {WireSize} bytes", nameof(buffer));
        }

        return new PlayerCommand(
            BinaryPrimitives.ReadUInt32LittleEndian(buffer),
            buffer[4],
            BinaryPrimitives.ReadInt16LittleEndian(buffer[5..]) / AxisScale,
            BinaryPrimitives.ReadInt16LittleEndian(buffer[7..]) / AxisScale,
            (CommandButtons)BinaryPrimitives.ReadUInt16LittleEndian(buffer[9..]),
            (CommandButtons)BinaryPrimitives.ReadUInt16LittleEndian(buffer[11..]));
    }

    private static short Quantize(float value) => (short)Math.Round(Math.Clamp(value, -1f, 1f) * AxisScale);
}
