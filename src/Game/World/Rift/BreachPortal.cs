using Godot;
using Vespera.Game.Combat;
using Vespera.Game.Components;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Brecha em pé (portal magenta, GDD §1): um rasgo oval no ar com redemoinho animado por shader, borda quente que
/// tremula, brilho no chão, luz magenta e partículas subindo. Abre com animação. A interação fica numa
/// <see cref="Interactable"/> no pé (a fase trata o que fazer).
/// </summary>
public partial class BreachPortal : Node2D
{
    private const string ShaderCode = """
        shader_type canvas_item;
        uniform float open_amount = 1.0;
        uniform float seed = 0.0;
        uniform vec4 core_color : source_color = vec4(0.09, 0.04, 0.16, 1.0);
        uniform vec4 rim_color : source_color = vec4(0.90, 0.29, 0.56, 1.0);
        uniform vec4 hot_color : source_color = vec4(1.0, 0.80, 0.93, 1.0);
        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float noise(vec2 p) {
            vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
        }
        float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int k = 0; k < 5; k++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
        void fragment() {
            vec2 uv = UV * 2.0 - 1.0;
            float t = TIME + seed;
            float r = length(uv);
            float ang = atan(uv.y, uv.x);
            float edge = (0.70 + 0.10 * fbm(vec2(ang * 2.5 + seed, t * 1.3))) * open_amount;
            float swirl = fbm(vec2(r * 3.2 - t * 1.4, ang * 1.6 + r * 5.0 - t * 0.9));
            float inside = smoothstep(edge, edge - 0.05, r);
            float rim = smoothstep(edge + 0.12, edge, r) * (1.0 - smoothstep(edge, edge - 0.14, r));
            vec3 col = mix(core_color.rgb, rim_color.rgb * 0.75, clamp(smoothstep(0.1, 0.95, r / max(edge, 0.01)) * swirl * 1.5, 0.0, 1.0));
            col += hot_color.rgb * pow(swirl, 3.0) * 0.55 * inside;
            col = mix(col, hot_color.rgb, clamp(rim * 1.3, 0.0, 1.0));
            float glow = (1.0 - smoothstep(edge, 1.0, r)) * 0.5 * open_amount;
            float a = max(inside, rim);
            COLOR = vec4(col * a + rim_color.rgb * glow * (1.0 - a), max(a, glow * 0.7));
        }
        """;

    private static Shader? _shader;
    private readonly float _height;
    private readonly float _width;
    private ShaderMaterial _material = null!;
    private float _open;

    public BreachPortal(float height, string kind, string prompt, float radius)
    {
        _height = height;
        _width = height * 0.56f;
        Zone = new Interactable(kind, prompt, radius) { Name = "Zone" };
    }

    public Interactable Zone { get; }

    /// <summary>Retângulo sólido da base (para o pé não atravessar o portal).</summary>
    public Rect2 Footprint => new(GlobalPosition.X - (_width * 0.3f), GlobalPosition.Y - (_height * 0.08f), _width * 0.6f, _height * 0.08f);

    public static void ClearCache() => _shader = null;

    public override void _Ready()
    {
        _shader ??= new Shader { Code = ShaderCode };
        _material = new ShaderMaterial { Shader = _shader };
        _material.SetShaderParameter("seed", GD.Randf() * 50f);
        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        var tear = new Sprite2D
        {
            Name = "Tear",
            Texture = ImageTexture.CreateFromImage(image),
            Scale = new Vector2(_width / 8f, _height / 8f),
            Position = new Vector2(0, -_height * 0.55f),
            Material = _material,
        };
        AddChild(new GroundGlow(_width * 0.9f) { Name = "Glow" });
        AddChild(tear);
        AddChild(new PointLight2D
        {
            Texture = LightTexture.Get(),
            Color = new Color("e64a8e"),
            Energy = 1.3f,
            TextureScale = _height * 2.4f / (2 * LightTexture.Radius),
            Position = new Vector2(0, -_height * 0.5f),
        });
        AddChild(Motes());
        var body = new StaticBody2D { Name = "Base", CollisionLayer = Actors.Player.WorldLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(_width * 0.6f, _height * 0.08f) },
            Position = new Vector2(0, -_height * 0.04f),
        });
        AddChild(body);
        Zone.Position = Vector2.Zero;
        Zone.PromptAnchor = GlobalPosition - new Vector2(0, _height * 1.15f);
        AddChild(Zone);
        _material.SetShaderParameter("open_amount", 0f);
    }

    public override void _Process(double delta)
    {
        _open = Mathf.MoveToward(_open, 1f, (float)delta * 1.6f);
        var eased = 1f - Mathf.Pow(1f - _open, 3f);
        _material.SetShaderParameter("open_amount", eased);
        Zone.PromptAnchor = GlobalPosition - new Vector2(0, _height * 1.15f);
    }

    private CpuParticles2D Motes() => new()
    {
        Name = "Motes",
        Amount = 18,
        Lifetime = 1.6f,
        Texture = CombatFeel.Dot(),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(_width * 0.4f, _height * 0.3f),
        Position = new Vector2(0, -_height * 0.5f),
        Direction = Vector2.Up,
        Spread = 25f,
        Gravity = new Vector2(0, -20f),
        InitialVelocityMin = 8f,
        InitialVelocityMax = 26f,
        ScaleAmountMin = 0.05f,
        ScaleAmountMax = 0.14f,
        Color = new Color(1f, 0.55f, 0.8f, 0.8f),
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
    };

    /// <summary>Mancha de luz magenta no chão sob a fenda.</summary>
    private partial class GroundGlow(float radius) : Node2D
    {
        private float _t;

        public override void _Ready() => ZIndex = -3;

        public override void _Process(double delta)
        {
            _t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var beat = 0.85f + (0.15f * Mathf.Sin(_t * 3f));
            DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.35f));
            DrawCircle(Vector2.Zero, radius * 1.3f * beat, new Color(0.9f, 0.29f, 0.56f, 0.12f));
            DrawCircle(Vector2.Zero, radius * 0.8f * beat, new Color(0.9f, 0.29f, 0.56f, 0.22f));
            DrawCircle(Vector2.Zero, radius * 0.45f, new Color(1f, 0.7f, 0.88f, 0.25f));
        }
    }
}
