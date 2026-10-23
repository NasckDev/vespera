using Godot;

namespace Vespera.Game.Combat;

/// <summary>
/// Flash de impacto num sprite (material próprio): branco por 1-2 quadros ao levar golpe (pinta o corpo); o aviso âmbar
/// dos monstros vira CONTORNO brilhante em volta da silhueta, com um calor leve por dentro, para o monstro continuar
/// legível (antes o âmbar pintava o corpo inteiro e apagava o desenho). Com "reduzir flashes" o flash cai para um tom suave.
/// </summary>
public sealed class HitFlash
{
    private const string Code = """
        shader_type canvas_item;
        uniform float flash : hint_range(0.0, 1.0) = 0.0;
        uniform vec4 flash_color : source_color = vec4(1.0);
        uniform float glow : hint_range(0.0, 1.0) = 0.0;
        uniform vec4 glow_color : source_color = vec4(1.0, 0.65, 0.25, 1.0);
        uniform float glow_width = 4.0;
        void fragment() {
            vec4 c = texture(TEXTURE, UV) * COLOR;
            vec3 rgb = mix(c.rgb, flash_color.rgb, flash * c.a);
            if (glow > 0.0) {
                vec2 px = TEXTURE_PIXEL_SIZE * glow_width;
                float near = 0.0;
                float far = 0.0;
                for (int i = 0; i < 12; i++) {
                    float a = 6.2831853 * float(i) / 12.0;
                    vec2 d = vec2(cos(a), sin(a)) * px;
                    near = max(near, texture(TEXTURE, UV + d).a);
                    far = max(far, texture(TEXTURE, UV + d * 1.8).a);
                }
                float outer = max(near, 0.6 * far);
                float edge = clamp(outer - c.a, 0.0, 1.0) * glow;
                rgb = mix(rgb, glow_color.rgb, 0.18 * glow * c.a);
                COLOR = vec4(mix(glow_color.rgb, rgb, c.a), max(c.a, edge * COLOR.a));
            } else {
                COLOR = vec4(rgb, c.a);
            }
        }
        """;

    private const float ReducedStrength = 0.3f;

    private static Shader? _shader;
    private readonly ShaderMaterial _material;
    private float _amount;
    private float _hold;
    private float _decay;
    private Color _color = Colors.White;

    public HitFlash(CanvasItem target)
    {
        _shader ??= new Shader { Code = Code };
        _material = new ShaderMaterial { Shader = _shader };
        target.Material = _material;
    }

    /// <summary>Aviso âmbar segurado (0 desliga): intensidade do contorno brilhante; 0,3+ já é o contorno cheio.</summary>
    public float Tint { get; set; }

    public Color TintColor { get; set; } = new(1f, 0.65f, 0.25f);

    public static void ClearCache() => _shader = null;

    /// <summary>Flash que segura <paramref name="hold"/> s e some em <paramref name="fade"/> s.</summary>
    public void Pulse(Color color, float hold = 0.05f, float fade = 0.1f, float strength = 1f)
    {
        var s = CombatFeel.ReduceFlashes ? Mathf.Min(strength, ReducedStrength) : strength;
        _color = color;
        _amount = Mathf.Max(_amount, s);
        _hold = hold;
        _decay = fade <= 0 ? 999f : s / fade;
    }

    public void Update(float dt)
    {
        if (_hold > 0f)
        {
            _hold -= dt;
        }
        else
        {
            _amount = Mathf.Max(0f, _amount - (_decay * dt));
        }

        _material.SetShaderParameter("flash", _amount);
        _material.SetShaderParameter("flash_color", _color);
        _material.SetShaderParameter("glow", Mathf.Clamp(Tint * 3f, 0f, 1f));
        _material.SetShaderParameter("glow_color", TintColor);
    }
}
