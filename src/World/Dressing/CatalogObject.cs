using System;
using System.Collections.Generic;
using Godot;
using Vespera.Core.Art;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Sprite de um objeto do catálogo (<see cref="DressingDef"/>), pronto para o y-sort: pé na origem do nó, espelhado com
/// <c>flip</c>. <c>atlas:&lt;chave&gt;</c> usa o atlas da v2 (escala de objeto, D41); caminhos <c>res://</c> usam
/// <c>ppu</c> (px do sprite por u) e a âncora do catálogo, ou o centro da base do recorte opaco (D10).
/// </summary>
public static class ObjectVisuals
{
    private static readonly Dictionary<string, Vector2> AnchorCache = [];

    /// <summary>Cria o sprite e devolve o retângulo que ele ocupa em u, relativo ao pé (já espelhado).</summary>
    public static Sprite2D Create(DressingDef def, bool flip, out Rect2 bounds)
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
/// <see cref="WorldDressing"/> a partir do mesmo <see cref="DressingDef"/>.
/// Objetos de onde saiu a arte de estados do designer (DECISIONS C2-D5) trocam o quadro pelo estado: lampião e braseiro
/// apagado/aceso com a chama em 4 quadros (seguem o halo da luz), flor florida/colhida/brotando, alavanca e porta
/// alternam ao usar, quadro de avisos com a arte nova.
/// </summary>
public partial class CatalogObject : Node2D
{
    private static Shader? _swayShader;
    private static readonly StringName ArtOn = "on";
    private static readonly StringName ArtOff = "off";
    private static readonly StringName ArtOpen = "open";
    private static readonly StringName ArtClosed = "closed";
    private static readonly StringName ArtFlowering = "flowering";
    private static readonly StringName ArtHarvested = "harvested";
    private static readonly StringName ArtSprouting = "sprouting";
    private static readonly StringName ArtBoard = "board";

    private ArtFrames? _art;
    private StringName _artState = ArtOff;
    private float _artTime;
    private int _artFrame = -1;

    private readonly MapPoint[] _rect = new MapPoint[4];
    private MapRect _bounds;
    private float _coveredAlpha = 0.4f;
    private float _fadeRate = 10f;
    private float _alpha = 1f;
    private bool _occluder;

    public DressingDef Def { get; private set; } = null!;

    public Sprite2D Sprite { get; private set; } = null!;

    /// <summary>Quem fica translúcido por trás (o jogador).</summary>
    public Player.Player? Target { get; set; }

    public float Alpha => _alpha;

    public bool IsCovering { get; private set; }

    /// <summary>Retângulo do sprite no mundo (u).</summary>
    public MapRect Bounds => _bounds;

    /// <summary>Papel da arte de estados (<see cref="PropArtRole.None"/> sem arte nova).</summary>
    public PropArtRole ArtRole { get; private set; }

    /// <summary>Estado desenhado agora (<c>on</c>, <c>off</c>, <c>open</c>, <c>flowering</c>…; "" sem arte).</summary>
    public string ArtState => _art is null ? string.Empty : _artState.ToString();

    /// <summary>Halo que o lampião/braseiro segue (o motor liga e a cidade acende ao escurecer).</summary>
    public LightGlows? Lights { get; set; }

    public int LightIndex { get; set; } = -1;

    /// <summary>Lampião/braseiro aceso agora (pelo halo; sem halo, sempre aceso).</summary>
    public bool IsLit => Lights is null || LightIndex < 0 || Lights.IsLit(LightIndex);

    /// <summary>Alavanca puxada / porta aberta.</summary>
    public bool Toggled { get; private set; }

    public void Setup(DressingDef def, LayoutObject placed, float coveredAlpha, float fadeRate, bool sway)
    {
        Def = def;
        Name = $"Object_{def.Id}_{(int)placed.X}_{(int)placed.Y}";
        Position = new Vector2(placed.X, placed.Y);
        _coveredAlpha = coveredAlpha;
        _fadeRate = fadeRate;
        _occluder = def.Occluder;

        // Camada da biblioteca B5: piso por baixo de quem anda, pendurado por cima; só objetos de chão têm sombra.
        ZIndex = def.Layer switch { ObjectLayer.Floor => -1, ObjectLayer.Overhead or ObjectLayer.Fx => 1, _ => 0 };
        if (def.Layer == ObjectLayer.Entity)
        {
            AddChild(new AtlasSprite { Key = "shadow", ScaleClass = SpriteScaleClass.Prop, Modulate = new Color(1, 1, 1, 0.35f), Position = new Vector2(0, 1) });
        }

        Sprite = ObjectVisuals.Create(def, placed.Flip, out var local);
        AddChild(Sprite);
        var (artPath, role) = PropArt.ForCatalogSprite(def.Sprite);
        if (artPath is not null && role != PropArtRole.Breakable && ArtFrames.TryLoad(artPath, out var art))
        {
            _art = art;
            ArtRole = role;
            _artState = role switch
            {
                PropArtRole.Lamp or PropArtRole.Brazier => ArtOn,
                PropArtRole.Flower => ArtFlowering,
                PropArtRole.Door => ArtClosed,
                PropArtRole.Board => ArtBoard,
                _ => ArtOff,
            };
            ApplyArt(0);
        }
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

        SetProcess(_occluder || ArtRole is PropArtRole.Lamp or PropArtRole.Brazier);
    }

    /// <summary>Estado da flor (o motor chama no tique de 0,25 s).</summary>
    public void SetFlowerStage(FlowerStage stage)
    {
        if (ArtRole != PropArtRole.Flower)
        {
            return;
        }

        var next = stage switch { FlowerStage.Harvested => ArtHarvested, FlowerStage.Sprouting => ArtSprouting, _ => ArtFlowering };
        if (next != _artState)
        {
            _artState = next;
            ApplyArt(0);
        }
    }

    /// <summary>Alavanca: puxa/solta. Porta: abre/fecha. Devolve o estado novo.</summary>
    public bool Toggle()
    {
        if (ArtRole is not (PropArtRole.Lever or PropArtRole.Door))
        {
            return false;
        }

        Toggled = !Toggled;
        _artState = ArtRole == PropArtRole.Door ? (Toggled ? ArtOpen : ArtClosed) : (Toggled ? ArtOn : ArtOff);
        ApplyArt(0);
        return Toggled;
    }

    private void ApplyArt(int frame)
    {
        if (_art is null)
        {
            return;
        }

        _artFrame = frame;
        _art.Apply(Sprite, _artState, frame);
    }

    public override void _Process(double delta)
    {
        if (ArtRole is PropArtRole.Lamp or PropArtRole.Brazier && _art is not null)
        {
            // chama em 4 quadros (8 fps) enquanto o halo está aceso; apagado, o quadro "off"
            var lit = IsLit;
            var state = lit ? ArtOn : ArtOff;
            _artTime += (float)delta;
            var frame = lit ? FrameClock.Loop(_artTime * _art.Fps(ArtOn) % _art.Count(ArtOn), _art.Count(ArtOn)) : 0;
            if (state != _artState || frame != _artFrame)
            {
                _artState = state;
                ApplyArt(frame);
            }

            if (!_occluder)
            {
                return;
            }
        }

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
