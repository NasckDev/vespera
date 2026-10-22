using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.UI;
using Vespera.UI.Common;
using Vespera.UI.HudKit;
using Vespera.UI.Screens;

namespace Vespera.Testing;

/// <summary>
/// Frente C1, rodada 1 de correções (POLI-1 a POLI-16): regressões das falhas que o QA provou, com prova antes/depois.
/// </summary>
public partial class SmokeTest
{
    [SmokeScenario("polimento (r1): save e opções do smoke são exclusivos do processo (rodadas em paralelo não se pisam)", order: 1100)]
    private async Task PrIsolatedSmokeFiles()
    {
        var pid = OS.GetProcessId().ToString();
        var save = SaveService.Instance.SavePath;
        var settings = AudioManager.Instance.SettingsFile;
        Log.Info($"poli-16: save {save}, opções {settings}");
        Require(save.Contains(pid, StringComparison.Ordinal), $"o save do smoke ({save}) deveria ter o id do processo {pid}");
        Require(settings.Contains(pid, StringComparison.Ordinal), $"as opções do smoke ({settings}) deveriam ter o id do processo {pid}");
        await Task.CompletedTask;
    }

    [SmokeScenario("polimento (r1): remapear para tecla ou botão fixo (1–5, F2, RB, mira) é recusado com o nome da ação e nada muda", order: 1101)]
    private async Task PrReservedKeysRefused()
    {
        InputBindings.RestoreDefaults();
        try
        {
            var keys = new[] { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.F2 };
            foreach (var key in keys)
            {
                var before = InputBindings.Label(InputActions.Attack, false);
                var result = InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = key }, out var other);
                Log.Info($"poli-6/13: Atacar ← {key}: {result}, dona '{other}' ({(other is null ? "-" : InputBindings.ActionLabel(other))})");
                Require(result == RemapResult.Conflict, $"Atacar ← {key} deveria dar Conflict, deu {result}");
                Require(other is not null && InputBindings.ActionLabel(other) != other.ToString(), $"o conflito de {key} não diz o nome da ação ('{other}')");
                Require(InputBindings.Label(InputActions.Attack, false) == before, $"Atacar mudou de tecla ao recusar {key}");
            }

            var pads = new (JoyButton? Button, JoyAxis? Axis, float Value)[]
            {
                (JoyButton.RightShoulder, null, 0),
                (null, JoyAxis.RightX, -1f),
                (null, JoyAxis.RightX, 1f),
                (null, JoyAxis.RightY, -1f),
                (null, JoyAxis.RightY, 1f),
            };
            foreach (var (button, axis, value) in pads)
            {
                InputEvent e = button is { } b ? new InputEventJoypadButton { ButtonIndex = b } : new InputEventJoypadMotion { Axis = axis!.Value, AxisValue = value };
                var before = InputBindings.Label(InputActions.Dodge, true);
                var result = InputBindings.Assign(InputActions.Dodge, pad: true, e, out var other);
                Log.Info($"poli-6/13: Esquivar ← {InputBindings.Describe(e)}: {result}, dona '{other}'");
                Require(result == RemapResult.Conflict, $"Esquivar ← {InputBindings.Describe(e)} deveria dar Conflict, deu {result}");
                Require(InputBindings.Label(InputActions.Dodge, true) == before, "Esquivar mudou de botão ao recusar");
            }

            // Uma tecla livre continua valendo.
            Require(InputBindings.Assign(InputActions.Attack, pad: false, new InputEventKey { PhysicalKeycode = Key.H }, out _) == RemapResult.Ok, "H está livre");
        }
        finally
        {
            InputBindings.RestoreDefaults();
        }

        await Task.CompletedTask;
    }
}
