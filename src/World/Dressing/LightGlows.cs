using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Halos dos lampiões e braseiros (DECISIONS D44): para cada luz do mapa (<c>lights.json</c> nos mapas hd, raio 46 âmbar
/// como a v2; <c>maps.json</c> nos demais), um halo grande e suave que pulsa devagar e um núcleo pequeno que tremula
/// rápido como chama, em blend aditivo (os "glows" da v2, <c>render.js:62-83</c>: raio 0,7·r, alpha 0,14, magenta 0,22).
/// Luzes de fogo (<c>f</c>) soltam brasas. A escuridão por hora é da frente Cidade; aqui a força só cresce à noite.
/// Tudo num nó, sem alocação por quadro.
/// </summary>
public partial class LightGlows : Node2D
{
    public const string LightsJsonPath = "res://assets/maps/lights.json";
    private const float HaloScale = 0.7f; // render.js: raio 0,7·r
    private const float CoreScale = 0.2f;

    private static Dictionary<string, MapLight[]>? _lightsJson;
    private static GradientTexture2D? _texture;
    private static CanvasItemMaterial? _additive;

    private readonly List<Glow> _glows = [];
    private float _time;
    private float _night = 1f;
    private double _nightCheck;

    /// <summary>Quantas luzes o mapa atual tem.</summary>
    public int Count => _glows.Count;

    /// <summary>Luzes acesas agora.</summary>
    public int LitCount
    {
        get
        {
            var n = 0;
            foreach (var g in _glows)
            {
                n += g.Lit ? 1 : 0;
            }

            return n;
        }
    }

    public static IReadOnlyList<MapLight> LightsOf(MapDef map)
    {
        if (map.LightsKey is { } key)
        {
            _lightsJson ??= ParseLightsJson();
            if (_lightsJson.TryGetValue(key, out var lights))
            {
                return lights;
            }
        }

        return map.Lights;
    }

    /// <summary>Monta as luzes do mapa (apaga as anteriores).</summary>
    public void Build(MapDef map)
    {
        Clear();
        if (!WorldFx.Lights)
        {
            return;
        }

        _texture ??= CreateTexture();
        _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var lights = LightsOf(map);
        for (var i = 0; i < lights.Count; i++)
        {
            var l = lights[i];
            AddLight(new Vector2(l.X, l.Y), l.Radius, l.Kind);
        }

        UpdateNight();
        Animate(0f);
    }

    /// <summary>Acrescenta uma luz (objeto do catálogo, lampião sem luz no mapa). Devolve o índice. Não faz nada com as luzes desligadas.</summary>
    public int AddLight(Vector2 at, float radius, string kind)
    {
        if (!WorldFx.Lights)
        {
            return -1;
        }

        _texture ??= CreateTexture();
        _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var i = _glows.Count;
        var color = ColorOf(kind);
        var magenta = kind == "m";
        var halo = new Sprite2D
        {
            Texture = _texture,
            Material = _additive,
            Position = at,
            Modulate = color with { A = magenta ? 0.22f : 0.16f },
            TextureFilter = TextureFilterEnum.Linear,
        };
        var core = new Sprite2D
        {
            Texture = _texture,
            Material = _additive,
            Position = at,
            Modulate = color.Lightened(0.35f) with { A = 0.55f },
            TextureFilter = TextureFilterEnum.Linear,
        };
        AddChild(halo);
        AddChild(core);
        CpuParticles2D? embers = null;
        if (kind == "f" && WorldFx.Particles)
        {
            embers = CreateEmbers(at, color);
            AddChild(embers);
        }

        _glows.Add(new Glow(halo, core, embers, radius, color, (i * 1.7f) + (at.X * 0.013f)));
        Animate(0f);
        return i;
    }

    public void Clear()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _glows.Clear();
    }

    /// <summary>Índice da luz mais perto de (x, y) até <paramref name="maxDistance"/> u, ou −1.</summary>
    public int NearestLight(Vector2 at, float maxDistance)
    {
        var best = -1;
        var bestD = maxDistance * maxDistance;
        for (var i = 0; i < _glows.Count; i++)
        {
            var d = _glows[i].Halo.Position.DistanceSquaredTo(at);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    public Vector2 PositionOf(int index) => _glows[index].Halo.Position;

    public bool IsLit(int index) => index >= 0 && index < _glows.Count && _glows[index].Lit;

    /// <summary>Acende/apaga uma luz (lampião do layout), com fade.</summary>
    public void SetLit(int index, bool lit)
    {
        if (index >= 0 && index < _glows.Count)
        {
            var g = _glows[index];
            g.Lit = lit;
            if (g.Embers is { } e)
            {
                e.Emitting = lit;
            }
        }
    }

    public override void _Process(double delta)
    {
        var dt = MathF.Min(0.05f, (float)delta);
        _time += dt;
        _nightCheck -= dt;
        if (_nightCheck <= 0)
        {
            _nightCheck = 1.0;
            UpdateNight();
        }

        Animate(dt);
    }

    private void Animate(float dt)
    {
        for (var i = 0; i < _glows.Count; i++)
        {
            var g = _glows[i];
            g.Level = Mathf.MoveToward(g.Level, g.Lit ? 1f : 0f, dt * 4f);
            var t = _time + g.Phase;
            // halo: respira devagar (render.js:57: r·(1 + 0,05·sin(t·(7+i%3) + i)))
            var pulse = 1f + (0.05f * MathF.Sin((t * (7 + (i % 3))) + i));
            var halo = g.Radius * HaloScale * 2f * pulse / 64f;
            g.Halo.Scale = new Vector2(halo, halo * 0.92f);
            g.Halo.Modulate = g.Color with { A = g.HaloAlpha * g.Level * _night };
            // núcleo: chama tremulando (duas frequências + um "estalo" ocasional)
            var flicker = 0.78f + (0.12f * MathF.Sin(t * 11.3f)) + (0.07f * MathF.Sin((t * 23.7f) + 1.3f)) + (0.05f * MathF.Sin((t * 3.1f) + 0.4f));
            var core = g.Radius * CoreScale * 2f * (0.9f + (0.12f * flicker)) / 64f;
            g.Core.Scale = new Vector2(core * 0.85f, core * 1.15f);
            g.Core.Modulate = g.CoreColor with { A = 0.6f * flicker * g.Level * (0.55f + (0.45f * _night)) };
        }
    }

    /// <summary>Mais forte à noite na cidade (19h–5h), mais fraco no fim de tarde; dungeons e interiores sempre fortes.</summary>
    private void UpdateNight()
    {
        if (GameManager.Instance is not { } gm)
        {
            _night = 1f;
            return;
        }

        try
        {
            var hour = gm.State.Save.ClockMinutes / 60.0;
            _night = hour < 17 ? 0.65f : hour < 19 ? (float)(0.65 + ((hour - 17) / 2 * 0.35)) : 1f;
        }
        catch (InvalidOperationException)
        {
            _night = 1f;
        }
    }

    private static Color ColorOf(string kind) => kind switch
    {
        "f" => new Color(255 / 255f, 120 / 255f, 40 / 255f),
        "m" => new Color(230 / 255f, 74 / 255f, 142 / 255f),
        "c" => new Color(95 / 255f, 208 / 255f, 230 / 255f),
        "g" => new Color(110 / 255f, 230 / 255f, 90 / 255f),
        _ => new Color(242 / 255f, 165 / 255f, 65 / 255f), // "a" âmbar (data.js:4 LIGHTC)
    };

    private static GradientTexture2D CreateTexture()
    {
        // makeGlow da v2 (core.js:41-46): alpha 1 no centro, 0,55 a 40% do raio, 0 na borda
        var gradient = new Gradient
        {
            Offsets = [0f, 0.4f, 1f],
            Colors = [new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0)],
        };
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 64,
            Height = 64,
        };
    }

    private static CpuParticles2D CreateEmbers(Vector2 at, Color color)
    {
        var ramp = new Gradient();
        ramp.SetColor(0, color.Lightened(0.4f));
        ramp.SetColor(1, color with { A = 0f });
        return new CpuParticles2D
        {
            Position = at,
            Amount = 4,
            Lifetime = 1.1,
            LocalCoords = false,
            Direction = new Vector2(0, -1),
            Spread = 25f,
            Gravity = new Vector2(0, -12),
            InitialVelocityMin = 6f,
            InitialVelocityMax = 14f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.4f,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(3, 1),
            Material = _additive,
        };
    }

    private static Dictionary<string, MapLight[]> ParseLightsJson()
    {
        var result = new Dictionary<string, MapLight[]>();
        if (!FileAccess.FileExists(LightsJsonPath))
        {
            return result;
        }

        var root = Json.ParseString(FileAccess.GetFileAsString(LightsJsonPath)).AsGodotDictionary();
        foreach (var key in root.Keys)
        {
            var arr = root[key].AsGodotArray();
            var lights = new MapLight[arr.Count];
            for (var i = 0; i < arr.Count; i++)
            {
                var l = arr[i].AsGodotArray();
                // core.js:29 — raio e tipo do JSON são ignorados: sempre 46, âmbar
                lights[i] = new MapLight((float)l[0].AsDouble(), (float)l[1].AsDouble(), 46f, "a");
            }

            result[key.AsString()] = lights;
        }

        return result;
    }

    private sealed class Glow(Sprite2D halo, Sprite2D core, CpuParticles2D? embers, float radius, Color color, float phase)
    {
        public Sprite2D Halo { get; } = halo;

        public Sprite2D Core { get; } = core;

        public CpuParticles2D? Embers { get; } = embers;

        public float Radius { get; } = radius;

        public Color Color { get; } = color;

        public Color CoreColor { get; } = color.Lightened(0.35f);

        public float HaloAlpha { get; } = color.R > 0.85f && color.B > 0.5f ? 0.22f : 0.16f;

        public float Phase { get; } = phase;

        public bool Lit { get; set; } = true;

        public float Level { get; set; } = 1f;
    }
}
