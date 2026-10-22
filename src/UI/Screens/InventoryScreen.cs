using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Ranks;
using Vespera.Core.State;
using Vespera.UI.Common;
using Vespera.UI.HudKit;
using Vespera.UI.Widgets;

namespace Vespera.UI.Screens;

/// <summary>
/// Inventário da v2 (<c>uiInv</c>, <c>ui.js:105-233</c> + travas de <c>extra.js:75-89</c>): abas PERSONAGEM (palco com o
/// personagem e os 8 slots, atributos, grade da mochila com filtros e o detalhe/tooltip do item com ações), ECOS (4
/// cartões, equipar) e SELOS (os 10 Selos, progresso e visuais). Tab/LB/RB trocam de aba, setas/WASD/d-pad navegam,
/// Enter/E/A = 1ª ação (equipar, desequipar, usar), X/Y desmonta, R/X usa a poção, Esc/I/B fecha.
/// </summary>
public partial class InventoryScreen : Screen
{
    private const int Columns = 5;
    private const float Cell = 26;

    private static readonly (string Id, string Label, string? Icon)[] Filters =
    [
        ("all", "Todos", null),
        ("arm", "Armas", "i_sword"),
        ("eq", "Equipamento", "eq_armor0"),
        ("mat", "Materiais", "i_ore"),
        ("con", "Consumíveis", "ic_potion"),
    ];

    /// <summary>Posição de cada slot no palco (<c>slotPos</c>, <c>ui.js:141</c>): coluna 0/1/2, linha −1..3.</summary>
    private static readonly Dictionary<string, (int Col, int Row)> SlotPositions = new()
    {
        ["arma"] = (0, 0), ["lanterna"] = (0, 1), ["capa"] = (0, 2),
        ["peito"] = (2, 0), ["maos"] = (2, 1), ["reliquia"] = (2, 2),
        ["cabeca"] = (1, -1), ["botas"] = (1, 3),
    };

    private readonly List<SelectableRow> _tabs = [];
    private readonly List<Control> _pages = [];
    private readonly List<(SelectableRow Row, HotbarSlot Art, ItemRef Item)> _cells = [];
    private readonly Dictionary<string, (SelectableRow Row, HotbarSlot Art)> _slots = [];
    private readonly List<SelectableRow> _filterRows = [];
    private readonly List<(SelectableRow Card, int Index)> _ecoCards = [];
    private readonly VBoxContainer _root;
    private GridContainer _grid = null!;
    private ItemTooltip _detail = null!;
    private VBoxContainer _actions = null!;
    private Label _empty = null!;
    private IconRect _hero = null!;
    private VBoxContainer _statsLeft = null!;
    private VBoxContainer _statsRight = null!;
    private HBoxContainer _ecoRow = null!;
    private Control _sealsPage = null!;
    private Label _lumes = null!;
    private Label _shards = null!;
    private string _filter = "all";
    private int _tab;
    private ItemRef? _selected;
    private string? _selectedSlot;
    private double _time;
    private Label _status = null!;
    private float _statusT;

    public InventoryScreen()
    {
        AddChild(new ColorRect { Color = UiPalette.Overlay(0.82f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var root = _root = Ui.Column(4);
        root.CustomMinimumSize = new Vector2(606, 340);
        center.AddChild(root);

        root.AddChild(BuildHeader());
        var pages = new Control { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(606, 290) };
        root.AddChild(pages);
        AddPage(pages, BuildCharacterPage());
        AddPage(pages, BuildEcosPage());
        AddPage(pages, _sealsPage = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore });
        root.AddChild(BuildFooter());
    }

    /// <summary>Tamanho que o inventário precisa (u): a escala da interface para antes de ele sair da janela (POLI-2).</summary>
    public override Vector2 DesignSize => _root.GetCombinedMinimumSize() + new Vector2(16, 16);

    public int Tab => _tab;

    /// <summary>Item em foco no detalhe (smoke test).</summary>
    public ItemRef? SelectedItem => _selected;

    public ItemTooltip Detail => _detail;

    public IReadOnlyList<(SelectableRow Row, HotbarSlot Art, ItemRef Item)> Cells => _cells;

    public SelectableRow? SlotRow(string slotId) => _slots.TryGetValue(slotId, out var s) ? s.Row : null;

    public override bool HidesHud => true;

    protected override Control? DefaultFocus => _tab switch
    {
        0 => _cells.Count > 0 ? _cells[0].Row : _filterRows[0],
        1 => _ecoCards.Count > 0 ? _ecoCards[0].Card : _tabs[1],
        _ => _tabs[2],
    };

    /// <summary>Troca de aba (0 Personagem, 1 Ecos, 2 Selos).</summary>
    public void SelectTab(int tab)
    {
        _tab = ((tab % _pages.Count) + _pages.Count) % _pages.Count;
        for (var i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == _tab;
            _tabs[i].NormalStyle = i == _tab ? "TabFocus" : "TabNormal";
        }

        Rebuild();
        FocusDefault();
    }

    /// <summary>1ª ação do item em foco (Enter/E/A).</summary>
    public void PrimaryAction()
    {
        if (_selectedSlot is { } slot)
        {
            Unequip(slot);
            return;
        }

        if (_selected is not { } item)
        {
            return;
        }

        switch (item.Kind)
        {
            case ItemKind.Gear:
                Equip(item);
                break;
            case ItemKind.Potion:
                UsePotion();
                break;
        }
    }

    /// <summary>Desmonta o equipamento em foco (+2 Estilhaços, <c>ui.js:100</c>).</summary>
    public void DismantleSelected()
    {
        if (_selected is not { Kind: ItemKind.Gear } item || _selectedSlot is not null)
        {
            return;
        }

        var state = GameManager.Instance.State;
        if (state.Dismantle(item.Index))
        {
            Common.UiSound.Play("brk");
            GameManager.Instance.SaveGame();
            Rebuild();
            FocusCell(item.Index);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (Stack?.Top != this || !InputReady || @event.IsEcho())
        {
            return;
        }

        // Tab é foco do Godot por padrão: aqui troca de aba (ui.js:131). LB/RB no gamepad.
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Tab })
        {
            GetViewport().SetInputAsHandled();
            UiSound();
            SelectTab(_tab + 1);
        }
        else if (@event is InputEventJoypadButton { Pressed: true } pad && pad.ButtonIndex is JoyButton.LeftShoulder or JoyButton.RightShoulder)
        {
            GetViewport().SetInputAsHandled();
            UiSound();
            SelectTab(_tab + (pad.ButtonIndex == JoyButton.RightShoulder ? 1 : -1));
        }
    }

    public override bool HandleInput(InputEvent @event)
    {
        if (!InputReady || @event.IsEcho())
        {
            return base.HandleInput(@event);
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.I } || @event.IsActionPressed(InputActions.Inventory))
        {
            UiSound();
            Close();
            return true;
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.X } || @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y })
        {
            DismantleSelected();
            return true;
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.R } || @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.X })
        {
            if (_selected is { Kind: ItemKind.Potion })
            {
                UsePotion();
            }

            return true;
        }

        return base.HandleInput(@event);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _time += delta;
        if (_statusT > 0f)
        {
            _statusT = Mathf.Max(0f, _statusT - (float)delta);
            _status.Modulate = new Color(1, 1, 1, Mathf.Min(1f, _statusT * 2f));
        }

        if (_tab == 0)
        {
            // personagem flutuando (ui.js:139)
            _hero.Position = new Vector2(_hero.Position.X, 34 + (Mathf.Sin((float)_time * 2f) * 1.5f));
        }
    }

    protected override void OnOpened()
    {
        _statusT = 0f;
        _status.Text = string.Empty;
        _filter = "all";
        SelectTab(0);
    }

    // --- montagem ---

    private Control BuildHeader()
    {
        var logo = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/sprites/ui/logo.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            CustomMinimumSize = new Vector2(72, 24),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var row = Ui.Row(6, logo);
        string[] names = ["Personagem", "Ecos", "Selos"];
        string[] icons = ["eq_armor1", "i_eco", "r_C"];
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var tab = new SelectableRow
            {
                NormalStyle = "TabNormal",
                FocusStyle = "TabFocus",
                Marker = false,
                CustomMinimumSize = new Vector2(96, 20),
                OnActivate = () => SelectTab(index),
            };
            tab.AddChild(Ui.Row(4, IconRect.Create(icons[i], 11), Ui.Title(names[i].ToUpperInvariant(), UiFonts.Small)));
            row.AddChild(tab);
            _tabs.Add(tab);
        }

        return row;
    }

    private Control BuildFooter()
    {
        _status = Ui.Text(string.Empty, UiFonts.Small, UiPalette.Locked, align: HorizontalAlignment.Center);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.ClipText = true;
        _status.CustomMinimumSize = new Vector2(40, 0);
        _lumes = Ui.Bold("0", UiFonts.Body, UiPalette.Creme);
        _shards = Ui.Text("0", UiFonts.Body, UiPalette.Creme);
        return Ui.Row(8,
            Ui.Hint("Esc", "B", "Fechar"),
            Ui.Hint("Tab", "LB/RB", "Aba"),
            Ui.Hint("Setas", "D-pad", "Navegar"),
            Ui.ActionHint(InputActions.Interact, "Equipar"),
            Ui.Hint("X", "Y", "Desmontar"),
            _status,
            IconRect.Create("i_shard", 9), _shards,
            IconRect.Create("i_coin", 10), _lumes);
    }

    private void AddPage(Control parent, Control page)
    {
        page.SetAnchorsPreset(LayoutPreset.FullRect);
        page.Visible = false;
        parent.AddChild(page);
        _pages.Add(page);
    }

    private Control BuildCharacterPage()
    {
        // Esquerda: palco com os slots e atributos (44%).
        var stage = new Control { CustomMinimumSize = new Vector2(240, 162), MouseFilter = MouseFilterEnum.Ignore };
        stage.AddChild(new StageGlow { Position = new Vector2(70, 98), Size = new Vector2(100, 30) });
        _hero = IconRect.Create(string.Empty, 0);
        _hero.Size = new Vector2(70, 100);
        _hero.Position = new Vector2(85, 34);
        stage.AddChild(_hero);
        foreach (var slot in Database.Db.GearSlots)
        {
            var (col, rowIndex) = SlotPositions.GetValueOrDefault(slot.Id, (0, 0));
            var x = col switch { 0 => 10f, 1 => 107f, _ => 204f };
            var y = rowIndex switch { -1 => 2f, 3 => 128f, _ => 24f + (rowIndex * 38f) };
            var art = new HotbarSlot { DrawFrame = false, CustomMinimumSize = new Vector2(Cell - 4, Cell - 4) }; // + margem do SlotPanel = Cell
            var cell = new SelectableRow { NormalStyle = "SlotPanel", FocusStyle = "SlotFocus", Marker = false, CustomMinimumSize = new Vector2(Cell, Cell), Position = new Vector2(x, y), Size = new Vector2(Cell, Cell) };
            cell.AddChild(art);
            var slotId = slot.Id;
            cell.OnFocused = () => FocusSlot(slotId);
            cell.OnActivate = () =>
            {
                _selectedSlot = slotId;
                PrimaryAction();
            };
            stage.AddChild(cell);
            var label = Ui.Title(slot.Name.ToUpperInvariant(), UiFonts.Tiny, UiPalette.SubLight, HorizontalAlignment.Center);
            label.Position = new Vector2(x - 12, y + Cell + 1);
            label.Size = new Vector2(Cell + 24, 8);
            stage.AddChild(label);
            _slots[slot.Id] = (cell, art);
        }

        _statsLeft = Ui.Column(2);
        _statsRight = Ui.Column(2);
        _statsLeft.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statsRight.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stats = Ui.Panel("InsetPanel", Ui.Row(10, _statsLeft, new ColorRect { Color = UiPalette.Gold with { A = 0.3f }, CustomMinimumSize = new Vector2(1, 0), MouseFilter = MouseFilterEnum.Ignore }, _statsRight));
        var left = Ui.Ornate(Ui.Column(4, Ui.SectionTitle("Personagem", UiFonts.Body), stage, stats));
        left.CustomMinimumSize = new Vector2(256, 0);

        // Direita: filtros, grade e detalhe.
        var filters = Ui.Row(3);
        foreach (var (id, label, icon) in Filters)
        {
            var filterId = id;
            var chip = new SelectableRow
            {
                NormalStyle = "TabNormal",
                FocusStyle = "TabFocus",
                Marker = false,
                CustomMinimumSize = new Vector2(icon is null ? 30 : 18, 14),
                TooltipText = label,
                OnActivate = () =>
                {
                    _filter = filterId;
                    Rebuild();
                },
            };
            chip.AddChild(icon is null ? Ui.Text(label, UiFonts.Tiny, align: HorizontalAlignment.Center) : IconRect.Create(icon, 9));
            filters.AddChild(chip);
            _filterRows.Add(chip);
        }

        _grid = new GridContainer { Columns = Columns, MouseFilter = MouseFilterEnum.Ignore };
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            CustomMinimumSize = new Vector2((Columns * (Cell + 4)) + 4, 206),
            MouseFilter = MouseFilterEnum.Pass,
        };
        scroll.AddChild(Ui.Margin(2, 2, 2, 2, _grid));
        _empty = Ui.Text("Nada aqui ainda.", UiFonts.Small, UiPalette.Dim);

        _detail = new ItemTooltip { CustomMinimumSize = new Vector2(150, 0) };
        _actions = Ui.Column(1);
        var detail = Ui.Column(4, _detail, _actions);
        detail.CustomMinimumSize = new Vector2(152, 0);

        var gridColumn = Ui.Column(3, filters, scroll, _empty);
        var right = Ui.Ornate(Ui.Column(4, Ui.SectionTitle("Inventário", UiFonts.Body), Ui.Row(8, gridColumn, detail)));
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var page = Ui.Row(6, left, right);
        return page;
    }

    private Control BuildEcosPage()
    {
        var intro = Ui.Text("Quando um chefe morre, às vezes deixa um Eco. Quem o absorve ganha uma habilidade única daquele monstro. Dois caçadores do mesmo rank podem lutar de jeitos completamente diferentes.", UiFonts.Small, UiPalette.SubLight, wrap: true);
        _ecoRow = Ui.Row(7);
        _ecoRow.Alignment = BoxContainer.AlignmentMode.Center;
        var panel = Ui.Ornate(Ui.Column(6, Ui.SectionTitle("Ecos", UiFonts.Body), intro, _ecoRow));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        return panel;
    }

    // --- conteúdo ---

    private void Rebuild()
    {
        var state = GameManager.Instance.State;
        _lumes.Text = Ui.Thousands(state.Lumes);
        _shards.Text = state.Shards.ToString();
        switch (_tab)
        {
            case 0:
                RebuildCharacter(state);
                break;
            case 1:
                RebuildEcos(state);
                break;
            default:
                RebuildSeals(state);
                break;
        }
    }

    private void RebuildCharacter(GameState state)
    {
        var db = Database.Db;
        var cls = state.Save.ClassId;
        _hero.Key = $"c_{cls}_front";
        foreach (var (slotId, (_, art)) in _slots)
        {
            var id = slotId == "arma" ? cls : state.Save.Equipment.GetValueOrDefault(slotId);
            art.IconKey = id is null ? string.Empty : slotId == "arma" ? UiItems.WeaponIcon(state, id) : db.Gear[id].Icon;
            art.Stripe = id is not null && slotId != "arma" ? UiItems.RarityColor(db.Gear[id].Rarity) : null;
        }

        for (var i = 0; i < _filterRows.Count; i++)
        {
            _filterRows[i].NormalStyle = Filters[i].Id == _filter ? "TabFocus" : "TabNormal";
        }

        Ui.Clear(_grid);
        _cells.Clear();
        foreach (var item in Items(state, _filter))
        {
            AddCell(state, item);
        }

        _empty.Visible = _cells.Count == 0;
        RebuildStats(state);
        if (_cells.Count == 0)
        {
            _selected = null;
            _selectedSlot = null;
            _detail.Visible = false;
            Ui.Clear(_actions);
        }
    }

    private static List<ItemRef> Items(GameState state, string filter)
    {
        var db = Database.Db;
        var list = new List<ItemRef>();
        if (filter is "all" or "arm")
        {
            foreach (var w in db.WeaponOrder)
            {
                if (state.Save.WeaponLevel(w.Id) > 0)
                {
                    list.Add(new ItemRef(ItemKind.Weapon, w.Id));
                }
            }
        }

        if (filter is "all" or "eq")
        {
            for (var i = 0; i < state.Save.Backpack.Count; i++)
            {
                list.Add(new ItemRef(ItemKind.Gear, state.Save.Backpack[i], i));
            }
        }

        if (filter is "all" or "mat")
        {
            foreach (var m in db.MaterialData.Materials)
            {
                var n = state.Save.MaterialCount(m.Id);
                if (n > 0)
                {
                    list.Add(new ItemRef(ItemKind.Material, m.Id, -1, n));
                }
            }
        }

        if (filter is "all" or "con")
        {
            if (state.Save.Potions > 0)
            {
                list.Add(new ItemRef(ItemKind.Potion, "potion", -1, state.Save.Potions));
            }

            for (var i = 0; i < state.Save.CarriedLanterns.Count; i++)
            {
                list.Add(new ItemRef(ItemKind.Lantern, state.Save.CarriedLanterns[i], i));
            }
        }

        return list;
    }

    private void AddCell(GameState state, ItemRef item)
    {
        var info = UiItems.Info(state, item);
        var art = new HotbarSlot { DrawFrame = false, IconKey = info.Icon, CustomMinimumSize = new Vector2(Cell, Cell) };
        art.Locked = info.Locked is not null;
        art.CornerMarker = item.Kind == ItemKind.Weapon && item.Id == state.Save.ClassId;
        art.Stripe = item.Kind == ItemKind.Gear ? info.RarityColor : null;
        art.SetCorner(item.Count > 1 ? item.Count.ToString() : string.Empty, UiPalette.Creme);
        var cell = new SelectableRow { NormalStyle = "SlotPanel", FocusStyle = "SlotFocus", Marker = false, CustomMinimumSize = new Vector2(Cell, Cell) };
        cell.AddChild(art);
        cell.OnFocused = () => FocusItem(item);
        cell.OnActivate = () =>
        {
            _selectedSlot = null;
            _selected = item;
            PrimaryAction();
        };
        _grid.AddChild(cell);
        _cells.Add((cell, art, item));
    }

    private void RebuildStats(GameState state)
    {
        var st = state.Stats;
        var rank = state.RankDef;
        Ui.Clear(_statsLeft);
        Ui.Clear(_statsRight);
        AddStat(_statsLeft, "ui_hpicon", "Vida", st.Hp.ToString());
        AddStat(_statsLeft, "ui_sticon", "Energia", "100");
        AddStat(_statsLeft, "i_sword", "Ataque", st.Atk.ToString());
        AddStat(_statsLeft, "eq_shield0", "Defesa", st.Def.ToString());
        AddStat(_statsRight, "rl_vazio", "Crítico", $"{st.Crit}%");
        AddStat(_statsRight, "eq_boot1", "Velocidade", UiItems.Percent(st.Speed));
        AddStat(_statsRight, rank.Icon, "Rank", rank.Title);
        AddStat(_statsRight, "i_eco", "Essência", UiItems.Percent(st.Essence));
    }

    private static void AddStat(VBoxContainer column, string icon, string label, string value) =>
        column.AddChild(Ui.Row(4, IconRect.Create(icon, 8), Ui.Text(label, UiFonts.Small), Ui.Spacer(), Ui.Bold(value, UiFonts.Small, UiPalette.Creme)));

    private void FocusItem(ItemRef item)
    {
        _selectedSlot = null;
        _selected = item;
        var state = GameManager.Instance.State;
        var info = UiItems.Info(state, item);
        _detail.ShowInfo(info);
        Ui.Clear(_actions);
        switch (item.Kind)
        {
            case ItemKind.Gear:
                if (info.Locked is null)
                {
                    AddInteractAction("Equipar", () => Equip(item));
                }

                AddAction("X", "Y", "Desmontar (+2 Estilhaços)", DismantleSelected);
                break;
            case ItemKind.Potion:
                AddAction("R", "X", "Usar", UsePotion);
                break;
        }
    }

    private void FocusSlot(string slotId)
    {
        _selectedSlot = slotId;
        _selected = null;
        var state = GameManager.Instance.State;
        Ui.Clear(_actions);
        if (slotId == "arma")
        {
            _detail.ShowInfo(UiItems.Weapon(state, state.Save.ClassId));
            return;
        }

        if (!state.Save.Equipment.TryGetValue(slotId, out var gearId))
        {
            _detail.Visible = false;
            return;
        }

        _detail.ShowInfo(UiItems.Gear(state, gearId, compareEquipped: false));
        AddInteractAction("Desequipar", () => Unequip(slotId));
    }

    private void AddAction(string key, string pad, string label, Action run)
    {
        var row = new SelectableRow { Marker = false, OnActivate = run, CustomMinimumSize = new Vector2(0, 13) };
        row.AddChild(Ui.Hint(key, pad, label, UiFonts.Small, UiPalette.Creme));
        _actions.AddChild(row);
    }

    /// <summary>Ação que a linha executa com o botão de interagir: a dica segue o remapeamento (POLI-4).</summary>
    private void AddInteractAction(string label, Action run)
    {
        var row = new SelectableRow { Marker = false, OnActivate = run, CustomMinimumSize = new Vector2(0, 13) };
        row.AddChild(Ui.ActionHint(InputActions.Interact, label, UiFonts.Small, UiPalette.Creme));
        _actions.AddChild(row);
    }

    private void Equip(ItemRef item)
    {
        var state = GameManager.Instance.State;
        if (item.Index < 0 || item.Index >= state.Save.Backpack.Count)
        {
            return;
        }

        if (state.Equip(item.Index) is { } reason)
        {
            Common.UiSound.Denied(); // extra.js:88
            Flash(reason);
            return;
        }

        Common.UiSound.Open();
        GameManager.Instance.SaveGame();
        Rebuild();
        FocusCell(item.Index);
    }

    private void Unequip(string slotId)
    {
        var state = GameManager.Instance.State;
        if (slotId == "arma" || !state.Unequip(slotId))
        {
            return;
        }

        UiSound();
        GameManager.Instance.SaveGame();
        Rebuild();
        if (_slots.TryGetValue(slotId, out var slot))
        {
            slot.Row.GrabFocus();
        }
    }

    private void UsePotion()
    {
        var state = GameManager.Instance.State;
        var result = state.TryUsePotion(Time.GetTicksMsec() / 1000.0, out _);
        switch (result)
        {
            case PotionResult.Used:
                Common.UiSound.Play("pick");
                Rebuild();
                FocusDefault();
                break;
            case PotionResult.NoPotions:
                Common.UiSound.Denied();
                Flash("Sem poções. Lúcia vende na estalagem.");
                break;
            case PotionResult.FullHealth:
                Common.UiSound.Denied();
                Flash("Vida já está cheia.");
                break;
        }
    }

    /// <summary>Aviso no rodapé (o HUD fica escondido atrás do inventário): some em 3 s.</summary>
    private void Flash(string message)
    {
        _status.Text = message;
        _statusT = 3f;
        _status.Modulate = Colors.White;
    }

    /// <summary>Aviso atual do rodapé (smoke test).</summary>
    public string StatusText => _statusT > 0f ? _status.Text : string.Empty;

    private void FocusCell(int backpackIndex)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].Item.Kind == ItemKind.Gear && _cells[i].Item.Index >= backpackIndex)
            {
                _cells[i].Row.GrabFocus();
                return;
            }
        }

        if (_cells.Count > 0)
        {
            _cells[^1].Row.GrabFocus();
        }
        else
        {
            FocusDefault();
        }
    }

    // --- Ecos (ui.js:198-212) ---

    private void RebuildEcos(GameState state)
    {
        var db = Database.Db;
        Ui.Clear(_ecoRow);
        _ecoCards.Clear();
        var index = 0;
        foreach (var eco in db.EcoData.Ecos)
        {
            var owned = state.Save.Ecos.Contains(eco.Id);
            var equipped = state.Save.EquippedEco == eco.Id;
            var tuned = state.Save.EcoLevels.GetValueOrDefault(eco.Id) > 0;
            var desc = Ui.Text(owned ? eco.Desc : string.Empty, UiFonts.Tiny, UiPalette.Body, wrap: true);
            desc.CustomMinimumSize = new Vector2(118, 0);
            var column = Ui.Column(3,
                IconRect.Create("i_eco", 44, owned ? 1f : 0.15f),
                Ui.Title(owned ? eco.Name : "???", UiFonts.Body, owned ? UiPalette.Creme : UiPalette.Disabled, HorizontalAlignment.Center),
                Ui.Text(owned ? eco.Skill : $"Deixado por: {eco.SourceLabel}", UiFonts.Tiny, owned ? UiPalette.EcoText : UiPalette.Dim, align: HorizontalAlignment.Center),
                desc,
                Ui.Spacer(horizontal: false));
            if (owned)
            {
                column.AddChild(Ui.Text($"Custo: {state.EcoCost(eco.Id)} de Essência{(tuned ? " (afinado)" : string.Empty)}", UiFonts.Tiny, UiPalette.Sub));
                column.AddChild(Ui.Text(KeyText.Expand(equipped ? "Equipado em [{key:eco_1}]" : "[{key:interact}] Equipar"), UiFonts.Tiny, equipped ? UiPalette.Ambar : UiPalette.Creme));
            }

            var card = new SelectableRow
            {
                NormalStyle = equipped ? "OrnatePanelAmber" : "CardNormal",
                FocusStyle = "CardFocus",
                Marker = false,
                CustomMinimumSize = new Vector2(132, 160),
            };
            card.AddChild(column);
            var id = eco.Id;
            card.OnActivate = () =>
            {
                if (GameManager.Instance.State.EquipEco(id))
                {
                    Common.UiSound.Play("eco");
                    GameManager.Instance.SaveGame();
                    var focus = _ecoCards.FindIndex(c => c.Card.HasFocus());
                    Rebuild();
                    if (focus >= 0 && focus < _ecoCards.Count)
                    {
                        _ecoCards[focus].Card.GrabFocus();
                    }
                }
            };
            _ecoRow.AddChild(card);
            _ecoCards.Add((card, index++));
        }
    }

    // --- Selos (ui.js:213-232) ---

    private void RebuildSeals(GameState state)
    {
        var db = Database.Db;
        Ui.Clear(_sealsPage);
        var ri = state.RankIndex;
        var intro = Ui.Text("Ranks são Selos marcados na pele. Outros caçadores veem o seu. No topo está o Alvor, o caçador que faz amanhecer.", UiFonts.Small, UiPalette.SubLight, wrap: true);
        var seals = Ui.Row(0);
        for (var i = 0; i < db.Ranks.Count; i++)
        {
            var r = db.Ranks[i];
            var got = i <= ri;
            var isCurrent = i == ri;
            var seal = new RankSeal
            {
                Rank = (Rank)i,
                CustomMinimumSize = new Vector2(isCurrent ? 28 : 21, isCurrent ? 28 : 21),
                LabelFont = UiFonts.Title700,
                LabelFontSize = isCurrent ? 9 : 7,
                Modulate = new Color(1, 1, 1, got ? 1f : 0.25f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            };
            var cell = Ui.Column(2, seal,
                Ui.Text(r.Title, UiFonts.Tiny, got ? UiPalette.FromHex(r.Color, UiPalette.Creme) : UiPalette.Disabled, align: HorizontalAlignment.Center),
                Ui.Text($"{r.Xp} XP", UiFonts.Tiny, UiPalette.Dim, align: HorizontalAlignment.Center));
            cell.CustomMinimumSize = new Vector2(57, 52);
            cell.Alignment = BoxContainer.AlignmentMode.End;
            seals.AddChild(cell);
        }

        var next = ri + 1 < db.Ranks.Count ? db.Ranks[ri + 1] : null;
        var cur = db.Ranks[ri];
        var bar = UiBar.Create("xp", new Vector2(560, 4));
        bar.SetRatio(next is null ? 1f : (state.Save.Xp - cur.Xp) / (float)(next.Xp - cur.Xp), instant: true);
        var progress = Ui.Text(next is null ? "Você chegou ao Alvor." : $"{state.Save.Xp} / {next.Xp} XP até o Selo {next.Id}", UiFonts.Small);

        string[] looks = ["look0", "look0", "look1", "look2", "look2", "look3", "look3", "look4", "look4", "look4"];
        (string Label, string Look)[] tiers = [("E–D", "look0"), ("C", "look1"), ("B–A", "look2"), ("S–SS", "look3"), ("SSS+", "look4")];
        var current = looks[Math.Clamp(ri, 0, looks.Length - 1)];
        var row = Ui.Row(4);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        foreach (var (label, look) in tiers)
        {
            var on = look == current;
            var img = IconRect.Create(look, 0, on ? 1f : 0.3f);
            img.CustomMinimumSize = new Vector2(100, 100);
            row.AddChild(Ui.Column(2, img, Ui.Title(label, UiFonts.Small, on ? UiPalette.Ambar : UiPalette.Disabled, HorizontalAlignment.Center)));
        }

        var panel = Ui.Ornate(Ui.Column(6, Ui.SectionTitle("Selos", UiFonts.Body), intro, seals, bar, progress, row));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _sealsPage.AddChild(panel);
    }
}

/// <summary>Elipses âmbar do palco do personagem (<c>ui.js:138</c>), desenhadas uma vez.</summary>
public partial class StageGlow : Control
{
    private static readonly Color Inner = new(242 / 255f, 165 / 255f, 65 / 255f, 0.6f);
    private static readonly Color Outer = new(242 / 255f, 165 / 255f, 65 / 255f, 0.25f);

    public StageGlow() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var c = Size / 2f;
        DrawSetTransform(c, 0, new Vector2(1f, 0.3f));
        DrawCircle(Vector2.Zero, Size.X * 0.45f, new Color(242 / 255f, 165 / 255f, 65 / 255f, 0.08f));
        DrawArc(Vector2.Zero, Size.X * 0.38f, 0, Mathf.Tau, 48, Inner, 1.5f, true);
        DrawArc(Vector2.Zero, Size.X * 0.47f, 0, Mathf.Tau, 48, Outer, 1f, true);
        DrawSetTransform(Vector2.Zero);
    }
}
