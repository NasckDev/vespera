using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Minimapa (frente B4a, DECISIONS B4a-D47): no canto superior direito, abaixo do nome do local, a pintura do mapa
/// reduzida com a vista da câmera, o jogador (seta na direção em que olha), saídas, portas de loja, NPCs, portais
/// abertos e baús já vistos. <c>M</c> ou o clique do analógico esquerdo abrem em tela cheia (com legenda; o jogo
/// para por baixo) e fecham. A pintura reduzida é feita uma vez por mapa (Lanczos + mipmaps); o desenho do canto é
/// refeito a 20 Hz, sem alocar.
/// </summary>
public partial class Minimap : CanvasLayer
{
    public const string BlockReason = "minimap";

    private static readonly Dictionary<string, ImageTexture> Thumbs = [];

    private MinimapView _view = null!;
    private double _redraw;

    /// <summary>Mundo que o minimapa mostra (o World liga no _Ready).</summary>
    public World? World { get; set; }

    public bool Expanded { get; private set; }

    /// <summary>Quantos marcadores foram desenhados no último quadro (para testes).</summary>
    public int MarkerCount => _view.MarkerCount;

    public override void _Ready()
    {
        Layer = 15;
        _view = new MinimapView { Name = "View", Owner2 = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_view);
        EventBus.Instance.MapChanged += OnMapChanged;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance is { } bus)
        {
            bus.MapChanged -= OnMapChanged;
        }

        if (Expanded)
        {
            GameManager.Instance?.UnblockGameplay(BlockReason);
        }
    }

    public void Toggle() => SetExpanded(!Expanded);

    public void SetExpanded(bool expanded)
    {
        if (expanded == Expanded)
        {
            return;
        }

        if (expanded && (GameManager.Instance?.IsGameplayBlocked ?? true))
        {
            return; // diálogo, troca de mapa, pesca… não abre por cima
        }

        Expanded = expanded;
        if (expanded)
        {
            GameManager.Instance.BlockGameplay(BlockReason);
        }
        else
        {
            GameManager.Instance?.UnblockGameplay(BlockReason);
        }

        AudioManager.Instance?.PlaySfx("ui", pitchJitter: 0f);
        _view.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.Map))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (Expanded && (@event.IsActionPressed(InputActions.Pause) || @event.IsActionPressed("ui_cancel")))
        {
            SetExpanded(false);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _redraw -= delta;
        if (Expanded || _redraw <= 0)
        {
            _redraw = 0.05;
            _view.QueueRedraw();
        }
    }

    /// <summary>Pintura reduzida do mapa (cache por id).</summary>
    internal static ImageTexture? Thumb(MapDef map, Texture2D? source)
    {
        if (Thumbs.TryGetValue(map.Id, out var cached))
        {
            return cached;
        }

        if (source?.GetImage() is not { } image)
        {
            return null;
        }

        image = (Image)image.Duplicate();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        var scale = MathF.Min(1f, 600f / MathF.Max(image.GetWidth(), image.GetHeight()));
        image.Resize(Math.Max(1, (int)(image.GetWidth() * scale)), Math.Max(1, (int)(image.GetHeight() * scale)), Image.Interpolation.Lanczos);
        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        Thumbs[map.Id] = texture;
        return texture;
    }

    private void OnMapChanged(string mapId, string mapName)
    {
        if (Expanded)
        {
            SetExpanded(false);
        }

        _view.QueueRedraw();
    }
}

/// <summary>Desenho do minimapa (canto ou tela cheia).</summary>
public partial class MinimapView : Control
{
    private const float CornerWidth = 120f;
    private const float CornerHeight = 70f;
    private const float Margin = 6f;
    private const float TopOffset = 28f; // abaixo do nome do local do HUD

    private static readonly Color Frame = new("#F4E7C6");
    private static readonly Color Back = new(0.06f, 0.05f, 0.1f, 0.82f);
    private static readonly Color ExitColor = new("#F2A541");
    private static readonly Color NpcColor = new("#F4E7C6");
    private static readonly Color ShopColor = new("#52E0C4");
    private static readonly Color PortalColor = new("#E64A8E");
    private static readonly Color ChestColor = new("#ffd27a");
    private static readonly Color PlayerColor = new("#ffffff");

    private readonly Vector2[] _arrow = new Vector2[3];
    private readonly Color[] _arrowColors = [PlayerColor, PlayerColor, PlayerColor];

    internal Minimap Owner2 { get; set; } = null!;

    public int MarkerCount { get; private set; }

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw()
    {
        MarkerCount = 0;
        var world = Owner2.World;
        if (world is null || !IsInstanceValid(world) || world.Map is null)
        {
            return;
        }

        var map = world.Map;
        var screen = GetViewportRect().Size;
        var expanded = Owner2.Expanded;
        MapRect panel;
        if (expanded)
        {
            DrawRect(new Rect2(Vector2.Zero, screen), new Color(0.03f, 0.02f, 0.06f, 0.78f));
            var w = screen.X - 80f;
            var h = screen.Y - 70f;
            panel = new MapRect(40f, 40f, w, h);
        }
        else
        {
            // C1-D7: o HUD reserva o lugar do minimapa (cresce com a escala da interface); sem HUD, o canto padrão.
            var slot = UI.Common.HudZones.MinimapSlot;
            panel = slot.Size.X > 0
                ? new MapRect(slot.Position.X, slot.Position.Y, slot.Size.X, slot.Size.Y)
                : new MapRect(screen.X - Margin - CornerWidth, TopOffset, CornerWidth, CornerHeight);
        }

        var proj = new MinimapProjection(map.ImageWidth, map.ImageHeight, panel);
        var img = proj.ImageRect;
        var imgRect = new Rect2(img.X, img.Y, img.Width, img.Height);
        DrawRect(imgRect.Grow(2f), Back);
        if (Minimap.Thumb(map, world.MapSprite.Texture) is { } thumb)
        {
            DrawTextureRect(thumb, imgRect, false, expanded ? Colors.White : new Color(1, 1, 1, 0.92f));
        }

        DrawRect(imgRect.Grow(1f), Frame with { A = 0.8f }, false, 1f);

        // vista da câmera
        var cam = world.Camera;
        var v = proj.ViewToPanel(new MapRect(cam.Position.X, cam.Position.Y, cam.VisibleSize.X, cam.VisibleSize.Y));
        DrawRect(new Rect2(v.X, v.Y, v.Width, v.Height), Frame with { A = 0.35f }, false, 1f);

        var dot = expanded ? 3f : 1.6f;
        foreach (var exit in map.Exits)
        {
            var c = proj.ToPanel(exit.Area.X + (exit.Area.Width / 2f), exit.Area.Y + (exit.Area.Height / 2f));
            DrawRect(new Rect2(c.X - dot, c.Y - dot, dot * 2f, dot * 2f), ExitColor);
            MarkerCount++;
            if (expanded)
            {
                DrawLabel(new Vector2(c.X + 5f, c.Y + 3f), NameOf(exit.To), ExitColor);
            }
        }

        foreach (var it in map.Interactables)
        {
            if (world.GetInteractable(it.Id) is not { } node || !node.Enabled)
            {
                continue;
            }

            var c = proj.ToPanel(it.X, it.Y);
            var color = it.Action.StartsWith("talk", StringComparison.Ordinal) ? NpcColor
                : it.Action == "portalDialog" ? PortalColor
                : it.Action == "oficiosDialog" ? ShopColor
                : ExitColor with { A = 0.8f };
            DrawCircle(new Vector2(c.X, c.Y), dot, color);
            MarkerCount++;
            if (expanded)
            {
                DrawLabel(new Vector2(c.X + 5f, c.Y + 3f), node.Prompt, color);
            }
        }

        foreach (var chest in world.Dressing.Chests)
        {
            if (!WorldDressing.IsDiscovered(chest))
            {
                continue;
            }

            var c = proj.ToPanel(chest.Position.X, chest.Position.Y);
            var color = chest.IsOpen ? ChestColor with { A = 0.45f } : ChestColor;
            DrawRect(new Rect2(c.X - dot, c.Y - (dot * 0.7f), dot * 2f, dot * 1.4f), color);
            MarkerCount++;
        }

        // jogador: seta na direção em que olha
        var player = world.Player;
        var pos = player.VisualPosition;
        var pp = proj.ToPanel(pos.X, pos.Y);
        var dir = player.Facing == Vector2.Zero ? Vector2.Down : player.Facing.Normalized();
        var size = expanded ? 7f : 4f;
        var center = new Vector2(pp.X, pp.Y);
        var side = new Vector2(-dir.Y, dir.X);
        _arrow[0] = center + (dir * size);
        _arrow[1] = center - (dir * size * 0.6f) + (side * size * 0.65f);
        _arrow[2] = center - (dir * size * 0.6f) - (side * size * 0.65f);
        DrawCircle(center, size * 0.9f, PortalColor with { A = 0.35f });
        DrawPolygon(_arrow, _arrowColors);
        MarkerCount++;

        if (expanded)
        {
            DrawLabel(new Vector2(img.X, img.Y - 6f), map.Name, Frame, 12);
            var x = img.X;
            var y = img.Bottom + 14f;
            x = DrawLegend(x, y, ExitColor, square: true, "saídas");
            x = DrawLegend(x, y, NpcColor, square: false, "pessoas");
            x = DrawLegend(x, y, ShopColor, square: false, "lojas");
            x = DrawLegend(x, y, PortalColor, square: false, "Brechas");
            x = DrawLegend(x, y, ChestColor, square: true, "baús vistos");
            DrawLabel(new Vector2(x + 12f, y + 3f), "M / L3: fechar", Frame with { A = 0.7f });
        }
    }

    private float DrawLegend(float x, float y, Color color, bool square, string text)
    {
        if (square)
        {
            DrawRect(new Rect2(x, y - 2f, 5f, 5f), color);
        }
        else
        {
            DrawCircle(new Vector2(x + 2.5f, y + 0.5f), 2.6f, color);
        }

        DrawLabel(new Vector2(x + 8f, y + 3f), text, color);
        return x + 14f + GetThemeDefaultFont().GetStringSize(text, HorizontalAlignment.Left, -1, 8).X;
    }

    private void DrawLabel(Vector2 at, string text, Color color, int size = 8)
    {
        var font = GetThemeDefaultFont();
        DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, size, 3, new Color(0.04f, 0.03f, 0.07f, 0.9f));
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
    }

    private static string NameOf(string mapId) => World.Catalog.TryGet(mapId, out var m) ? m.Name : mapId;
}
