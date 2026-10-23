using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Ranks;
using Vespera.Game.Boot;
using Vespera.Game.Hunter;

namespace Vespera.Game.UI;

/// <summary>
/// Inventário e equipamento: à esquerda a arma, as quatro peças de armadura e os dois Ecos; no centro a mochila e os materiais;
/// à direita os detalhes (atributos, conjunto, motivo de trava) do que estiver focado. Confirmar veste ou tira a peça.
/// </summary>
public partial class InventoryScreen : HunterScreen
{
    private const int EquipmentWidth = 560;
    private const int DetailWidth = 520;
    private const int RowIconSize = 80;
    private const string WeaponKey = "weapon";

    private readonly List<SlotRow> _rows = [];
    private DetailPane _detail = null!;
    private VBoxContainer _equipment = null!;
    private VBoxContainer _bag = null!;
    private Label _wallet = null!;
    private Label _fragmentos = null!;
    private Button _characterButton = null!;
    private string _focusKey = WeaponKey;

    private static ProgressionEngine Engine => Progression.Engine;

    public static void Open() => Open(new InventoryScreen());

    /// <summary>Linha (peça, material, slot) por chave, para testes e para devolver o foco depois de refazer a lista.</summary>
    public SlotRow? RowFor(string key) => _rows.FirstOrDefault(r => (string?)r.GetMeta("key") == key);

    public IReadOnlyList<SlotRow> Rows => _rows;

    public string DetailText => _detail.Plain;

    protected override Control? DefaultFocus() => (Control?)RowFor(_focusKey) ?? (Control?)_rows.FirstOrDefault() ?? _characterButton;

    protected override void Build()
    {
        var panel = BuildPage("Inventário", out var column, sideMargin: 40);
        var content = UiKit.MakeRow(26);
        panel.AddChild(content);

        _equipment = UiKit.MakeColumn(6);
        _equipment.CustomMinimumSize = new Vector2(EquipmentWidth, 0);
        content.AddChild(WrapScroll(_equipment, EquipmentWidth));

        var center = UiKit.MakeColumn(12);
        center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var walletMargin = new MarginContainer();
        walletMargin.AddThemeConstantOverride("margin_left", 44);
        _wallet = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation);
        var walletRow = UiKit.MakeRow(10);
        walletRow.AddChild(CurrencyIcon(ItemIcons.Fragmentos));
        _fragmentos = UiKit.MakeLabel(string.Empty, UiTheme.HeadingVariation);
        walletRow.AddChild(_fragmentos);
        walletRow.AddChild(new Control { CustomMinimumSize = new Vector2(30, 0) });
        walletRow.AddChild(CurrencyIcon(ItemIcons.Lumes));
        walletRow.AddChild(_wallet);
        walletMargin.AddChild(walletRow);
        center.AddChild(walletMargin);
        var scroll = UiKit.MakeScroll();
        scroll.FollowFocus = false;
        _bag = UiKit.MakeColumn(10);
        _bag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(UiKit.PadForGlow(_bag));
        center.AddChild(scroll);
        content.AddChild(center);

        _detail = new DetailPane { CustomMinimumSize = new Vector2(DetailWidth, 0) };
        content.AddChild(_detail);

        var footer = MakeFooter();
        _characterButton = UiKit.MakeButton("Personagem", () => CharacterScreen.Open(), 380);
        footer.AddChild(_characterButton);
        footer.AddChild(UiKit.MakeButton("Voltar", Close, 300));
        column.AddChild(footer);

        Rebuild();
    }

    protected override void OnProgressionChanged()
    {
        if (IsInsideTree())
        {
            CallDeferred(nameof(Rebuild));
        }
    }

    private static TextureRect CurrencyIcon(Texture2D? texture) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(48, 48),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static ScrollContainer WrapScroll(Control child, int width)
    {
        var scroll = UiKit.MakeScroll();
        scroll.FollowFocus = false;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        scroll.CustomMinimumSize = new Vector2(width, 0);
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        scroll.AddChild(UiKit.PadForGlow(child));
        return scroll;
    }

    private void Rebuild()
    {
        var focused = GetViewport().GuiGetFocusOwner() as SlotRow;
        if (focused?.HasMeta("key") == true)
        {
            _focusKey = (string)focused.GetMeta("key");
        }

        foreach (var child in _equipment.GetChildren().Concat(_bag.GetChildren()))
        {
            child.QueueFree();
        }

        _rows.Clear();
        var engine = Engine;
        _fragmentos.Text = $"Fragmentos {engine.Progress.Fragmentos}";
        _wallet.Text = $"Lumes {engine.Progress.Lumes}";
        BuildEquipment(engine);
        BuildBag(engine);
        CallDeferred(nameof(RestoreFocusAfterRebuild));
    }

    private void RestoreFocusAfterRebuild()
    {
        var target = RowFor(_focusKey) ?? _rows.FirstOrDefault();
        target?.GrabFocus();
        if (target is not null)
        {
            ShowDetail(target);
        }
    }

    private void BuildEquipment(ProgressionEngine engine)
    {
        var weapon = engine.WeaponStats;
        AddRow(_equipment, WeaponKey, new SlotRow("Arma", weapon.DisplayName, RankColors.Of(weapon.TierRank), $"Nv. {weapon.Level}", Icon(ItemIcons.Gear(engine.ClassId, weapon.TierRank), weapon.TierRank)) { Tag = weapon });

        foreach (var slot in engine.Catalog.GearData.Slots)
        {
            var item = engine.EquippedIn(slot.Id);
            var row = item is null
                ? new SlotRow(slot.Name, "Vazio", Palette.CreamDim, lead: Ghost(slot.Id), dimmed: true) { Tag = slot }
                : new SlotRow(slot.Name, item.Name, RankColors.Of(item.Rank), string.Empty, Icon(ItemIcons.Item(item), item.Rank), () => Unequip(slot.Id)) { Tag = item };
            AddRow(_equipment, $"slot:{slot.Id}", row);
        }

        for (var i = 0; i < engine.EchoSlotCount; i++)
        {
            var slotIndex = i;
            var echoId = engine.EchoIn(i);
            var echo = echoId is null ? null : engine.Catalog.Echoes[echoId];
            var row = echo is null
                ? new SlotRow($"Eco {i + 1}", "Vazio", Palette.CreamDim, lead: Ghost("eco"), activate: () => CycleEcho(slotIndex), dimmed: true) { Tag = $"echo-empty:{i}" }
                : new SlotRow($"Eco {i + 1}", echo.Name, RankColors.Of(echo.Rank), string.Empty, Icon(ItemIcons.Echo(echo), echo.Rank), () => CycleEcho(slotIndex)) { Tag = echo };
            AddRow(_equipment, $"echo:{i}", row);
        }
    }

    private void BuildBag(ProgressionEngine engine)
    {
        _bag.AddChild(UiKit.MakeLabel("Peças na mochila", UiTheme.HeadingVariation));
        var items = engine.BackpackItems.OrderByDescending(i => engine.Catalog.RankIndex(i.Rank)).ThenBy(i => i.Slot).ThenBy(i => i.Id).ToList();
        if (items.Count == 0)
        {
            _bag.AddChild(UiKit.MakeWrapped("Nenhuma peça ainda. Guardiões e peças raras caem nas Brechas.", UiTheme.DimVariation));
        }

        foreach (var item in items)
        {
            var locked = engine.LockReason(item) is not null;
            var slotName = engine.Catalog.GearData.Slots.First(s => s.Id == item.Slot).Name;
            var summary = item.Stats.Lines().FirstOrDefault();
            var caption = summary is null ? slotName : $"{slotName} · {summary}";
            var row = new SlotRow(caption, item.Name, RankColors.Of(item.Rank), string.Empty, Icon(ItemIcons.Item(item), item.Rank), () => Equip(item), locked) { Tag = item };
            AddRow(_bag, $"item:{item.Id}", row);
        }

        _bag.AddChild(UiKit.MakeLabel("Materiais", UiTheme.HeadingVariation));
        var materials = engine.Progress.Materials
            .Select(kv => (Def: engine.Catalog.Materials[kv.Key], Count: kv.Value))
            .OrderByDescending(m => engine.Catalog.RankIndex(m.Def.Rank))
            .ThenBy(m => m.Def.Name)
            .ToList();
        if (materials.Count == 0)
        {
            _bag.AddChild(UiKit.MakeWrapped("Sem materiais. Quebre partes dos monstros para coletar.", UiTheme.DimVariation));
        }

        foreach (var (def, count) in materials)
        {
            var kind = def.Kind == MaterialKind.Guardian ? "Guardião" : "Comum";
            var row = new SlotRow($"Rank {def.Rank} · {kind}", def.Name, RankColors.Of(def.Rank), $"x{count}", Icon(ItemIcons.Material(def), def.Rank)) { Tag = def };
            AddRow(_bag, $"mat:{def.Id}", row);
        }
    }

    private static ItemIconSlot Icon(Texture2D? texture, Rank rank) => new(RowIconSize, texture, rank);

    private static ItemIconSlot Ghost(string slot) => new(RowIconSize, ItemIcons.Gear(slot, Rank.E), Rank.E, ghost: true);

    private void AddRow(Container parent, string key, SlotRow row)
    {
        row.SetMeta("key", key);
        row.Highlighted += ShowDetail;
        row.Highlighted += r => Callable.From(() => UiKit.KeepVisible(r)).CallDeferred();
        parent.AddChild(row);
        _rows.Add(row);
    }

    private void ShowDetail(SlotRow row)
    {
        var engine = Engine;
        _focusKey = (string?)row.GetMeta("key") ?? _focusKey;
        if (row.Lead is ItemIconSlot { IsGhost: false } lead)
        {
            _detail.ShowIcon(lead.Icon, lead.ItemRank);
        }
        else
        {
            _detail.ShowIcon(null, null);
        }

        _detail.ShowText(row.Tag switch
        {
            ItemInstance item => HunterText.Item(item, engine) + (engine.IsEquipped(item) ? "\n\n" + HunterText.Dimmed("Equipado. Confirmar para tirar.") : "\n\n" + HunterText.Dimmed("Confirmar para vestir.")),
            WeaponStats weapon => HunterText.Weapon(weapon, engine.Catalog.Weapons[weapon.ClassId].Name) + "\n\n" + HunterText.Dimmed("Evolui na forja da Odete, com Fragmentos e materiais."),
            HunterMaterialDef material => HunterText.Material(material, engine.Progress.MaterialCount(material.Id)),
            EchoDef echo => HunterText.Echo(echo, engine.Catalog) + "\n\n" + HunterText.Dimmed("Confirmar para trocar de Eco."),
            GearSlotDef slot => HunterText.EmptySlot(slot.Name, "Nenhuma peça vestida. Escolha uma na mochila."),
            string key when key.StartsWith("echo-empty", System.StringComparison.Ordinal) => HunterText.EmptySlot("Eco", "Slot livre. Absorva Ecos derrotando Guardiões; confirmar escolhe um."),
            _ => string.Empty,
        });
    }

    private void Equip(ItemInstance item)
    {
        var result = Progression.Equipment.Equip(item.Id);
        if (result.Ok)
        {
            HunterSfx.Play(HunterSound.Equip);
        }
        else
        {
            UiSfx.Play(UiSound.Deny);
        }
    }

    private void Unequip(string slot)
    {
        if (Progression.Equipment.Unequip(slot))
        {
            HunterSfx.Play(HunterSound.Equip);
        }
    }

    /// <summary>Confirmar num slot de Eco passa pelo vazio e por cada Eco absorvido que não está no outro slot.</summary>
    private void CycleEcho(int slot)
    {
        var engine = Engine;
        var options = new List<string?> { null };
        options.AddRange(engine.Progress.Echoes.Where(id => engine.EchoIn(1 - slot) != id));
        var current = engine.EchoIn(slot);
        var next = options[(options.IndexOf(current) + 1) % options.Count];
        var changed = next is null ? Progression.Equipment.UnequipEcho(slot) : Progression.Equipment.EquipEcho(slot, next).Ok;
        if (changed)
        {
            HunterSfx.Play(HunterSound.Equip);
        }
        else
        {
            UiSfx.Play(UiSound.Deny);
        }
    }
}
