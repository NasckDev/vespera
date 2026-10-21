using System;
using Godot;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Recorte da pintura do mapa (Polygon2D com UV = posição no mapa) no y-sort das entidades com origem na base
/// (<c>baseY</c>, DECISIONS D44): o jogador com o pé acima da base fica atrás e é coberto. Quando o recorte cobre o
/// corpo dele, o recorte vai a ~40% de opacidade com fade suave, então o jogador sempre aparece. <c>sway</c> balança o
/// conteúdo (shader que desloca a amostra, não os vértices: o recorte continua cobrindo a arte de baixo).
/// Filtro Nearest, como o mapa: em escala inteira, recorte e mapa ficam pixel a pixel iguais.
/// </summary>
public partial class Occluder : Polygon2D
{
    private static Shader? _swayShader;

    private MapPoint[] _points = [];
    private MapRect _bounds;
    private float _baseY;
    private float _coveredAlpha = 0.4f;
    private float _fadeRate = 10f;
    private float _alpha = 1f;

    /// <summary>Quem fica translúcido por trás (o jogador).</summary>
    public Player.Player? Target { get; set; }

    public LayoutOccluder Def { get; private set; } = null!;

    /// <summary>Opacidade atual (1 = opaco).</summary>
    public float Alpha => _alpha;

    /// <summary>Cobrindo o alvo neste quadro.</summary>
    public bool IsCovering { get; private set; }

    public void Setup(LayoutOccluder def, Texture2D mapTexture, float coveredAlpha, float fadeRate, bool sway)
    {
        Def = def;
        _baseY = def.BaseY;
        _bounds = def.Bounds;
        _coveredAlpha = coveredAlpha;
        _fadeRate = fadeRate;
        _points = new MapPoint[def.Polygon.Count];
        var local = new Vector2[def.Polygon.Count];
        var uv = new Vector2[def.Polygon.Count];
        for (var i = 0; i < def.Polygon.Count; i++)
        {
            var p = def.Polygon[i];
            _points[i] = p;
            local[i] = new Vector2(p.X, p.Y - _baseY);
            uv[i] = new Vector2(p.X, p.Y);
        }

        Name = $"Occluder_{(int)_bounds.X}_{(int)_bounds.Y}";
        Position = new Vector2(0, _baseY);
        Texture = mapTexture;
        Polygon = local;
        UV = uv;
        TextureFilter = TextureFilterEnum.Nearest;
        Antialiased = false;
        if (sway && def.Sway)
        {
            _swayShader ??= GD.Load<Shader>("res://src/World/Shaders/sway.gdshader");
            var material = new ShaderMaterial { Shader = _swayShader };
            material.SetShaderParameter("height", MathF.Max(1f, _baseY - _bounds.Y));
            material.SetShaderParameter("phase", (_bounds.X * 0.037f) + (_bounds.Y * 0.011f));
            material.SetShaderParameter("amp_px", MathF.Min(2f, 0.6f + (_bounds.Height / 90f)));
            Material = material;
        }
    }

    public override void _Process(double delta)
    {
        var covering = false;
        if (Target is { } t && IsInstanceValid(t))
        {
            var foot = t.VisualPosition;
            var height = t.VisualHeight;
            var body = OcclusionRules.BodyRect(foot.X, foot.Y, height * 0.45f, height);
            covering = OcclusionRules.IsBehind(foot.Y, _baseY) && OcclusionRules.Covers(_points, _bounds, body);
        }

        IsCovering = covering;
        var target = covering ? _coveredAlpha : 1f;
        if (MathF.Abs(_alpha - target) < 0.002f)
        {
            if (_alpha != target)
            {
                _alpha = target;
                Color = new Color(1, 1, 1, _alpha);
            }

            return;
        }

        _alpha = OcclusionRules.Approach(_alpha, target, _fadeRate, MathF.Min(0.05f, (float)delta));
        Color = new Color(1, 1, 1, _alpha);
    }
}
