using System;
using System.Collections.Generic;
using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Boot;

namespace Vespera.Game.UI;

/// <summary>
/// Escolha da arma (classe) do caçador: três cartões com o caçador parado animado, uma linha de estilo, três pontos
/// (alcance, ritmo, ponto forte) e a dificuldade. Aparece uma vez, em "Nova história" (depois do espaço de save, antes do
/// prólogo), e de novo só quando a Odete oferece "Trocar de arma". Mouse: passar o mouse destaca, clicar escolhe, clicar no
/// escolhido ou em "Escolher" confirma, botão direito volta. Teclado e controle: setas e confirmar.
/// </summary>
public partial class ClassSelectScreen : UiScreen
{
    private sealed record ClassInfo(HunterClass Hunter, string Style, string Reach, string Pace, string Strength, string Difficulty);

    private static readonly ClassInfo[] Infos =
    [
        new(HunterClass.Lamina, "Espada equilibrada. Boa para aprender.", "Alcance: curto", "Ritmo: médio, combo de 3 golpes", "Ponto forte: corte giratório em volta de você", "Fácil"),
        new(HunterClass.Arco, "Ataca de longe, com calma.", "Alcance: longo", "Ritmo: médio, segure para tensionar", "Ponto forte: flecha de luz atravessa vários monstros", "Média"),
        new(HunterClass.Martelo, "Lento e muito forte.", "Alcance: curto", "Ritmo: lento, cada golpe pesa", "Ponto forte: quebra partes dos monstros em dobro", "Difícil"),
    ];

    private readonly Action<HunterClass> _onChosen;
    private readonly string _title;
    private readonly Action? _onCancel;
    private readonly string _confirmText;
    private readonly List<ClassCard> _cards = [];
    private Button _confirm = null!;
    private int _index;

    /// <param name="onChosen">Chamado com a classe confirmada.</param>
    /// <param name="start">Cartão que começa selecionado.</param>
    public ClassSelectScreen(Action<HunterClass> onChosen, HunterClass start = HunterClass.Lamina, string title = "Escolha sua arma", string confirmText = "Escolher esta arma", Action? onCancel = null)
    {
        _onChosen = onChosen;
        _onCancel = onCancel;
        _title = title;
        _confirmText = confirmText;
        _index = Math.Max(0, Array.FindIndex(Infos, i => i.Hunter == start));
    }

    /// <summary>Classe destacada agora.</summary>
    public HunterClass Selected => Infos[_index].Hunter;

    /// <summary>Cartões (para o teste de mouse).</summary>
    public IReadOnlyList<Control> Cards => _cards;

    public Button ConfirmButton => _confirm;

    protected override Control? DefaultFocus() => _cards[_index];

    protected override void Build()
    {
        var panel = BuildPage(_title, out var column, sideMargin: 110);
        var inner = UiKit.MakeColumn(14);
        panel.AddChild(UiKit.PadForGlow(inner));
        inner.AddChild(UiKit.MakeLabel("Cada arma luta de um jeito. Escolha a que combina com você.", UiTheme.DimVariation, HorizontalAlignment.Center));
        var row = UiKit.MakeRow(28);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        inner.AddChild(row);
        for (var i = 0; i < Infos.Length; i++)
        {
            var card = new ClassCard(Infos[i], i, OnCardClicked, OnCardHovered);
            _cards.Add(card);
            row.AddChild(card);
        }

        var footer = UiKit.MakeRow(24);
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        column.AddChild(footer);
        _confirm = UiKit.MakeButton(_confirmText, Confirm, 420);
        var back = UiKit.MakeButton("Voltar", Back, 300);
        footer.AddChild(_confirm);
        footer.AddChild(back);
        UiKit.WrapFocus([_confirm, back]);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsVisibleInTree() || !SceneRouter.Instance.IsActive(this))
        {
            return;
        }

        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is ClassCard && (@event.IsActionPressed("ui_left") || @event.IsActionPressed("ui_right")))
        {
            Select((_index + (@event.IsActionPressed("ui_left") ? -1 : 1) + Infos.Length) % Infos.Length, grab: true);
            GetViewport().SetInputAsHandled();
            return;
        }

        base._UnhandledInput(@event);
    }

    private void OnCardHovered(int index) => Select(index, grab: false);

    private void OnCardClicked(int index, bool confirm)
    {
        Select(index, grab: true);
        if (confirm)
        {
            Confirm();
        }
    }

    private void Select(int index, bool grab)
    {
        if (index == _index && !grab)
        {
            return;
        }

        _index = index;
        UiScreen.PlayMoveSound();
        if (grab)
        {
            _cards[index].GrabFocus();
        }

        Refresh();
    }

    private void Confirm() => _onChosen(Infos[_index].Hunter);

    protected override void Back()
    {
        base.Back();
        _onCancel?.Invoke();
    }

    private void Refresh()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].SetSelected(i == _index);
        }
    }

    /// <summary>Cartão de uma arma: caçador animado, nome, estilo, três pontos e dificuldade.</summary>
    private sealed partial class ClassCard : PanelContainer
    {
        private readonly ClassInfo _info;
        private readonly int _index;
        private readonly Action<int, bool> _clicked;
        private readonly Action<int> _hovered;

        public ClassCard(ClassInfo info, int index, Action<int, bool> clicked, Action<int> hovered)
        {
            _info = info;
            _index = index;
            _clicked = clicked;
            _hovered = hovered;
        }

        public override void _Ready()
        {
            FocusMode = FocusModeEnum.All;
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            CustomMinimumSize = new Vector2(480, 0);
            SizeFlagsVertical = SizeFlags.ExpandFill;
            PivotOffset = new Vector2(240, 300);
            MouseEntered += () => _hovered(_index);
            FocusEntered += () => _hovered(_index);
            AddChild(new FocusGlow { Outset = 14 });
            var inner = UiKit.MakeColumn(8);
            AddChild(inner);
            var stage = new Control { CustomMinimumSize = new Vector2(440, 250), MouseFilter = MouseFilterEnum.Ignore };
            var sprite = new AnimatedSprite2D
            {
                SpriteFrames = HunterSprites.Build(_info.Hunter),
                Position = new Vector2(220, 238),
                Offset = HunterSprites.FootOffset,
                Scale = new Vector2(1.9f, 1.9f),
            };
            stage.AddChild(sprite);
            sprite.Play(HunterSprites.IdleAnimation(Facing.Down));
            inner.AddChild(stage);
            inner.AddChild(IgnoreMouse(UiKit.MakeLabel(HunterSprites.DisplayName(_info.Hunter), UiTheme.HeadingVariation, HorizontalAlignment.Center)));
            inner.AddChild(IgnoreMouse(Wrapped(_info.Style, UiTheme.ReadingVariation)));
            inner.AddChild(IgnoreMouse(Wrapped("• " + _info.Reach, null)));
            inner.AddChild(IgnoreMouse(Wrapped("• " + _info.Pace, null)));
            inner.AddChild(IgnoreMouse(Wrapped("• " + _info.Strength, null)));
            inner.AddChild(IgnoreMouse(UiKit.MakeLabel("Dificuldade: " + _info.Difficulty, UiTheme.DimVariation, HorizontalAlignment.Center)));
        }

        public void SetSelected(bool selected)
        {
            AddThemeStyleboxOverride("panel", UiTheme.RowFrame(selected));
            Modulate = selected ? Colors.White : new Color(0.72f, 0.72f, 0.8f);
            Scale = selected ? new Vector2(1.03f, 1.03f) : Vector2.One;
        }

        public override void _GuiInput(InputEvent @event)
        {
            var click = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
            var accept = @event.IsActionPressed("ui_accept");
            if (click || accept)
            {
                AcceptEvent();
                UiSfx.Play(UiSound.Confirm);
                _clicked(_index, accept || @event is InputEventMouseButton { DoubleClick: true });
            }
        }

        private static Label Wrapped(string text, string? variation)
        {
            var label = UiKit.MakeWrapped(text, variation);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            return label;
        }

        private static Control IgnoreMouse(Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
            return control;
        }
    }
}
