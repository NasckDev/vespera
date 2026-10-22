using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.Actors;

/// <summary>
/// Junta várias fontes de input num só jogador (teclado + controles conectados, no modo solo).
/// O movimento vem da fonte mais inclinada; uma ação vale se qualquer fonte a apertou.
/// Lembra qual fonte foi usada por último, para o aviso mostrar a tecla ou o botão certo.
/// </summary>
public sealed class MergedInput(IReadOnlyList<IInputSource> sources) : IInputSource
{
    public int DeviceId => LastUsed.DeviceId;

    public IInputSource LastUsed { get; private set; } = sources[0];

    public Vector2 Move
    {
        get
        {
            var best = Vector2.Zero;
            foreach (var source in sources)
            {
                var move = source.Move;
                if (move.LengthSquared() > best.LengthSquared())
                {
                    best = move;
                    LastUsed = source;
                }
            }

            return best;
        }
    }

    public bool IsPressed(string action)
    {
        foreach (var source in sources)
        {
            if (source.IsPressed(action))
            {
                LastUsed = source;
                return true;
            }
        }

        return false;
    }

    public bool IsJustPressed(string action)
    {
        var any = false;
        foreach (var source in sources)
        {
            if (source.IsJustPressed(action))
            {
                LastUsed = source;
                any = true;
            }
        }

        return any;
    }

    /// <summary>Teclado e mouse mais todos os controles conectados agora.</summary>
    public static MergedInput LocalSolo()
    {
        var list = new List<IInputSource> { new LocalInputSource(LocalInputSource.KeyboardMouse) };
        foreach (var pad in Input.GetConnectedJoypads())
        {
            list.Add(new LocalInputSource(pad));
        }

        return new MergedInput(list);
    }
}
