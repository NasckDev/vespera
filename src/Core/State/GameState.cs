using Vespera.Core.Combat;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Ranks;
using Vespera.Core.Rules;
using Vespera.Core.Save;

namespace Vespera.Core.State;

/// <summary>Resultado de uma compra/forja/serviço.</summary>
public enum PurchaseResult
{
    Ok,

    /// <summary>Faltam Lumes ou materiais (nada foi gasto).</summary>
    CannotAfford,

    /// <summary>Item único que o jogador já tem.</summary>
    AlreadyOwned,

    /// <summary>Já está no nível máximo.</summary>
    MaxLevel,

    /// <summary>Bloqueado pela classe (ou requisito equivalente).</summary>
    Locked,

    /// <summary>Não se aplica agora (ex.: resgate sem Lanterna perdida, arma que não possui).</summary>
    NotAvailable,
}

public enum PotionResult
{
    Used,
    NoPotions,
    FullHealth,
    OnCooldown,
}

/// <summary>
/// Estado do jogador em jogo: o <see cref="SaveData"/> mais o que é só de sessão (HP atual, invasão, recarga da poção),
/// com as operações da v2 que mudam esse estado. Cada mudança dispara um evento C# simples; o GameManager (Godot)
/// repassa para o EventBus. Não depende do Godot. Uso: <c>new GameState(db, save, random)</c>.
/// </summary>
public sealed class GameState
{
    private double _potionReadyAt = double.NegativeInfinity;

    public GameState(GameDatabase db, SaveData save, IRandom random)
    {
        Db = db ?? throw new ArgumentNullException(nameof(db));
        Save = save ?? throw new ArgumentNullException(nameof(save));
        Random = random ?? throw new ArgumentNullException(nameof(random));
        StatsCalculator = new PlayerStatsCalculator(db);
        Prices = new PriceCalculator(db);
        Upgrades = new UpgradeCosts(db);
        EquipmentRules = new EquipmentRules(db);
        LanternRules = new LanternRules(db);
        QuestRules = new QuestRules(db);
        Sale = new MaterialSale(db);
        Calendar = new SeasonCalendar(db.SeasonData);
        Brechas = new BrechaRules(db);
        Stats = StatsCalculator.Compute(save);
        Hp = Stats.Hp;
    }

    // --- Eventos (todos depois da mudança) ---

    public event Action<XpGainResult>? XpGained;

    /// <summary>Selo mudou (anterior, novo).</summary>
    public event Action<Rank, Rank>? RankChanged;

    /// <summary>Nível mudou (anterior, novo).</summary>
    public event Action<int, int>? LevelChanged;

    public event Action<int>? LumesChanged;

    /// <summary>Quantidade de um material mudou (id, nova quantidade).</summary>
    public event Action<string, int>? MaterialChanged;

    /// <summary>Mochila, materiais, poções ou lanternas carregadas mudaram.</summary>
    public event Action? InventoryChanged;

    /// <summary>Slot equipado mudou (id do slot).</summary>
    public event Action<string>? EquipmentChanged;

    /// <summary>Arma ganha ou nível mudou (arma, nível).</summary>
    public event Action<string, int>? WeaponLevelChanged;

    public event Action<int>? PotionsChanged;

    /// <summary>HP atual ou máximo mudou (atual, máximo).</summary>
    public event Action<int, int>? HealthChanged;

    /// <summary><see cref="Stats"/> foi recalculado.</summary>
    public event Action<PlayerStats>? StatsChanged;

    /// <summary>A Lanterna ficou na Brecha (true) ou voltou (false).</summary>
    public event Action<bool>? LanternChanged;

    public event Action<int>? DayChanged;

    /// <summary>Eco absorvido, equipado ou afinado (id).</summary>
    public event Action<string>? EcoChanged;

    /// <summary>Infusão comprada ou trocada (arma).</summary>
    public event Action<string>? InfusionChanged;

    public event Action<QuestDef>? QuestCompleted;

    // --- Dados e regras ---

    public GameDatabase Db { get; }

    public SaveData Save { get; }

    public IRandom Random { get; }

    public PlayerStatsCalculator StatsCalculator { get; }

    public PriceCalculator Prices { get; }

    public UpgradeCosts Upgrades { get; }

    public EquipmentRules EquipmentRules { get; }

    public LanternRules LanternRules { get; }

    public QuestRules QuestRules { get; }

    public MaterialSale Sale { get; }

    public SeasonCalendar Calendar { get; }

    public BrechaRules Brechas { get; }

    public Progression Progression => Db.Progression;

    // --- Estado derivado ---

    public PlayerStats Stats { get; private set; }

    public int MaxHp => Stats.Hp;

    /// <summary>HP atual (só de sessão: ao carregar começa cheio).</summary>
    public int Hp { get; private set; }

    public bool IsDead => Hp <= 0;

    public int RankIndex => Progression.RankIndex(Save.Xp);

    public Rank Rank => (Rank)RankIndex;

    public RankDef RankDef => Db.Ranks[RankIndex];

    public LevelInfo Level => Progression.LevelOf(Save.Xp);

    public int Lumes => Save.Lumes;

    /// <summary>Estilhaços de Brecha (material-moeda do HUD).</summary>
    public int Shards => Save.MaterialCount(Db.Rules.Economy.ShardMaterial);

    public SeasonDef Season => Calendar.SeasonOf(Save.Day);

    public bool HasLostLantern => Save.LostLantern is not null;

    /// <summary>Invasão em curso: preços ×1.5. Estado de sessão; o mundo liga e desliga.</summary>
    public bool Invasion { get; set; }

    // --- XP ---

    /// <summary>Soma XP; no rank-up/level-up o HP atual recebe o aumento do HP máximo (<c>combat.js:217</c>).</summary>
    public XpGainResult GainXp(int amount)
    {
        var result = Progression.Gain(Save.Xp, amount);
        Save.Xp = result.TotalXp;
        XpGained?.Invoke(result);
        if (result.LeveledUp || result.RankedUp)
        {
            RefreshStats(HpPolicy.AddDelta);
        }

        if (result.LeveledUp)
        {
            LevelChanged?.Invoke(result.PreviousLevel, result.Level);
        }

        if (result.RankedUp)
        {
            RankChanged?.Invoke(result.PreviousRank, result.NewRank);
        }

        return result;
    }

    // --- Lumes e materiais ---

    public void AddLumes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0)
        {
            return;
        }

        Save.Lumes += amount;
        LumesChanged?.Invoke(Save.Lumes);
    }

    /// <summary>Gasta Lumes exatos (sem multiplicador de preço). False e nada gasto se faltar.</summary>
    public bool TrySpendLumes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > Save.Lumes)
        {
            return false;
        }

        if (amount > 0)
        {
            Save.Lumes -= amount;
            LumesChanged?.Invoke(Save.Lumes);
        }

        return true;
    }

    /// <summary>Lumes cobrados por um preço base agora (×0.9; ×1.35 em invasão; arredondamento JS).</summary>
    public int CostLumes(int baseLumes) => Prices.CostLumes(baseLumes, Invasion);

    public bool CanPay(Cost cost) => Prices.CanPay(Save, cost, Invasion);

    /// <summary>Paga um custo de loja (Lumes com multiplicador, materiais crus). False e nada gasto se faltar.</summary>
    public bool TryPay(Cost cost)
    {
        var lumes = Save.Lumes;
        var mats = cost.Mats.Keys.ToList();
        if (!Prices.TryPay(Save, cost, Invasion))
        {
            return false;
        }

        if (Save.Lumes != lumes)
        {
            LumesChanged?.Invoke(Save.Lumes);
        }

        if (mats.Count > 0)
        {
            foreach (var id in mats)
            {
                MaterialChanged?.Invoke(id, Save.MaterialCount(id));
            }

            InventoryChanged?.Invoke();
        }

        return true;
    }

    /// <summary>"54 Lumes · 2× Presa Rubra (3)".</summary>
    public string DescribeCost(Cost cost) => Prices.Describe(cost, Save, Invasion);

    public int MaterialCount(string materialId) => Save.MaterialCount(materialId);

    public void AddMaterial(string materialId, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!Db.Materials.ContainsKey(materialId))
        {
            throw new ArgumentException($"Material desconhecido: '{materialId}'.", nameof(materialId));
        }

        if (amount == 0)
        {
            return;
        }

        Save.Materials[materialId] = Save.MaterialCount(materialId) + amount;
        MaterialChanged?.Invoke(materialId, Save.Materials[materialId]);
        InventoryChanged?.Invoke();
    }

    public bool TryRemoveMaterial(string materialId, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var have = Save.MaterialCount(materialId);
        if (have < amount)
        {
            return false;
        }

        SetMaterial(materialId, have - amount);
        return true;
    }

    /// <summary>Recebe Lumes, materiais e equipamentos de uma bolsa de Brecha (saída normal ou fuga).</summary>
    public void MergeBag(RunBag bag)
    {
        var lumes = Save.Lumes;
        LanternRules.MergeBag(Save, bag);
        if (Save.Lumes != lumes)
        {
            LumesChanged?.Invoke(Save.Lumes);
        }

        foreach (var id in bag.Materials.Keys)
        {
            MaterialChanged?.Invoke(id, Save.MaterialCount(id));
        }

        InventoryChanged?.Invoke();
    }

    // --- Equipamento ---

    public void AddGear(string gearId)
    {
        if (!Db.Gear.ContainsKey(gearId))
        {
            throw new ArgumentException($"Equipamento desconhecido: '{gearId}'.", nameof(gearId));
        }

        Save.Backpack.Add(gearId);
        InventoryChanged?.Invoke();
    }

    /// <summary>Por que este equipamento não pode ser equipado agora (classe/Selo), ou null.</summary>
    public string? EquipLockReason(string gearId) => EquipmentRules.LockReason(gearId, Save.ClassId, RankIndex);

    /// <summary>
    /// Equipa o item da mochila no índice dado; o que estava no slot volta para a mochila. Devolve o motivo do bloqueio
    /// (e não equipa) ou null. HP atual recebe o delta, entre 1 e o máximo (<c>equipGear</c>, <c>ui.js:91</c>).
    /// </summary>
    public string? Equip(int backpackIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(backpackIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(backpackIndex, Save.Backpack.Count);
        var gearId = Save.Backpack[backpackIndex];
        if (EquipLockReason(gearId) is { } reason)
        {
            return reason;
        }

        var slot = Db.Gear[gearId].Slot;
        Save.Backpack.RemoveAt(backpackIndex);
        if (Save.Equipment.TryGetValue(slot, out var old))
        {
            Save.Backpack.Add(old);
        }

        Save.Equipment[slot] = gearId;
        RefreshStats(HpPolicy.AddDeltaMin1);
        EquipmentChanged?.Invoke(slot);
        InventoryChanged?.Invoke();
        return null;
    }

    /// <summary>Equipa pelo id (primeira cópia na mochila). Devolve o motivo do bloqueio, "não possui" ou null.</summary>
    public string? EquipById(string gearId)
    {
        var index = Save.Backpack.IndexOf(gearId);
        return index < 0 ? $"Você não tem {gearId} na mochila." : Equip(index);
    }

    /// <summary>Desequipa o slot para a mochila; HP só é truncado (<c>ui.js:92</c>).</summary>
    public bool Unequip(string slotId)
    {
        if (!Save.Equipment.Remove(slotId, out var gearId))
        {
            return false;
        }

        Save.Backpack.Add(gearId);
        RefreshStats(HpPolicy.Truncate);
        EquipmentChanged?.Invoke(slotId);
        InventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Desmonta um item da mochila: +2 Estilhaços (<c>ui.js:100</c>).</summary>
    public bool Dismantle(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= Save.Backpack.Count)
        {
            return false;
        }

        Save.Backpack.RemoveAt(backpackIndex);
        AddMaterial(Db.Rules.Economy.ShardMaterial, Db.Rules.Economy.DismantleShards);
        return true;
    }

    // --- Armas ---

    /// <summary>Melhora a arma no Brás (<c>smithUpgrade</c>): paga e sobe um nível.</summary>
    public PurchaseResult TryUpgradeWeapon(string weaponId)
    {
        var level = Save.WeaponLevel(weaponId);
        if (level == 0)
        {
            return PurchaseResult.NotAvailable;
        }

        if (Upgrades.WeaponUpgrade(weaponId, level) is not { } cost)
        {
            return PurchaseResult.MaxLevel;
        }

        if (!TryPay(cost))
        {
            return PurchaseResult.CannotAfford;
        }

        Save.Weapons[weaponId] = level + 1;
        RefreshStats(HpPolicy.Keep);
        WeaponLevelChanged?.Invoke(weaponId, level + 1);
        return PurchaseResult.Ok;
    }

    /// <summary>
    /// Forja uma arma nova (<c>WEAP.unlock</c>). Efetivo na v2: o Brás não forja arma de outra classe
    /// (<c>extra.js:94</c>), então outra classe devolve <see cref="PurchaseResult.Locked"/>.
    /// </summary>
    public PurchaseResult TryUnlockWeapon(string weaponId)
    {
        if (Save.WeaponLevel(weaponId) > 0)
        {
            return PurchaseResult.AlreadyOwned;
        }

        if (!EquipmentRules.CanWield(weaponId, Save.ClassId))
        {
            return PurchaseResult.Locked;
        }

        if (!TryPay(Upgrades.WeaponUnlock(weaponId)))
        {
            return PurchaseResult.CannotAfford;
        }

        Save.Weapons[weaponId] = 1;
        WeaponLevelChanged?.Invoke(weaponId, 1);
        return PurchaseResult.Ok;
    }

    // --- Forja, relíquias, serviços e venda ---

    /// <summary>Forja um equipamento de receita (vai para a mochila; pode repetir).</summary>
    public PurchaseResult TryForge(RecipeDef recipe)
    {
        if (!TryPay(recipe.Cost))
        {
            return PurchaseResult.CannotAfford;
        }

        AddGear(recipe.Gear);
        return PurchaseResult.Ok;
    }

    /// <summary>Compra na Oficina do Abel; item já possuído (mochila ou equipado) não vende de novo.</summary>
    public PurchaseResult TryBuyRelic(RelicOfferDef offer)
    {
        if (Save.OwnsGear(offer.Gear))
        {
            return PurchaseResult.AlreadyOwned;
        }

        if (!TryPay(offer.Cost))
        {
            return PurchaseResult.CannotAfford;
        }

        AddGear(offer.Gear);
        return PurchaseResult.Ok;
    }

    /// <summary>
    /// Serviço pago (poções, Diário da Vesna, descanso, resgate da Lanterna). Descanso começa um dia novo e cura tudo.
    /// O resgate só cobra: quem chama agenda a chegada (<see cref="LanternRules.OdeteRescueDelay"/>).
    /// </summary>
    public PurchaseResult TryBuyService(ServiceDef service)
    {
        if (service.Gear is { } gear && Save.OwnsGear(gear))
        {
            return PurchaseResult.AlreadyOwned;
        }

        if (service.LanternRescue && !HasLostLantern)
        {
            return PurchaseResult.NotAvailable;
        }

        if (!TryPay(service.Cost))
        {
            return PurchaseResult.CannotAfford;
        }

        if (service.Potions > 0)
        {
            AddPotions(service.Potions);
        }

        if (service.Gear is not null)
        {
            AddGear(service.Gear);
        }

        if (service.Rest)
        {
            NewDay();
            RestoreFullHealth();
        }

        return PurchaseResult.Ok;
    }

    public SaleQuote ScrapQuote() => Sale.QuoteScraps(Save.Materials);

    /// <summary>Vende as sobras de Brecha ao Brás. Devolve os Lumes recebidos.</summary>
    public int SellScraps()
    {
        var quote = ScrapQuote();
        if (quote.IsEmpty)
        {
            return 0;
        }

        foreach (var id in quote.MaterialIds)
        {
            SetMaterial(id, 0);
        }

        AddLumes(quote.Lumes);
        return quote.Lumes;
    }

    /// <summary>Vende todos os Estilhaços a 4 Lumes. Devolve os Lumes recebidos.</summary>
    public int SellShards()
    {
        var lumes = Sale.ShardSaleLumes(Shards);
        if (lumes == 0)
        {
            return 0;
        }

        SetMaterial(Db.Rules.Economy.ShardMaterial, 0);
        AddLumes(lumes);
        return lumes;
    }

    // --- Poções e HP ---

    public void AddPotions(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0)
        {
            return;
        }

        Save.Potions += amount;
        PotionsChanged?.Invoke(Save.Potions);
        InventoryChanged?.Invoke();
    }

    /// <summary>
    /// Bebe uma poção (<c>usePotion</c>, <c>combat.js:220-225</c>): cura <c>round(max * 0.45)</c>, recarga 1 s.
    /// <paramref name="nowSeconds"/> é o relógio de jogo de quem chama.
    /// </summary>
    public PotionResult TryUsePotion(double nowSeconds, out int healed)
    {
        healed = 0;
        if (nowSeconds < _potionReadyAt)
        {
            return PotionResult.OnCooldown;
        }

        if (Save.Potions <= 0)
        {
            return PotionResult.NoPotions;
        }

        if (Hp >= MaxHp)
        {
            return PotionResult.FullHealth;
        }

        Save.Potions--;
        _potionReadyAt = nowSeconds + Db.Rules.Potion.Cooldown;
        healed = JsMath.Round(MaxHp * Db.Rules.Potion.Heal);
        PotionsChanged?.Invoke(Save.Potions);
        InventoryChanged?.Invoke();
        Heal(healed);
        return PotionResult.Used;
    }

    /// <summary>Aplica dano final (já calculado). Devolve true se o jogador caiu.</summary>
    public bool Damage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (IsDead)
        {
            return false;
        }

        Hp = DamageCalculator.ApplyDamage(Hp, amount);
        HealthChanged?.Invoke(Hp, MaxHp);
        return IsDead;
    }

    public void Heal(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        SetHp(Math.Min(MaxHp, Hp + amount));
    }

    public void RestoreFullHealth() => SetHp(MaxHp);

    // --- Ecos e infusões ---

    /// <summary>Absorve um Eco (<c>collect</c>, <c>combat.js:500</c>): equipa se não houver outro. True se era novo.</summary>
    public bool AbsorbEco(string ecoId)
    {
        if (!Db.Ecos.ContainsKey(ecoId))
        {
            throw new ArgumentException($"Eco desconhecido: '{ecoId}'.", nameof(ecoId));
        }

        var isNew = !Save.Ecos.Contains(ecoId);
        if (isNew)
        {
            Save.Ecos.Add(ecoId);
        }

        Save.EquippedEco ??= ecoId;
        EcoChanged?.Invoke(ecoId);
        return isNew;
    }

    public bool EquipEco(string ecoId)
    {
        if (!Save.Ecos.Contains(ecoId))
        {
            return false;
        }

        Save.EquippedEco = ecoId;
        EcoChanged?.Invoke(ecoId);
        return true;
    }

    /// <summary>Essência para usar o Eco (custo base − 10 por afinação).</summary>
    public int EcoCost(string ecoId) => Upgrades.EcoCost(ecoId, Save.EcoLevels.GetValueOrDefault(ecoId));

    /// <summary>Afina o Eco na Vesna (máx. 2 vezes).</summary>
    public PurchaseResult TryTuneEco(string ecoId)
    {
        if (!Save.Ecos.Contains(ecoId))
        {
            return PurchaseResult.NotAvailable;
        }

        var level = Save.EcoLevels.GetValueOrDefault(ecoId);
        if (Upgrades.EcoTuning(level) is not { } cost)
        {
            return PurchaseResult.MaxLevel;
        }

        if (!TryPay(cost))
        {
            return PurchaseResult.CannotAfford;
        }

        Save.EcoLevels[ecoId] = level + 1;
        EcoChanged?.Invoke(ecoId);
        return PurchaseResult.Ok;
    }

    /// <summary>Compra uma infusão no Gaspar e já a ativa (uma ativa por arma).</summary>
    public PurchaseResult TryBuyInfusion(string weaponId, string elementId)
    {
        if (Save.WeaponLevel(weaponId) == 0 || !Db.Weapons[weaponId].Elements.Contains(elementId))
        {
            return PurchaseResult.NotAvailable;
        }

        var owned = Save.Infusions.TryGetValue(weaponId, out var list) ? list : null;
        if (owned?.Contains(elementId) == true)
        {
            return PurchaseResult.AlreadyOwned;
        }

        if (!TryPay(Upgrades.Infusion(elementId)))
        {
            return PurchaseResult.CannotAfford;
        }

        if (owned is null)
        {
            owned = [];
            Save.Infusions[weaponId] = owned;
        }

        owned.Add(elementId);
        SetActiveInfusion(weaponId, elementId);
        return PurchaseResult.Ok;
    }

    /// <summary>Ativa uma infusão já comprada, ou remove com null.</summary>
    public bool SetActiveInfusion(string weaponId, string? elementId)
    {
        if (elementId is null)
        {
            Save.ActiveInfusion.Remove(weaponId);
        }
        else if (Save.Infusions.TryGetValue(weaponId, out var owned) && owned.Contains(elementId))
        {
            Save.ActiveInfusion[weaponId] = elementId;
        }
        else
        {
            return false;
        }

        RefreshStats(HpPolicy.Keep);
        InfusionChanged?.Invoke(weaponId);
        return true;
    }

    // --- Lanterna e morte ---

    /// <summary>
    /// Caiu dentro de uma Brecha: a bolsa fica na Lanterna, HP máximo ×0.6 e o jogador volta com HP cheio (reduzido).
    /// Quem chama sorteia o resgate (<see cref="LanternRules.RollRescueDelay"/>).
    /// </summary>
    public void DieInBrecha(LostLantern lost)
    {
        ArgumentNullException.ThrowIfNull(lost);
        Save.LostLantern = lost;
        RefreshStats(HpPolicy.Full);
        LanternChanged?.Invoke(true);
    }

    /// <summary>Caiu na cidade: acorda na estalagem com 50% do HP e −10 Lumes.</summary>
    public void DieInCity()
    {
        var lumes = LanternRules.LumesAfterCityDeath(Save.Lumes);
        if (lumes != Save.Lumes)
        {
            Save.Lumes = lumes;
            LumesChanged?.Invoke(lumes);
        }

        SetHp(LanternRules.CityRespawnHp(MaxHp));
    }

    /// <summary>A Lanterna voltou (pega na Brecha, trazida por um caçador ou resgate pago): devolve a bolsa e o HP máximo.</summary>
    public bool RecoverLantern()
    {
        if (Save.LostLantern is not { } lost)
        {
            return false;
        }

        Save.LostLantern = null;
        MergeBag(lost.Bag);
        RefreshStats(HpPolicy.AddDelta);
        LanternChanged?.Invoke(false);
        return true;
    }

    /// <summary>Entrega à Odete as lanternas de outros caçadores: +35 Lumes e +20 XP cada.</summary>
    public (int Lumes, int Xp) DeliverLanterns()
    {
        var count = Save.CarriedLanterns.Count;
        if (count == 0)
        {
            return (0, 0);
        }

        var reward = LanternRules.DeliveryReward(count);
        Save.CarriedLanterns.Clear();
        InventoryChanged?.Invoke();
        AddLumes(reward.Lumes);
        GainXp(reward.Xp);
        return reward;
    }

    // --- Tempo ---

    /// <summary>Avança o relógio (1 min de jogo por segundo). Devolve true se começou um novo dia.</summary>
    public bool AdvanceClock(double seconds)
    {
        Save.ClockMinutes = Calendar.Advance(Save.ClockMinutes, seconds);
        if (!Calendar.IsNewDayDue(Save.ClockMinutes))
        {
            return false;
        }

        NewDay();
        return true;
    }

    /// <summary><c>newDay()</c>: próximo dia às 16:00.</summary>
    public void NewDay()
    {
        Save.Day++;
        Save.ClockMinutes = Calendar.NewDayClock;
        DayChanged?.Invoke(Save.Day);
    }

    // --- Missões e Brechas ---

    /// <summary>Soma um contador de missão (<see cref="QuestCounters"/>).</summary>
    public void CountQuest(string key, int amount = 1) => Save.Quests.Counters[key] = Save.Quests.Counter(key) + amount;

    /// <summary>Chefe derrotado numa Brecha deste tema: conta para "bosses", marca o tema e soma Brechas fechadas.</summary>
    public void RecordBossDefeated(string themeId)
    {
        CountQuest(QuestCounters.Bosses);
        if (!Save.Quests.ClosedThemes.Contains(themeId))
        {
            Save.Quests.ClosedThemes.Add(themeId);
        }

        Save.BrechasClosed++;
    }

    /// <summary>Invasão resolvida: +30 Lumes, +30 XP e uma Brecha fechada (<c>resolveInvasion</c>).</summary>
    public void RecordInvasionResolved()
    {
        var (lumes, xp) = Brechas.InvasionReward;
        Save.BrechasClosed++;
        AddLumes(lumes);
        GainXp(xp);
    }

    /// <summary>Missão atual, ou null se todas foram cumpridas.</summary>
    public QuestDef? CurrentQuest => QuestRules.Current(Save);

    /// <summary>Se a missão atual foi cumprida, dá a recompensa, avança e devolve a missão; senão null.</summary>
    public QuestDef? TryCompleteQuest()
    {
        if (CurrentQuest is not { } quest || !QuestRules.IsComplete(quest, Save))
        {
            return null;
        }

        var lumes = Save.Lumes;
        var potions = Save.Potions;
        QuestRules.Complete(quest, Save);
        if (Save.Lumes != lumes)
        {
            LumesChanged?.Invoke(Save.Lumes);
        }

        if (Save.Potions != potions)
        {
            PotionsChanged?.Invoke(Save.Potions);
        }

        foreach (var id in quest.Reward.Mats.Keys)
        {
            MaterialChanged?.Invoke(id, Save.MaterialCount(id));
        }

        InventoryChanged?.Invoke();
        QuestCompleted?.Invoke(quest);
        return quest;
    }

    // --- Internos ---

    private enum HpPolicy
    {
        /// <summary>HP atual não muda (só limita ao máximo).</summary>
        Keep,

        /// <summary>HP atual recebe o delta do máximo, limitado ao máximo (rank-up, Lanterna de volta).</summary>
        AddDelta,

        /// <summary>Delta entre 1 e o máximo (equipar).</summary>
        AddDeltaMin1,

        /// <summary>Só trunca (desequipar).</summary>
        Truncate,

        /// <summary>HP cheio.</summary>
        Full,
    }

    private void RefreshStats(HpPolicy policy)
    {
        var oldMax = MaxHp;
        Stats = StatsCalculator.Compute(Save);
        var delta = MaxHp - oldMax;
        var hp = policy switch
        {
            HpPolicy.AddDelta => Math.Min(MaxHp, Hp + delta),
            HpPolicy.AddDeltaMin1 => Math.Clamp(Hp + delta, 1, MaxHp),
            HpPolicy.Full => MaxHp,
            _ => Math.Min(MaxHp, Hp),
        };

        StatsChanged?.Invoke(Stats);
        if (hp != Hp || delta != 0)
        {
            Hp = Math.Max(0, hp);
            HealthChanged?.Invoke(Hp, MaxHp);
        }
    }

    private void SetHp(int hp)
    {
        if (hp == Hp)
        {
            return;
        }

        Hp = Math.Clamp(hp, 0, MaxHp);
        HealthChanged?.Invoke(Hp, MaxHp);
    }

    private void SetMaterial(string materialId, int amount)
    {
        if (amount > 0)
        {
            Save.Materials[materialId] = amount;
        }
        else
        {
            Save.Materials.Remove(materialId);
        }

        MaterialChanged?.Invoke(materialId, Math.Max(0, amount));
        InventoryChanged?.Invoke();
    }
}
