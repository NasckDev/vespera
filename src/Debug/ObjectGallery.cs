using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Debug;

/// <summary>
/// Galeria de depuração da biblioteca de objetos (<c>src/Data/objects.json</c>, DECISIONS B5-1). Mostra cada objeto
/// do tema no tamanho calibrado, em fileiras que começam pelo jogador (<c>c_sword_front</c>) e por uma porta
/// (<c>door_arched</c>), na mesma escala da câmera dos mapas hd (400 u de altura). Footprint (ciano), luz (âmbar) e
/// âncora (ponto) são desenhados por <see cref="GalleryOverlay"/>.
/// Abre só por flag (<c>-- --object-gallery[=tema]</c>) ou pelo smoke. ←/→ troca o tema, ↑/↓ a página (teclado ou
/// gamepad: ações <c>move_*</c>).
/// </summary>
public partial class ObjectGallery : Node2D
{
    public const string ScenePath = "res://scenes/Debug/ObjectGallery.tscn";
    public const string CommandLineFlag = "--object-gallery";
    public const string CatalogPath = "res://src/Data/objects.json";
    public const string DoorObjectId = "door_arched";

    /// <summary>Altura visível em u: igual à dos mapas hd (WorldCamera), então o tamanho na tela é o do jogo.</summary>
    public const float ViewHeight = 400f;

    private const float Margin = 14f;
    private const float TopMargin = 26f; // espaço do cabeçalho
    private const float Gap = 8f;
    private const float LabelHeight = 12f;
    private const float RowGap = 6f;
    private const float SwayDegrees = 1.6f;
    private const float LabelFontSize = 5.5f;
    private const float LabelRasterSize = 24f;
    private const float LabelCharWidth = 2.7f;

    private static ObjectCatalog? _catalog;

    private readonly List<Sprite2D> _sprites = [];
    private readonly List<ObjectDef> _defs = [];
    private readonly List<Sprite2D> _swaying = [];
    private readonly List<float> _swayPhase = [];
    private readonly List<GalleryOverlay.Mark> _marks = [];
    private readonly List<float> _baselines = [];
    private Node2D _content = null!;
    private GalleryOverlay _overlay = null!;
    private Camera2D _camera = null!;
    private Label _header = null!;
    private List<List<List<ObjectDef>>> _pages = [];
    private float _time;

    public static ObjectCatalog Catalog => _catalog ??= ObjectCatalog.Parse(Godot.FileAccess.GetFileAsString(CatalogPath));

    public string Theme { get; private set; } = string.Empty;

    public int Page { get; private set; }

    public int PageCount => _pages.Count;

    /// <summary>Sprites de objetos da página atual (sem o jogador e a porta de referência).</summary>
    public IReadOnlyList<Sprite2D> Sprites => _sprites;

    /// <summary>Objeto de cada sprite de <see cref="Sprites"/>, na mesma ordem.</summary>
    public IReadOnlyList<ObjectDef> Shown => _defs;

    /// <summary>Jogador de referência da primeira fileira (altura em u = <see cref="ReferenceHeight"/>).</summary>
    public AtlasSprite? ReferencePlayer { get; private set; }

    /// <summary>Porta de referência da primeira fileira.</summary>
    public Sprite2D? ReferenceDoor { get; private set; }

    public float ViewWidth { get; private set; } = ViewHeight * 16f / 9f;

    /// <summary>Altura em u de um sprite desenhado (textura usada × escala).</summary>
    public static float WorldHeightOf(Sprite2D sprite) =>
        sprite.Texture is { } t && t.GetImage() is { } img ? img.GetUsedRect().Size.Y * Math.Abs(sprite.GlobalScale.Y) : 0f;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        var background = new CanvasLayer { Name = "Background", Layer = -10 };
        AddChild(background);
        var fill = new ColorRect { Color = new Color("1b1f3b"), MouseFilter = Control.MouseFilterEnum.Ignore };
        fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.AddChild(fill);
        _content = new Node2D { Name = "Content", YSortEnabled = false };
        AddChild(_content);
        _overlay = new GalleryOverlay { Name = "Overlay", ZIndex = 10, Marks = _marks, Baselines = _baselines };
        AddChild(_overlay);
        _camera = new Camera2D { Name = "Camera" };
        AddChild(_camera);
        _camera.MakeCurrent();
        var hud = new CanvasLayer { Name = "Hud", Layer = 5 };
        AddChild(hud);
        _header = new Label { Name = "Header", Position = new Vector2(8, 4) };
        _header.AddThemeFontSizeOverride("font_size", 12);
        _header.AddThemeColorOverride("font_color", new Color("f4e7c6"));
        _header.AddThemeColorOverride("font_outline_color", new Color("1b1f3b"));
        _header.AddThemeConstantOverride("outline_size", 4);
        hud.AddChild(_header);

        var wanted = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(CommandLineFlag + "=", StringComparison.Ordinal))?[(CommandLineFlag.Length + 1)..];
        ShowTheme(wanted is not null && Catalog.Themes.ContainsKey(wanted) ? wanted : Catalog.Themes.Keys.First());
    }

    public override void _PhysicsProcess(double delta) => HandleInput();

    public override void _Process(double delta)
    {
        _time += (float)delta;
        for (var i = 0; i < _swaying.Count; i++)
        {
            _swaying[i].RotationDegrees = Mathf.Sin((_time * 1.7f) + _swayPhase[i]) * SwayDegrees;
        }
    }

    /// <summary>Lido por polling no frame de física (como o jogador): funciona com teclado, gamepad e com as ações simuladas do smoke.</summary>
    private void HandleInput()
    {
        var themes = Catalog.Themes.Keys.ToList();
        var index = themes.IndexOf(Theme);
        if (Input.IsActionJustPressed(InputActions.MoveRight))
        {
            ShowTheme(themes[(index + 1) % themes.Count]);
        }
        else if (Input.IsActionJustPressed(InputActions.MoveLeft))
        {
            ShowTheme(themes[(index + themes.Count - 1) % themes.Count]);
        }
        else if (Input.IsActionJustPressed(InputActions.MoveDown))
        {
            ShowPage((Page + 1) % Math.Max(1, PageCount));
        }
        else if (Input.IsActionJustPressed(InputActions.MoveUp))
        {
            ShowPage((Page + PageCount - 1) % Math.Max(1, PageCount));
        }
    }

    /// <summary>Mostra a primeira página de um tema.</summary>
    public void ShowTheme(string theme)
    {
        if (!Catalog.Themes.ContainsKey(theme))
        {
            Log.Warning($"Galeria: tema '{theme}' não existe.");
            return;
        }

        Theme = theme;
        var viewport = GetViewportRect().Size;
        ViewWidth = ViewHeight * viewport.X / Math.Max(1f, viewport.Y);
        _pages = Paginate(Catalog.InTheme(theme));
        ShowPage(0);
    }

    public void ShowPage(int page)
    {
        Page = Math.Clamp(page, 0, Math.Max(0, PageCount - 1));
        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child); // sai já da árvore: os nomes (= ids) dos novos sprites não colidem
            child.QueueFree();
        }

        _sprites.Clear();
        _defs.Clear();
        _swaying.Clear();
        _swayPhase.Clear();
        _marks.Clear();
        _baselines.Clear();
        ReferencePlayer = null;
        ReferenceDoor = null;

        var y = TopMargin;
        var rows = PageCount > 0 ? _pages[Page] : [];
        foreach (var row in rows)
        {
            var baseline = y + RowHeight(row);
            _baselines.Add(baseline);
            var x = Margin + AddReferences(baseline);
            foreach (var o in row)
            {
                var slot = SlotWidth(o);
                var foot = new Vector2(x + (slot / 2f), baseline);
                AddObject(o, foot);
                AddLabel(o.Id, new Vector2(x, baseline + 1f), slot);
                x += slot + Gap;
            }

            y = baseline + LabelHeight + RowGap;
        }

        _camera.Zoom = Vector2.One * (GetViewportRect().Size.Y / ViewHeight);
        _camera.Position = new Vector2(ViewWidth / 2f, ViewHeight / 2f);
        _overlay.Width = ViewWidth;
        _overlay.QueueRedraw();
        var themeIndex = Catalog.Themes.Keys.ToList().IndexOf(Theme) + 1;
        _header.Text = $"{Catalog.Themes[Theme]} ({Theme}) · tema {themeIndex}/{Catalog.Themes.Count} · página {Page + 1}/{PageCount} · " +
                       $"{Catalog.InTheme(Theme).Count} objetos · jogador {Catalog.PlayerHeight:0.0} u · ←/→ tema  ↑/↓ página";
    }

    private float AddReferences(float baseline)
    {
        var x = Margin;
        var holder = new Node2D { Position = new Vector2(x + 10f, baseline) };
        _content.AddChild(holder);
        holder.AddChild(new AtlasSprite { Key = "shadow", Modulate = new Color(1, 1, 1, 0.5f), Position = new Vector2(0, 2) });
        var player = new AtlasSprite { Key = "c_sword_front" };
        holder.AddChild(player);
        ReferencePlayer ??= player;
        AddLabel("jogador", new Vector2(x, baseline + 1f), 24f);
        x += 24f + Gap;
        if (Catalog.TryGet(DoorObjectId, out var door))
        {
            var sprite = AddSprite(door, new Vector2(x + (door.WorldWidth / 2f), baseline));
            ReferenceDoor ??= sprite;
            AddLabel("porta", new Vector2(x, baseline + 1f), door.WorldWidth);
            x += door.WorldWidth + (2 * Gap);
        }

        return x - Margin;
    }

    private void AddObject(ObjectDef o, Vector2 foot)
    {
        var sprite = AddSprite(o, foot);
        _sprites.Add(sprite);
        _defs.Add(o);
        if (o.Sway)
        {
            _swaying.Add(sprite);
            _swayPhase.Add(foot.X * 0.05f);
        }

        _marks.Add(new GalleryOverlay.Mark(foot, o.Footprint is { } f ? new Rect2(foot.X + f.X, foot.Y + f.Y, f.Width, f.Height) : null,
            o.Light is { } l ? foot + new Vector2(l.Offset.X, l.Offset.Y) : null, o.Light is { } l2 ? new Color(l2.Color) : Colors.Transparent));
    }

    private Sprite2D AddSprite(ObjectDef o, Vector2 foot)
    {
        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(o.File),
            Centered = false,
            Offset = new Vector2(-o.AnchorX, -o.AnchorY),
            Scale = Vector2.One / o.Density,
            Position = foot,
            ZIndex = o.Layer switch { ObjectLayer.Floor => -1, ObjectLayer.Overhead => 1, _ => 0 },
            Name = o.Id,
        };
        _content.AddChild(sprite);
        return sprite;
    }

    private void AddLabel(string text, Vector2 at, float width)
    {
        // Fonte rasterizada grande e reduzida: a 6 u direto, a fonte pixel do tema sai com espaçamento quebrado.
        var label = new Label
        {
            Text = text,
            Position = at,
            Scale = Vector2.One * (LabelFontSize / LabelRasterSize),
            Size = new Vector2(Math.Max(width, 30f) * LabelRasterSize / LabelFontSize, LabelHeight * LabelRasterSize / LabelFontSize),
            ClipText = false,
            TextureFilter = TextureFilterEnum.Linear,
        };
        label.AddThemeFontSizeOverride("font_size", (int)LabelRasterSize);
        label.AddThemeColorOverride("font_color", new Color("c8c4dc"));
        _content.AddChild(label);
    }

    private static float SlotWidth(ObjectDef o) => Math.Max(o.WorldWidth, (o.Id.Length * LabelCharWidth) + 2f);

    private static float RowHeight(List<ObjectDef> row) =>
        Math.Max(40f, row.Count == 0 ? 0f : row.Max(o => o.WorldHeight));

    /// <summary>Fileiras que cabem na largura visível e páginas que cabem nos 400 u de altura.</summary>
    private List<List<List<ObjectDef>>> Paginate(IReadOnlyList<ObjectDef> objects)
    {
        var refWidth = 24f + Gap + (Catalog.TryGet(DoorObjectId, out var door) ? door.WorldWidth + (2 * Gap) : 0f);
        var rows = new List<List<ObjectDef>> { new() };
        var x = Margin + refWidth;
        foreach (var o in objects)
        {
            var w = SlotWidth(o);
            if (x + w > ViewWidth - Margin && rows[^1].Count > 0)
            {
                rows.Add([]);
                x = Margin + refWidth;
            }

            rows[^1].Add(o);
            x += w + Gap;
        }

        var pages = new List<List<List<ObjectDef>>> { new() };
        var y = TopMargin;
        foreach (var row in rows)
        {
            var h = RowHeight(row) + LabelHeight + RowGap;
            if (y + h > ViewHeight && pages[^1].Count > 0)
            {
                pages.Add([]);
                y = TopMargin;
            }

            pages[^1].Add(row);
            y += h;
        }

        return pages;
    }
}
