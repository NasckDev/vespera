using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Combat;
using Vespera.Core.Ranks;
using Vespera.Core.Save;
using Vespera.Data;

namespace Vespera.Autoload;

/// <summary>
/// Estado da partida (XP, rank, HP, Fragmentos) e troca de cenas com fade.
/// Mudanças de estado são anunciadas pelo <see cref="EventBus"/>.
/// </summary>
public partial class GameManager : Node
{
    private const string RankTablePath = "res://src/Data/RankTable.tres";
    private const float FadeSeconds = 0.35f;
    private const int DebugXpGrant = 25;

    private static readonly Color FadeColor = new("#1B1F3B");

    private RankTableData _rankData = null!;
    private RankTable _rankTable = null!;
    private XpSystem _xp = null!;
    private ColorRect _fade = null!;
    private bool _changingScene;

    public static GameManager Instance { get; private set; } = null!;

    public Rank Rank => _xp.Rank;

    public int TotalXp => _xp.TotalXp;

    public float ProgressInRank => _xp.ProgressInRank;

    public int MaxHp { get; private set; }

    public int CurrentHp { get; private set; }

    public int Fragments { get; private set; }

    public int WeaponLevel { get; private set; } = 1;

    public int Day { get; private set; } = 1;

    public LanternData? Lantern { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _rankData = GD.Load<RankTableData>(RankTablePath);
        _rankTable = _rankData.ToRankTable();
        _fade = CreateFadeOverlay();
        LoadGame();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (OS.IsDebugBuild() && @event.IsActionPressed(InputActions.DebugGrantXp))
        {
            AddXp(DebugXpGrant);
        }
    }

    public void LoadGame()
    {
        var data = SaveService.Instance.LoadOrNew();
        _xp = new XpSystem(_rankTable, data.TotalXp);
        Fragments = data.Fragments;
        WeaponLevel = data.WeaponLevel;
        Day = data.Day;
        Lantern = data.Lantern;
        MaxHp = _rankTable.MaxHpFor(Rank, _rankData.BaseMaxHp);
        CurrentHp = MaxHp;
        Log.Info($"Partida carregada: rank {Rank}, {TotalXp} XP, {Fragments} Fragmentos.");
    }

    public void SaveGame() => SaveService.Instance.Save(new SaveData
    {
        TotalXp = TotalXp,
        Fragments = Fragments,
        WeaponLevel = WeaponLevel,
        Day = Day,
        Lantern = Lantern,
    });

    public void AddXp(int amount)
    {
        var result = _xp.Add(amount);
        EventBus.Instance.EmitSignal(EventBus.SignalName.XpGained, amount, result.TotalXp, ProgressInRank);

        if (!result.RankedUp)
        {
            return;
        }

        MaxHp = _rankTable.MaxHpFor(result.NewRank, _rankData.BaseMaxHp);
        CurrentHp = MaxHp;
        Log.Info($"Rank {result.PreviousRank} → {result.NewRank}");
        EventBus.Instance.EmitSignal(EventBus.SignalName.RankChanged, (int)result.PreviousRank, (int)result.NewRank);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealed, CurrentHp, MaxHp);
        SaveGame(); // autosave ao subir de rank (CLAUDE.md, seção 5)
    }

    public void AddFragments(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Fragments += amount;
        EventBus.Instance.EmitSignal(EventBus.SignalName.FragmentsChanged, Fragments);
    }

    /// <summary>Gasta Fragmentos se houver saldo. Devolve false (e não gasta nada) se faltar.</summary>
    public bool TrySpendFragments(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > Fragments)
        {
            return false;
        }

        Fragments -= amount;
        EventBus.Instance.EmitSignal(EventBus.SignalName.FragmentsChanged, Fragments);
        return true;
    }

    public void SetWeaponLevel(int level)
    {
        WeaponLevel = level;
        EventBus.Instance.EmitSignal(EventBus.SignalName.WeaponUpgraded, WeaponLevel);
        SaveGame(); // autosave ao forjar
    }

    public void DamagePlayer(int amount)
    {
        if (CurrentHp == 0)
        {
            return;
        }

        CurrentHp = DamageCalculator.ApplyDamage(CurrentHp, amount);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDamaged, amount, CurrentHp, MaxHp);
        if (CurrentHp == 0)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerDied);
        }
    }

    public void HealPlayer(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealed, CurrentHp, MaxHp);
    }

    public void RestoreFullHealth() => HealPlayer(MaxHp);

    /// <summary>Guarda ou limpa a Lanterna (Fragmentos deixados na Brecha ao morrer).</summary>
    public void SetLantern(LanternData? lantern) => Lantern = lantern;

    public void AdvanceDay() => Day++;

    // --- Bloqueio de gameplay (diálogo, menus, pausa) ---
    private readonly HashSet<string> _gameplayBlocks = [];

    /// <summary>Enquanto true, o jogador não se move nem ataca. Cada tela registra o próprio motivo.</summary>
    public bool IsGameplayBlocked => _gameplayBlocks.Count > 0;

    public void BlockGameplay(string reason) => _gameplayBlocks.Add(reason);

    public void UnblockGameplay(string reason) => _gameplayBlocks.Remove(reason);

    /// <summary>Troca a cena atual com fade para o índigo da marca.</summary>
    public async void ChangeScene(string scenePath)
    {
        if (_changingScene)
        {
            return;
        }

        _changingScene = true;
        await FadeTo(1f);

        var error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            Log.Error($"Falha ao trocar para {scenePath}: {error}");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);
        await FadeTo(0f);
        _changingScene = false;
    }

    private async Task FadeTo(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "color:a", alpha, FadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private ColorRect CreateFadeOverlay()
    {
        var layer = new CanvasLayer { Layer = 100, Name = "Fade" };
        var rect = new ColorRect
        {
            Color = FadeColor with { A = 0f },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        AddChild(layer);
        return rect;
    }
}
