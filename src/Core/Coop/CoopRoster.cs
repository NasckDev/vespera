namespace Vespera.Core.Coop;

/// <summary>Tipo de dispositivo de um jogador local. Dois teclados físicos são o mesmo teclado para o motor, daí a metade direita.</summary>
public enum DeviceKind
{
    /// <summary>Teclado (e mouse). Com alguém na metade direita, fica só com a esquerda: WASD + J K L + Espaço.</summary>
    Keyboard,

    /// <summary>Metade direita do mesmo teclado: setas + teclado numérico.</summary>
    KeyboardRight,

    Gamepad,
}

/// <summary>Um dispositivo de entrada: tipo e, para controle, o índice do motor (0, 1...).</summary>
public readonly record struct InputDevice(DeviceKind Kind, int Pad = 0)
{
    public static InputDevice Keyboard => new(DeviceKind.Keyboard);

    public static InputDevice KeyboardRight => new(DeviceKind.KeyboardRight);

    public static InputDevice Gamepad(int pad) => new(DeviceKind.Gamepad, pad);

    public bool IsKeyboard => Kind is DeviceKind.Keyboard or DeviceKind.KeyboardRight;

    public override string ToString() => Kind switch
    {
        DeviceKind.Keyboard => "keyboard",
        DeviceKind.KeyboardRight => "keyboard_right",
        _ => $"pad{Pad}",
    };
}

/// <summary>Um jogador na tela de entrada: dispositivo, classe (id da arma) e se já confirmou.</summary>
public sealed class RosterSlot(InputDevice device, string classId)
{
    public InputDevice Device { get; } = device;

    public string ClassId { get; set; } = classId;

    public bool Ready { get; set; }

    /// <summary>Entrou antes (J1 no "entrar no meio do jogo"): não sai pela tela de entrada.</summary>
    public bool Locked { get; init; }
}

/// <summary>
/// Quem joga e com o quê (tela "Co-op local"): cada jogador entra apertando um botão no próprio dispositivo, escolhe a
/// classe e confirma. A ordem de entrada dá o número (J1, J2...). Lógica pura, testada sem a engine.
/// </summary>
public sealed class CoopRoster
{
    private readonly List<RosterSlot> _slots = [];
    private readonly IReadOnlyList<string> _classes;

    public CoopRoster(int maxPlayers, IReadOnlyList<string> classIds)
    {
        if (classIds is null || classIds.Count == 0)
        {
            throw new ArgumentException("é preciso ao menos uma classe", nameof(classIds));
        }

        MaxPlayers = Math.Max(1, maxPlayers);
        _classes = classIds;
    }

    public int MaxPlayers { get; }

    public IReadOnlyList<RosterSlot> Slots => _slots;

    public int Count => _slots.Count;

    public bool IsFull => _slots.Count >= MaxPlayers;

    /// <summary>Alguém na metade direita do teclado e alguém no teclado: o teclado se divide ao meio.</summary>
    public bool KeyboardSplit => Has(DeviceKind.KeyboardRight) && Has(DeviceKind.Keyboard);

    /// <summary>Dá para começar: dois ou mais jogadores, todos confirmados.</summary>
    public bool CanStart => _slots.Count >= 2 && _slots.All(s => s.Ready);

    public int IndexOf(InputDevice device) => _slots.FindIndex(s => s.Device == device);

    /// <summary>Entra um jogador novo com a classe padrão do número dele; null se o dispositivo já está dentro ou o grupo está cheio.</summary>
    public RosterSlot? Join(InputDevice device, bool locked = false)
    {
        if (IsFull || IndexOf(device) >= 0)
        {
            return null;
        }

        var slot = new RosterSlot(device, _classes[_slots.Count % _classes.Count]) { Locked = locked };
        _slots.Add(slot);
        return slot;
    }

    /// <summary>Sai (os de trás sobem um número). Jogador travado não sai.</summary>
    public bool Leave(InputDevice device)
    {
        var index = IndexOf(device);
        if (index < 0 || _slots[index].Locked)
        {
            return false;
        }

        _slots.RemoveAt(index);
        return true;
    }

    /// <summary>Troca a classe (só antes de confirmar).</summary>
    public bool CycleClass(InputDevice device, int step)
    {
        var index = IndexOf(device);
        if (index < 0 || _slots[index].Ready)
        {
            return false;
        }

        var slot = _slots[index];
        var current = Math.Max(0, IndexOfClass(slot.ClassId));
        slot.ClassId = _classes[((current + step) % _classes.Count + _classes.Count) % _classes.Count];
        return true;
    }

    public bool SetReady(InputDevice device, bool ready)
    {
        var index = IndexOf(device);
        if (index < 0)
        {
            return false;
        }

        _slots[index].Ready = ready;
        return true;
    }

    /// <summary>Alguém antes deste jogador escolheu a mesma classe: ele ganha a capa com outro tom para não confundir.</summary>
    public bool NeedsCapeTint(int index) =>
        index > 0 && index < _slots.Count && _slots.Take(index).Any(s => s.ClassId == _slots[index].ClassId);

    private bool Has(DeviceKind kind) => _slots.Any(s => s.Device.Kind == kind);

    private int IndexOfClass(string classId)
    {
        for (var i = 0; i < _classes.Count; i++)
        {
            if (_classes[i] == classId)
            {
                return i;
            }
        }

        return -1;
    }
}
