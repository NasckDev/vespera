using System.Collections.Generic;
using Godot;
using Vespera.Game.Boot;

namespace Vespera.Game.Testing;

/// <summary>Fonte de input dirigida por código (bots de teste e capturas): direção e ações ligadas à mão.</summary>
public sealed class ScriptedInput : IInputSource
{
    private readonly HashSet<string> _held = [];
    private readonly HashSet<string> _taps = [];

    /// <summary>Finge ser o teclado, para os avisos das capturas mostrarem as teclas do jogador.</summary>
    public int DeviceId => LocalInputSource.KeyboardMouse;

    public Vector2 Move { get; set; }

    public bool IsPressed(string action) => _held.Contains(action);

    public bool IsJustPressed(string action) => _taps.Remove(action);

    /// <summary>Gera uma borda de subida que é consumida na próxima consulta.</summary>
    public void Tap(string action) => _taps.Add(action);

    public void Hold(string action, bool held)
    {
        if (held)
        {
            _held.Add(action);
            return;
        }

        _held.Remove(action);
    }
}
