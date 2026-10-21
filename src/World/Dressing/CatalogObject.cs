using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Sprite de um objeto do catálogo (<see cref="ObjectDef"/>), pronto para o y-sort: pé na origem do nó, espelhado com
/// <c>flip</c>. <c>atlas:&lt;chave&gt;</c> usa o atlas da v2 (escala de objeto, D41); caminhos <c>res://</c> usam
/// <c>ppu</c> (px do sprite por u) e a âncora do catálogo, ou o centro da base do recorte opaco (D10).
/// </summary>
public static class ObjectVisuals
{
    private static readonly Dictionary<string, Vector2> AnchorCache = [];

    /// <summary>Cria o sprite e devolve o retângulo que ele ocupa em u, relativo ao pé (já espelhado).</summary>
    public static Sprite2D Create(ObjectDef def, bool flip, out Rect2 bounds)
    {
        Sprite2D sprite;
        Vector2 size;
        Vector2 anchor;
        float unitsPerPixel;
        if (def.IsAtlasSprite)
        {
            var atlas = new AtlasSprite { Key = def.AtlasKey, ScaleClass = SpriteScaleClass.Prop };
            sprite = atlas;
            if (AtlasLibrary.TryGet(def.AtlasKey, out var frame))
            {
                size = frame.Texture.GetSize();
                anchor = frame.Anchor;
                unitsPerPixel = WorldScale.Prop / frame.Density;
            }
            else
            {
                size = Vector2.Zero;
                anchor = Vector2.Zero;
                unitsPerPixel = 1f;
            }
        }
        else
        {
            var texture = ResourceLoader.Exists(def.Sprite) ? GD.Load<Texture2D>(def.Sprite) : null;
            size = texture?.GetSize() ?? Vector2.Zero;
            anchor = def.Anchor is { } a ? new Vector2(a.X, a.Y) : BaseCenter(def.Sprite, texture);
            unitsPerPixel = 1f / def.PixelsPerUnit;
            sprite = new Sprite2D { Texture = texture, Centered = false, Offset = -anchor, Scale = Vector2.One * unitsPerPixel };
        }

        if (flip)
        {
            sprite.Scale = new Vector2(-MathF.Abs(sprite.Scale.X), sprite.Scale.Y);
        }

        var left = -anchor.X * unitsPerPixel;
        var right = (size.X - anchor.X) * unitsPerPixel;
        bounds = new Rect2(flip ? -right : left, -anchor.Y * unitsPerPixel, right - left, size.Y * unitsPerPixel);
        return sprite;
    }

    private static Vector2 BaseCenter(string path, Texture2D? texture)
    {
        if (texture is null)
        {
            return Vector2.Zero;
        }

        if (!AnchorCache.TryGetValue(path, out var anchor))
        {
            var used = texture.GetImage() is { } image ? image.GetUsedRect() : new Rect2I(Vector2I.Zero, (Vector2I)texture.GetSize());
            var (ax, ay) = Core.Atlas.AtlasIndex.BaseCenterAnchor(used.Position.X, used.Position.Y, used.Size.X, used.Size.Y);
            anchor = new Vector2((float)ax, (float)ay);
            AnchorCache[path] = anchor;
        }

        return anchor;
    }
}

/// <summary>
/// Objeto do catálogo posto pelo layout (<c>objects</c>): sprite no y-sort com o pé na origem, colisão pelo
/// <c>footprint</c>, translúcido com fade quando cobre o jogador (<c>occluder</c>, as mesmas regras dos recortes D44) e
/// balanço barato (<c>sway</c>, shader de vértice). Luz, interação, quebrável e baú são montados pelo
/// <see cref="WorldDressing"/> a partir do mesmo <see cref="ObjectDef"/>.
/// </summary>
public partial class CatalogObject : Node2D
{
    private static Shader? _swayShader;

    private readonly MapPoint[] _rect = new MapPoint[4];
    private MapRect _bounds;
    private float _coveredAlpha = 0.4f;
    private float _fadeRate = 10f;
    private float _alpha = 1f;
    private bool _occluder;

    public ObjectDef Def { get; private set; } = null!;

    public Sprite2D Sprite { get; private set; } = null!;

    /// <summary>Quem fica translúcido por trás (o jogador).</summary>
    public Player.Player? Target { get; set; }

    public float Alpha => _alpha;

    public bool IsCovering { get; private set; }

    /// <summary>Retângulo do sprite no mundo (u).</summary>
    public MapRect Bounds => _bounds;

    public void Setup(ObjectDef def, LayoutObject placed, float coveredAlpha, float fadeRate, bool sway)
    {
        Def = def;
        Name = $"Object_{def.Id}_{(int)placed.X}_{(int)placed.Y}";
        Position = new Vector2(placed.X, placed.Y);
        _coveredAlpha = coveredAlpha;
        _fadeRate = fadeRate;
        _occluder = def.Occluder;

        AddChild(new AtlasSprite { Key = "shadow", ScaleClass = SpriteScaleClass.Prop, Modulate = new Color(1, 1, 1, 0.35f), Position = new Vector2(0, 1) });
        Sprite = ObjectVisuals.Create(def, placed.Flip, out var local);
        AddChild(Sprite);
        _bounds = new MapRect(placed.X + local.Position.X, placed.Y + local.Position.Y, local.Size.X, local.Size.Y);
        _rect[0] = new MapPoint(_bounds.X, _bounds.Y);
        _rect[1] = new MapPoint(_bounds.Right, _bounds.Y);
        _rect[2] = new MapPoint(_bounds.Right, _bounds.Bottom);
        _rect[3] = new MapPoint(_bounds.X, _bounds.Bottom);

        if (sway && def.Sway)
        {
            _swayShader ??= GD.Load<Shader>("res://src/World/Shaders/sway_sprite.gdshader");
            var material = new ShaderMaterial { Shader = _swayShader };
            var scaleY = MathF.Max(1e-4f, MathF.Abs(Sprite.Scale.Y));
            var heightPx = local.Size.Y / scaleY;
            material.SetShaderParameter("height", MathF.Max(1f, heightPx));
            material.SetShaderParameter("amp", MathF.Min(2.5f, 0.8f + (local.Size.Y / 40f)) / scaleY);
            material.SetShaderParameter("phase", (placed.X * 0.037f) + (placed.Y * 0.011f));
            Sprite.Material = material;
        }

        if (def.Footprint is { } footprint)
        {
            var points = new Vector2[footprint.Polygon.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var p = footprint.Polygon[i];
                points[i] = new Vector2(placed.Flip ? -p.X : p.X, p.Y);
            }

            var body = new StaticBody2D { Name = "Footprint", CollisionLayer = 1, CollisionMask = 0 };
            body.AddChild(new CollisionPolygon2D { Polygon = points });
            AddChild(body);
        }

        SetProcess(_occluder);
    }

    public override void _Process(double delta)
    {
        var covering = false;
        if (Target is { } t && IsInstanceValid(t))
        {
            var foot = t.VisualPosition;
            var height = t.VisualHeight;
            var body = OcclusionRules.BodyRect(foot.X, foot.Y, height * 0.45f, height);
            covering = OcclusionRules.IsBehind(foot.Y, Position.Y) && OcclusionRules.Covers(_rect, _bounds, body);
        }

        IsCovering = covering;
        var target = covering ? _coveredAlpha : 1f;
        if (_alpha == target)
        {
            return;
        }

        _alpha = MathF.Abs(_alpha - target) < 0.002f ? target : OcclusionRules.Approach(_alpha, target, _fadeRate, MathF.Min(0.05f, (float)delta));
        Sprite.Modulate = new Color(1, 1, 1, _alpha);
    }
}
