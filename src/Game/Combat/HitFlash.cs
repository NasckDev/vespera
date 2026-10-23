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
        uniform float outline = 0.0;
        uniform vec4 outline_color : source_color = vec4(1.0);
        uniform float outline_width = 2.0;
        uniform float cape = 0.0;
        uniform float cape_hue = 0.62;
        vec3 to_hsv(vec3 c) {
            vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
            vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
        }
        vec3 to_rgb(vec3 c) {
            vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
            return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
        }
        void fragment() {
            vec4 c = texture(TEXTURE, UV) * COLOR;
            if (cape > 0.0) {
                vec3 hsv = to_hsv(c.rgb);
                float red = max(smoothstep(0.90, 0.95, hsv.x), 1.0 - smoothstep(0.015, 0.04, hsv.x));
                float w = red * smoothstep(0.42, 0.58, hsv.y) * smoothstep(0.10, 0.22, hsv.z) * cape;
                c.rgb = mix(c.rgb, to_rgb(vec3(cape_hue, hsv.y * 0.85, min(1.0, hsv.z * 1.1))), w);
            }
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
                float outer = max(smoothstep(0.04, 0.35, near), 0.65 * smoothstep(0.04, 0.35, far));
                float edge = clamp(outer - c.a, 0.0, 1.0) * glow;
                rgb = mix(rgb, glow_color.rgb, 0.22 * glow * c.a);
                COLOR = vec4(mix(glow_color.rgb, rgb, c.a), max(c.a, edge * COLOR.a));
            } else if (outline > 0.0) {
                vec2 px = TEXTURE_PIXEL_SIZE * outline_width;
                float near = 0.0;
                for (int i = 0; i < 8; i++) {
                    float a = 6.2831853 * float(i) / 8.0;
                    near = max(near, texture(TEXTURE, UV + vec2(cos(a), sin(a)) * px).a);
                }
                float edge = clamp(smoothstep(0.1, 0.5, near) - c.a, 0.0, 1.0) * outline;
                COLOR = vec4(mix(outline_color.rgb, rgb, c.a), max(c.a, edge * COLOR.a));
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

    public Color TintColor { get; set; } = new(1f, 0.72f, 0.3f);

    /// <summary>Espessura do contorno do aviso em texels da folha (o monstro ajusta pela escala do sprite).</summary>
    public float GlowWidth { get; init; } = 4f;

    /// <summary>Contorno fixo (cor do jogador no co-op); 0 desliga. O aviso âmbar dos monstros tem prioridade sobre ele.</summary>
    public float Outline { get; set; }

    public Color OutlineColor { get; set; } = Colors.White;

    /// <summary>Espessura do contorno fixo em texels da folha.</summary>
    public float OutlineWidth { get; set; } = 2.5f;

    /// <summary>Capa recolorida (0 desliga, 1 cheio) para o matiz <see cref="CapeHue"/> (0..1): mesmo personagem, outro jogador.</summary>
    public float Cape { get; set; }

    public float CapeHue { get; set; } = 0.62f;

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
        _material.SetShaderParameter("glow_width", GlowWidth);
        _material.SetShaderParameter("outline", Outline);
        _material.SetShaderParameter("outline_color", OutlineColor);
        _material.SetShaderParameter("outline_width", OutlineWidth);
        _material.SetShaderParameter("cape", Cape);
        _material.SetShaderParameter("cape_hue", CapeHue);
    }
}
