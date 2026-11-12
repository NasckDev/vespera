using System.Collections.Generic;

namespace Vespera.Game.Boot;

/// <summary>Coluna de associação de uma ação: duas de teclado/mouse e uma de controle.</summary>
public enum BindingSlot
{
    Primary = 0,
    Alternate = 1,
    Gamepad = 2,
}

/// <summary>
/// Tabela ação -> 3 associações em texto (<c>key:87</c>, <c>mouse:1</c>, <c>pad:2</c>, <c>axis:5+</c>). Lógica pura,
/// sem Godot: detecta conflito, troca, restaura padrão e serializa. O mapeamento para <c>InputMap</c> fica em InputSetup.
/// </summary>
public sealed class BindingTable
{
    public const int SlotCount = 3;
    public const string Empty = "";

    // Códigos de tecla (Godot.Key como inteiro) usados nos padrões.
    private const int KeyW = 87, KeyA = 65, KeyS = 83, KeyD = 68, KeyJ = 74, KeyK = 75, KeyQ = 81, KeyE = 69;
    private const int KeyF = 70, KeyI = 73, KeyP = 80, KeySpace = 32, KeyL = 76, KeyR = 82;
    private const int KeyEscape = 4194305, KeyTab = 4194306, KeyShift = 4194325;
    private const int KeyLeft = 4194319, KeyUp = 4194320, KeyRight = 4194321, KeyDown = 4194322;

    // Teclado numérico (Godot.Key: KpEnter, KpMultiply, KpSubtract, KpPeriod, KpAdd, Kp0..Kp3) e Enter.
    public const int KeyEnter = 4194309, KeyKpEnter = 4194310, KeyKpMultiply = 4194433, KeyKpSubtract = 4194435;
    public const int KeyKpPeriod = 4194436, KeyKpAdd = 4194437, KeyKp0 = 4194438, KeyKp1 = 4194439, KeyKp2 = 4194440, KeyKp3 = 4194441, KeyKp4 = 4194442;

    private readonly Dictionary<string, string[]> _slots = [];

    public static BindingTable Defaults()
    {
        var t = new BindingTable();
        t.Set("move_up", $"key:{KeyW}", $"key:{KeyUp}", "axis:1-");
        t.Set("move_down", $"key:{KeyS}", $"key:{KeyDown}", "axis:1+");
        t.Set("move_left", $"key:{KeyA}", $"key:{KeyLeft}", "axis:0-");
        t.Set("move_right", $"key:{KeyD}", $"key:{KeyRight}", "axis:0+");
        t.Set("attack", $"key:{KeyJ}", "mouse:1", "pad:2");
        t.Set("heavy", $"key:{KeyK}", "mouse:2", "pad:3");
        t.Set("dodge", $"key:{KeySpace}", $"key:{KeyShift}", "pad:1");
        t.Set("skill", $"key:{KeyQ}", Empty, "axis:5+");
        t.Set("interact", $"key:{KeyE}", Empty, "pad:0");
        t.Set("lantern", $"key:{KeyF}", Empty, "pad:10");
        t.Set("potion", $"key:{KeyR}", Empty, "axis:4+");
        t.Set("inventory", $"key:{KeyTab}", $"key:{KeyI}", "pad:4");
        t.Set("journal", $"key:{KeyL}", Empty, "pad:9");
        t.Set("pause", $"key:{KeyEscape}", $"key:{KeyP}", "pad:6");
        return t;
    }

    /// <summary>
    /// Padrões do jogador 2 no co-op local: metade direita do teclado (setas + teclado numérico, Enter também interage) ou o
    /// controle dele. As ações são as mesmas do jogador 1; só as teclas mudam.
    /// </summary>
    public static BindingTable DefaultsPlayer2()
    {
        var t = new BindingTable();
        t.Set("move_up", $"key:{KeyUp}", Empty, "axis:1-");
        t.Set("move_down", $"key:{KeyDown}", Empty, "axis:1+");
        t.Set("move_left", $"key:{KeyLeft}", Empty, "axis:0-");
        t.Set("move_right", $"key:{KeyRight}", Empty, "axis:0+");
        t.Set("attack", $"key:{KeyKp1}", Empty, "pad:2");
        t.Set("heavy", $"key:{KeyKp2}", Empty, "pad:3");
        t.Set("dodge", $"key:{KeyKp0}", Empty, "pad:1");
        t.Set("skill", $"key:{KeyKp3}", Empty, "axis:5+");
        t.Set("interact", $"key:{KeyKpEnter}", $"key:{KeyEnter}", "pad:0");
        t.Set("lantern", $"key:{KeyKpAdd}", Empty, "pad:10");
        t.Set("potion", $"key:{KeyKp4}", Empty, "axis:4+");
        t.Set("inventory", $"key:{KeyKpPeriod}", Empty, "pad:4");
        t.Set("journal", $"key:{KeyKpSubtract}", Empty, "pad:9");
        t.Set("pause", $"key:{KeyKpMultiply}", Empty, "pad:6");
        return t;
    }

    public IEnumerable<string> Actions => _slots.Keys;

    /// <summary>Verdadeiro se algum slot de teclado/mouse da tabela usa <paramref name="code"/>.</summary>
    public bool UsesKeyboardCode(string code)
    {
        foreach (var slots in _slots.Values)
        {
            if (slots[(int)BindingSlot.Primary] == code || slots[(int)BindingSlot.Alternate] == code)
            {
                return true;
            }
        }

        return false;
    }

    public string Get(string action, BindingSlot slot) =>
        _slots.TryGetValue(action, out var s) ? s[(int)slot] : Empty;

    public void Set(string action, string primary, string alternate, string gamepad) =>
        _slots[action] = [primary, alternate, gamepad];

    /// <summary>
    /// Associa <paramref name="code"/>. Se outra ação do mesmo tipo de dispositivo já usava o código, as duas
    /// trocam de lugar (a outra recebe o que estava aqui). Devolve o id da ação em conflito, ou null.
    /// </summary>
    public string? Assign(string action, BindingSlot slot, string code)
    {
        if (!_slots.TryGetValue(action, out var mine))
        {
            return null;
        }

        var previous = mine[(int)slot];
        if (previous == code)
        {
            return null;
        }

        string? conflict = null;
        foreach (var (otherAction, otherSlots) in _slots)
        {
            foreach (var otherSlot in SlotsOfSameDevice(slot))
            {
                if (otherSlots[(int)otherSlot] != code || (otherAction == action && otherSlot == slot))
                {
                    continue;
                }

                otherSlots[(int)otherSlot] = previous;
                conflict = otherAction;
            }
        }

        mine[(int)slot] = code;
        return conflict;
    }

    public void Clear(string action, BindingSlot slot)
    {
        if (_slots.TryGetValue(action, out var s))
        {
            s[(int)slot] = Empty;
        }
    }

    public Dictionary<string, string[]> ToDictionary()
    {
        var copy = new Dictionary<string, string[]>();
        foreach (var (action, s) in _slots)
        {
            copy[action] = (string[])s.Clone();
        }

        return copy;
    }

    /// <summary>Aplica por cima dos padrões o que veio salvo; ações desconhecidas e tamanhos errados são ignorados.</summary>
    public static BindingTable FromDictionary(IReadOnlyDictionary<string, string[]>? saved) => FromDictionary(saved, Defaults());

    /// <summary>Como <see cref="FromDictionary(IReadOnlyDictionary{string, string[]}?)"/>, por cima de outros padrões (jogador 2).</summary>
    public static BindingTable FromDictionary(IReadOnlyDictionary<string, string[]>? saved, BindingTable defaults)
    {
        var table = defaults;
        if (saved is null)
        {
            return table;
        }

        foreach (var (action, values) in saved)
        {
            if (table._slots.ContainsKey(action) && values is { Length: SlotCount })
            {
                table._slots[action] = (string[])values.Clone();
            }
        }

        return table;
    }

    private static BindingSlot[] SlotsOfSameDevice(BindingSlot slot) =>
        slot == BindingSlot.Gamepad
            ? [BindingSlot.Gamepad]
            : [BindingSlot.Primary, BindingSlot.Alternate];
}
