using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Core.Rules;
using Vespera.Core.Save;
using Vespera.Core.State;

namespace Vespera.Autoload;

/// <summary>
/// Dono da partida: guarda o <see cref="GameState"/> (Core) do save atual e repassa cada mudança para o
/// <see cref="EventBus"/>. Também troca cenas com fade. Use <see cref="State"/> para as operações completas
/// (comprar, equipar, forjar…); os atalhos abaixo mantêm a API da fundação.
/// </summary>
public partial class GameManager : Node
{
    private const float FadeSeconds = 0.35f;
    private const int DebugXpGrant = 25;

    /// <summary>Seed fixa no smoke test para o playtest ser reprodutível.</summary>
    private const int SmokeSeed = 20261020;

    private static readonly Color FadeColor = new("#1B1F3B");

    private GameState? _state;
    private int _lastHp;
    private ColorRect _fade = null!;
    private bool _changingScene;

    public static GameManager Instance { get; private set; } = null!;

    /// <summary>Estado do jogador (save + sessão). Troca de instância em <see cref="LoadGame"/> e <see cref="NewGame"/>.</summary>
    public GameState State => _state ?? throw new InvalidOperationException("Nenhuma partida carregada.");

    public static GameDatabase Db => Database.Db;

    public Rank Rank => State.Rank;

    public int TotalXp => State.Save.Xp;

    /// <summary>Progresso de 0 a 1 até o próximo Selo.</summary>
    public float ProgressInRank => Db.Progression.ProgressToNextRank(TotalXp);

    public int MaxHp => State.MaxHp;

    public int CurrentHp => State.Hp;

    /// <summary>Estilhaços de Brecha (o "Fragmento" da fundação é o material <c>estilhaco</c> da v2).</summary>
    public int Fragments => State.Shards;

    public int Lumes => State.Lumes;

    /// <summary>Nível da arma da classe.</summary>
    public int WeaponLevel => State.Save.WeaponLevel(State.Save.ClassId);

    public int Day => State.Save.Day;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _fade = CreateFadeOverlay();
        LoadGame();
    }

    public override void _ExitTree() => Detach();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (OS.IsDebugBuild() && @event.IsActionPressed(InputActions.DebugGrantXp))
        {
            AddXp(DebugXpGrant);
        }
    }

    /// <summary>Carrega o save (ou começa um jogo novo se não houver) e avisa com <c>GameLoaded</c>.</summary>
    public void LoadGame()
    {
        Attach(SaveService.Instance.LoadOrNew());
        Log.Info($"Partida carregada: Selo {Rank}, Nv. {State.Level.Level}, {TotalXp} XP, {Lumes} Lumes, {Fragments} Estilhaços.");
    }

    /// <summary>Começa um jogo novo (classe permanente). Não grava até <see cref="SaveGame"/>.</summary>
    public void NewGame(string? name, string classId)
    {
        Attach(SaveData.NewGame(Db, name, classId));
        Log.Info($"Jogo novo: {State.Save.Name} ({classId}).");
    }

    public void SaveGame() => SaveService.Instance.Save(State.Save);

    public void AddXp(int amount) => State.GainXp(amount);

    public void AddFragments(int amount) => State.AddMaterial(Db.Rules.Economy.ShardMaterial, amount);

    /// <summary>Gasta Estilhaços se houver saldo. Devolve false (e não gasta nada) se faltar.</summary>
    public bool TrySpendFragments(int amount) => State.TryRemoveMaterial(Db.Rules.Economy.ShardMaterial, amount);

    public void DamagePlayer(int amount) => State.Damage(amount);

    public void HealPlayer(int amount) => State.Heal(amount);

    public void RestoreFullHealth() => State.RestoreFullHealth();

    public void AdvanceDay() => State.NewDay();

    private void Attach(SaveData save)
    {
        Detach();
        Vespera.World.World.Objects.Reset(); // barril quebrado e baú aberto de outro jogo não passam para este (o registro é estático)
        var seed = Testing.SmokeTest.IsRequested() ? SmokeSeed : (int)(Time.GetTicksUsec() & int.MaxValue);
        _state = new GameState(Db, save, new SeededRandom(seed));
        _lastHp = _state.Hp;
        _state.XpGained += OnXpGained;
        _state.RankChanged += OnRankChanged;
        _state.LevelChanged += OnLevelChanged;
        _state.LumesChanged += OnLumesChanged;
        _state.MaterialChanged += OnMaterialChanged;
        _state.InventoryChanged += OnInventoryChanged;
        _state.EquipmentChanged += OnEquipmentChanged;
        _state.WeaponLevelChanged += OnWeaponLevelChanged;
        _state.PotionsChanged += OnPotionsChanged;
        _state.HealthChanged += OnHealthChanged;
        _state.StatsChanged += OnStatsChanged;
        _state.LanternChanged += OnLanternChanged;
        _state.DayChanged += OnDayChanged;
        _state.EcoChanged += OnEcoChanged;
        _state.QuestCompleted += OnQuestCompleted;
        Log.Debug($"GameState com seed {seed}.");
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameLoaded);
    }

    private void Detach()
    {
        if (_state is null)
        {
            return;
        }

        _state.XpGained -= OnXpGained;
        _state.RankChanged -= OnRankChanged;
        _state.LevelChanged -= OnLevelChanged;
        _state.LumesChanged -= OnLumesChanged;
        _state.MaterialChanged -= OnMaterialChanged;
        _state.InventoryChanged -= OnInventoryChanged;
        _state.EquipmentChanged -= OnEquipmentChanged;
        _state.WeaponLevelChanged -= OnWeaponLevelChanged;
        _state.PotionsChanged -= OnPotionsChanged;
        _state.HealthChanged -= OnHealthChanged;
        _state.StatsChanged -= OnStatsChanged;
        _state.LanternChanged -= OnLanternChanged;
        _state.DayChanged -= OnDayChanged;
        _state.EcoChanged -= OnEcoChanged;
        _state.QuestCompleted -= OnQuestCompleted;
        _state = null;
    }

    // --- GameState → EventBus ---

    private static EventBus Bus => EventBus.Instance;

    private void OnXpGained(XpGainResult result) =>
        Bus.EmitSignal(EventBus.SignalName.XpGained, result.Amount, result.TotalXp, ProgressInRank);

    private void OnRankChanged(Rank previous, Rank current)
    {
        Log.Info($"Selo {previous} → {current}");
        Bus.EmitSignal(EventBus.SignalName.RankChanged, (int)previous, (int)current);
        SaveGame(); // autosave ao subir de Selo (CLAUDE.md, seção 5)
    }

    private void OnLevelChanged(int previous, int current) => Bus.EmitSignal(EventBus.SignalName.LevelChanged, previous, current);

    private void OnLumesChanged(int total) => Bus.EmitSignal(EventBus.SignalName.LumesChanged, total);

    private void OnMaterialChanged(string materialId, int count)
    {
        Bus.EmitSignal(EventBus.SignalName.MaterialChanged, materialId, count);
        if (materialId == Db.Rules.Economy.ShardMaterial)
        {
            Bus.EmitSignal(EventBus.SignalName.FragmentsChanged, count);
        }
    }

    private void OnInventoryChanged() => Bus.EmitSignal(EventBus.SignalName.InventoryChanged);

    private void OnEquipmentChanged(string slotId) => Bus.EmitSignal(EventBus.SignalName.EquipmentChanged, slotId);

    private void OnWeaponLevelChanged(string weaponId, int level)
    {
        if (weaponId == State.Save.ClassId)
        {
            Bus.EmitSignal(EventBus.SignalName.WeaponUpgraded, level);
        }
    }

    private void OnPotionsChanged(int count) => Bus.EmitSignal(EventBus.SignalName.PotionsChanged, count);

    /// <summary>Traduz a mudança de HP para os sinais da fundação: dano, cura e morte.</summary>
    private void OnHealthChanged(int hp, int maxHp)
    {
        var previous = _lastHp;
        _lastHp = hp;
        if (hp < previous)
        {
            Bus.EmitSignal(EventBus.SignalName.PlayerDamaged, previous - hp, hp, maxHp);
            if (hp == 0)
            {
                Bus.EmitSignal(EventBus.SignalName.PlayerDied);
            }

            return;
        }

        Bus.EmitSignal(EventBus.SignalName.PlayerHealed, hp, maxHp);
    }

    private void OnStatsChanged(PlayerStats stats) => Bus.EmitSignal(EventBus.SignalName.PlayerStatsChanged);

    private void OnLanternChanged(bool lost) => Bus.EmitSignal(EventBus.SignalName.LanternStateChanged, lost);

    private void OnDayChanged(int day) => Bus.EmitSignal(EventBus.SignalName.DayChanged, day);

    private void OnEcoChanged(string ecoId) => Bus.EmitSignal(EventBus.SignalName.EcoChanged, ecoId);

    private void OnQuestCompleted(QuestDef quest) => Bus.EmitSignal(EventBus.SignalName.QuestCompleted, quest.Id);

    // --- Bloqueio de gameplay (diálogo, menus, pausa) ---
    private readonly HashSet<string> _gameplayBlocks = [];

    /// <summary>Enquanto true, o jogador não se move nem ataca. Cada tela registra o próprio motivo.</summary>
    public bool IsGameplayBlocked => _gameplayBlocks.Count > 0;

    public void BlockGameplay(string reason) => _gameplayBlocks.Add(reason);

    /// <summary>Motivos do bloqueio agora (diagnóstico do smoke).</summary>
    public IReadOnlyCollection<string> GameplayBlocks => _gameplayBlocks;

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
