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
}
