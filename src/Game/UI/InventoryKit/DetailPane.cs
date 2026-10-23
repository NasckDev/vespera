using Godot;

namespace Vespera.Game.UI;

/// <summary>
/// Painel de detalhes com a moldura de dica (tooltip) pintada: título na cor do rank, linhas de atributos, bônus de conjunto.
/// Mostra o que está focado, então funciona igual com teclado, controle e mouse. O texto é BBCode.
/// </summary>
public partial class DetailPane : PanelContainer
{
    private const int Padding = 30;
    private const int IconSize = 176;
    private readonly RichTextLabel _text;
    private readonly VBoxContainer _column;
    private readonly CenterContainer _iconHolder;

    public DetailPane()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
        _column = UiKit.MakeColumn(10);
        AddChild(_column);
        _iconHolder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _column.AddChild(_iconHolder);
        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _column.AddChild(_text);
    }

    public string Plain => _text.GetParsedText();

    public override void _Ready()
    {
        var style = GetThemeStylebox("panel", "TooltipPanel");
        if (style is StyleBoxTexture texture)
        {
            var copy = (StyleBoxTexture)texture.Duplicate();
            copy.ContentMarginLeft = Padding;
            copy.ContentMarginRight = Padding;
            copy.ContentMarginTop = Padding;
            copy.ContentMarginBottom = Padding;
            AddThemeStyleboxOverride("panel", copy);
        }
    }

    /// <summary>Ícone grande (escala 2x) no topo do painel; null esconde.</summary>
    public void ShowIcon(Texture2D? icon, Core.Ranks.Rank? rank)
    {
        foreach (var child in _iconHolder.GetChildren())
        {
            _iconHolder.RemoveChild(child);
            child.QueueFree();
        }

        _iconHolder.Visible = icon is not null;
        if (icon is not null)
        {
            _iconHolder.AddChild(new ItemIconSlot(IconSize, icon, rank));
        }
    }

    public void ShowText(string bbcode) => _text.Text = bbcode;
}
