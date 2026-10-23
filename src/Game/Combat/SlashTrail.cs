using System.Collections.Generic;
using Godot;

namespace Vespera.Game.Combat;

/// <summary>
/// Rastro do corte: uma meia-lua que varre o setor do golpe e some em ~0,2 s, na cor e na largura do nível da arma
/// (<see cref="WeaponLook"/>). Desenhada por cima das entidades, com mistura aditiva.
/// </summary>
public partial class SlashTrail : Node2D
{
    private const float Seconds = 0.2f;
    private const int Steps = 18;

    private readonly float _radius;
    private readonly float _arc;
    private readonly float _angle;
    private readonly float _thickness;
    private readonly WeaponTier _tier;
    private float _age;

    public SlashTrail(Vector2 origin, Vector2 dir, float radius, float arc, float thickness, WeaponTier tier)
    {
        Position = origin;
        _radius = radius;
        _arc = Mathf.Clamp(arc, 0.6f, 2.9f);
        _angle = dir.Angle();
        _thickness = thickness * tier.TrailScale;
        _tier = tier;
        Name = "Slash";
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Seconds)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = _age / Seconds;
        var sweep = Mathf.Min(1f, t * 2.4f);
        var alpha = 1f - (t * t);
        var start = _angle - (_arc / 2f);
        var end = start + (_arc * sweep);
        Band(start, end, _radius, _thickness, _tier.Trail with { A = 0.85f * alpha });
        Band(start, end, _radius * 0.98f, _thickness * 0.38f, _tier.Core with { A = alpha });
    }

    /// <summary>Faixa curva entre os ângulos, mais grossa no meio e fina nas pontas.</summary>
    private void Band(float from, float to, float radius, float thickness, Color color)
    {
        if (to - from < 0.02f)
        {
            return;
        }

        var outer = new List<Vector2>(Steps + 1);
        var inner = new List<Vector2>(Steps + 1);
        for (var i = 0; i <= Steps; i++)
        {
            var k = i / (float)Steps;
            var a = Mathf.Lerp(from, to, k);
            var w = thickness * Mathf.Sin(Mathf.Pi * Mathf.Clamp((k * 0.85f) + 0.12f, 0f, 1f));
            var d = Vector2.FromAngle(a);
            outer.Add(d * radius);
            inner.Add(d * (radius - w));
        }

        inner.Reverse();
        outer.AddRange(inner);
        DrawColoredPolygon(outer.ToArray(), color);
    }
}
