using System.Collections.Generic;
using Godot;
using Vespera.Autoload;

namespace Vespera.World;

/// <summary>Escala global da arte (DECISIONS D41): personagem (<c>characterScale</c>), objeto (<c>propScale</c>) ou nenhuma.</summary>
public enum ArtScaleClass
{
    Character,
    Prop,
    Fixed,
}

/// <summary>
/// SpriteFrames da arte aprovada (gerado por <c>tools/sprites/build_art.py</c>, DECISIONS C2-D1) com os metadados que
/// põem cada animação no tamanho e no pé certos: <c>anchors</c> (pé em px do PNG), <c>densities</c> (px por u a escala 1)
/// e <c>scale_class</c>. Carregado uma vez por caminho e guardado em cache (nada é alocado por quadro).
/// </summary>
public sealed class ArtFrames
{
    private static readonly Dictionary<string, ArtFrames?> Cache = [];

    private readonly Dictionary<StringName, Vector2> _anchors = [];
    private readonly Dictionary<StringName, float> _densities = [];

    private ArtFrames(string path, SpriteFrames frames)
    {
        Path = path;
        Frames = frames;
        if (frames.GetMeta("anchors", new Godot.Collections.Dictionary()).AsGodotDictionary() is { } anchors)
        {
            foreach (var (k, v) in anchors)
            {
                _anchors[new StringName(k.AsString())] = v.AsVector2();
            }
        }

        if (frames.GetMeta("densities", new Godot.Collections.Dictionary()).AsGodotDictionary() is { } densities)
        {
            foreach (var (k, v) in densities)
            {
                _densities[new StringName(k.AsString())] = Mathf.Max(0.01f, (float)v.AsDouble());
            }
        }

        ScaleClass = frames.GetMeta("scale_class", "fixed").AsString() switch
        {
            "character" => ArtScaleClass.Character,
            "prop" => ArtScaleClass.Prop,
            _ => ArtScaleClass.Fixed,
        };
    }

    public string Path { get; }

    public SpriteFrames Frames { get; }

    public ArtScaleClass ScaleClass { get; }

    /// <summary>Escala global atual da classe (lida do <c>rules.json</c>).</summary>
    public float GlobalScale => ScaleClass switch
    {
        ArtScaleClass.Character => WorldScale.Character,
        ArtScaleClass.Prop => WorldScale.Prop,
        _ => 1f,
    };

    /// <summary>A arte existe no projeto (sem aviso no log).</summary>
    public static bool Exists(string path) => ResourceLoader.Exists(path);

    /// <summary>Carrega (ou devolve do cache) o SpriteFrames em <paramref name="path"/>; falso se não existir.</summary>
    public static bool TryLoad(string path, out ArtFrames frames)
    {
        if (!Cache.TryGetValue(path, out var cached))
        {
            cached = null;
            if (ResourceLoader.Exists(path) && GD.Load<SpriteFrames>(path) is { } sf)
            {
                cached = new ArtFrames(path, sf);
            }
            else
            {
                Log.Warning($"Arte '{path}' não encontrada; fica o atlas.");
            }

            Cache[path] = cached;
        }

        frames = cached!;
        return cached is not null;
    }

    public bool Has(StringName animation) => Frames.HasAnimation(animation);

    public int Count(StringName animation) => Frames.HasAnimation(animation) ? Frames.GetFrameCount(animation) : 0;

    public float Fps(StringName animation) => Frames.HasAnimation(animation) ? (float)Frames.GetAnimationSpeed(animation) : 0f;

    public Vector2 Anchor(StringName animation) => _anchors.TryGetValue(animation, out var a) ? a : Vector2.Zero;

    public float Density(StringName animation) => _densities.TryGetValue(animation, out var d) ? d : 1f;

    /// <summary>u por px da animação (escala global / densidade).</summary>
    public float UnitsPerPixel(StringName animation) => GlobalScale / Density(animation);

    public Texture2D? Texture(StringName animation, int frame)
    {
        var n = Count(animation);
        return n == 0 ? null : Frames.GetFrameTexture(animation, Mathf.Clamp(frame, 0, n - 1));
    }

    /// <summary>Altura em u do quadro (tela inteira do PNG, pé até o topo).</summary>
    public float TopHeight(StringName animation) => Anchor(animation).Y * UnitsPerPixel(animation);

    /// <summary>
    /// Mostra o quadro num <see cref="Sprite2D"/> comum (objetos com shader de balanço/transparência): textura, pé e escala.
    /// <paramref name="extraScale"/> multiplica (o <c>o.s</c> da v2); o sinal de <c>scale.x</c> do sprite (espelho) é mantido.
    /// </summary>
    public void Apply(Sprite2D sprite, StringName animation, int frame, float extraScale = 1f)
    {
        var texture = Texture(animation, frame);
        if (sprite.Texture != texture)
        {
            sprite.Texture = texture;
        }

        sprite.Centered = false;
        sprite.Offset = -Anchor(animation);
        var k = extraScale * UnitsPerPixel(animation);
        sprite.Scale = new Vector2(sprite.Scale.X < 0 ? -k : k, k);
    }
}

/// <summary>
/// <see cref="AnimatedSprite2D"/> com a arte aprovada (DECISIONS C2-D2): quem o usa escolhe animação e quadro a cada
/// quadro (<see cref="Show"/>), sem <c>Play()</c>, para o passo seguir a velocidade real do corpo e a esquiva durar o
/// mesmo que o dash do combate. Pé e escala trocam junto com a animação (cada vista tem a sua densidade).
/// </summary>
public partial class ArtSprite : AnimatedSprite2D
{
    private StringName _shown = new(string.Empty);
    private float _extraScale = 1f;

    public ArtFrames? Art { get; private set; }

    /// <summary>Escala extra (o <c>scale</c> do monstro nos dados, por exemplo).</summary>
    public float ExtraScale
    {
        get => _extraScale;
        set
        {
            _extraScale = value;
            ApplyAnchor(_shown);
        }
    }

    /// <summary>Animação mostrada agora ("" antes do primeiro <see cref="Show"/>).</summary>
    public StringName Shown => _shown;

    public void Setup(ArtFrames art)
    {
        Art = art;
        SpriteFrames = art.Frames;
        Centered = false;
        _shown = new StringName(string.Empty);
    }

    /// <summary>Sem arte (classe sem quadros novos): esconde e solta o SpriteFrames.</summary>
    public void Clear()
    {
        Art = null;
        SpriteFrames = null;
        Visible = false;
        _shown = new StringName(string.Empty);
    }

    /// <summary>Mostra <paramref name="frame"/> de <paramref name="animation"/> (troca pé/escala se a animação mudou).</summary>
    public void Show(StringName animation, int frame)
    {
        if (Art is null)
        {
            return;
        }

        if (animation != _shown)
        {
            _shown = animation;
            Animation = animation;
            ApplyAnchor(animation);
        }

        if (Frame != frame)
        {
            Frame = frame;
        }
    }

    /// <summary>Textura do quadro mostrado (para o rastro da esquiva).</summary>
    public Texture2D? CurrentTexture => Art?.Texture(_shown, Frame);

    private void ApplyAnchor(StringName animation)
    {
        if (Art is null || animation.IsEmpty)
        {
            return;
        }

        Offset = -Art.Anchor(animation);
        Scale = Vector2.One * (_extraScale * Art.UnitsPerPixel(animation));
    }
}
