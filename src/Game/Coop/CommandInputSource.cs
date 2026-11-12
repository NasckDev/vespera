using Godot;
using Vespera.Core.Coop;
using Vespera.Game.Boot;

namespace Vespera.Game.Coop;

/// <summary>
/// De onde vêm os comandos de um jogador. No co-op local, <see cref="LocalCommandProvider"/> amostra o teclado/controle; no
/// online, o host terá um provedor por cliente alimentado pela rede (ver <c>docs/v2/coop.md</c>).
/// </summary>
public interface ICommandProvider
{
    /// <summary>Dispositivo (para nomear os botões nos avisos): <see cref="LocalInputSource.KeyboardMouse"/> ou o índice do controle.</summary>
    int DeviceId { get; }

    /// <summary>O comando deste passo de física (chamado uma vez por passo).</summary>
    PlayerCommand Next(uint tick);
}

/// <summary>Ação nomeada do jogo ⇄ botão do <see cref="PlayerCommand"/>.</summary>
public static class CommandMap
{
    private static readonly (string Action, CommandButtons Button)[] Buttons =
    [
        (InputActions.Attack, CommandButtons.Attack),
        (InputActions.Heavy, CommandButtons.Heavy),
        (InputActions.Dodge, CommandButtons.Dodge),
        (InputActions.Skill, CommandButtons.Skill),
        (InputActions.Skill2, CommandButtons.Skill2),
        (InputActions.Interact, CommandButtons.Interact),
        (InputActions.Lantern, CommandButtons.Lantern),
        (InputActions.Inventory, CommandButtons.Inventory),
        (InputActions.Journal, CommandButtons.Journal),
        (InputActions.Pause, CommandButtons.Pause),
        (InputActions.Potion, CommandButtons.Potion),
    ];

    public static CommandButtons ButtonOf(string action)
    {
        foreach (var (name, button) in Buttons)
        {
            if (name == action)
            {
                return button;
            }
        }

        return CommandButtons.None;
    }

    /// <summary>Amostra uma fonte de input (direção + todos os botões, segurados e recém-apertados) num comando.</summary>
    public static PlayerCommand Sample(IInputSource source, uint tick, byte slot)
    {
        var held = CommandButtons.None;
        var pressed = CommandButtons.None;
        foreach (var (action, button) in Buttons)
        {
            if (source.IsPressed(action))
            {
                held |= button;
            }

            // Toda ação é consultada uma vez por passo: a borda de subida nunca fica velha.
            if (source.IsJustPressed(action))
            {
                pressed |= button;
            }
        }

        var move = source.Move;
        return new PlayerCommand(tick, slot, move.X, move.Y, held, pressed).Normalized();
    }
}

/// <summary>Comandos de um dispositivo local (teclado inteiro, meia-tecla, controle N, ou uma fonte roteirizada nos bots).</summary>
public sealed class LocalCommandProvider(IInputSource device, byte slot) : ICommandProvider
{
    public IInputSource Device => device;

    public int DeviceId => device.DeviceId;

    public PlayerCommand Next(uint tick) => CommandMap.Sample(device, tick, slot);
}

/// <summary>
/// O caçador lê esta fonte: ela pede UM comando por passo de física ao provedor e responde às consultas do
/// <see cref="Actors.Player"/> a partir dele. Um aperto vale uma vez (a segunda consulta no mesmo passo não o vê de novo).
/// É o ponto em que, no online, o comando vindo da rede entra na simulação do host.
/// </summary>
public sealed class CommandInputSource(ICommandProvider provider) : IInputSource
{
    private PlayerCommand _command;
    private ulong _frame = ulong.MaxValue;
    private CommandButtons _consumed;

    public ICommandProvider Provider => provider;

    public int DeviceId => provider.DeviceId;

    /// <summary>Comando do passo atual (o mesmo que a simulação está usando).</summary>
    public PlayerCommand Command => Current;

    public Vector2 Move
    {
        get
        {
            var c = Current;
            return new Vector2(c.MoveX, c.MoveY);
        }
    }

    public bool IsPressed(string action) => Current.IsHeld(CommandMap.ButtonOf(action));

    public bool IsJustPressed(string action)
    {
        var button = CommandMap.ButtonOf(action);
        var c = Current;
        if (!c.WasPressed(button) || (_consumed & button) != 0)
        {
            return false;
        }

        _consumed |= button;
        return true;
    }

    private PlayerCommand Current
    {
        get
        {
            var frame = Engine.GetPhysicsFrames();
            if (frame != _frame)
            {
                _frame = frame;
                _command = provider.Next((uint)frame);
                _consumed = CommandButtons.None;
            }

            return _command;
        }
    }
}
