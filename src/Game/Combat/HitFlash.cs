using Godot;

namespace Vespera.Game.Combat;

/// <summary>
/// Flash de impacto num sprite (material próprio com um shader de "pintar de cor"): branco por 1-2 quadros ao levar
/// golpe; âmbar no aviso dos monstros. Com "reduzir flashes" a intensidade cai para um tom suave.
/// </summary>
public sealed class HitFlash
{
    private const string Code = """
        shader_type canvas_item;
        uniform float flash : hint_range(0.0, 1.0) = 0.0;
        uniform vec4 flash_color : source_color = vec4(1.0);
        void fragment() {
            vec4 c = texture(TEXTURE, UV) * COLOR;
            COLOR = vec4(mix(c.rgb, flash_color.rgb, flash * c.a), c.a);
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

    /// <summary>Cor fixa por cima (aviso âmbar segurado), 0 desliga.</summary>
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

        var tint = CombatFeel.ReduceFlashes ? Mathf.Min(Tint, 0.35f) : Tint;
        if (_amount >= tint)
        {
            _material.SetShaderParameter("flash", _amount);
            _material.SetShaderParameter("flash_color", _color);
            return;
        }

        _material.SetShaderParameter("flash", tint);
        _material.SetShaderParameter("flash_color", TintColor);
    }
}
