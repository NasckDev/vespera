using Godot;
using Vespera.Core.Advancement;
using Vespera.Game.World;

namespace Vespera.Game.Combat.Echo;

/// <summary>Muda do Eco da Semente: fica plantada pela duração do Eco e atira espinhos no monstro mais perto.</summary>
public partial class EchoSeed : Node2D
{
    private readonly LevelWorld _world;
    private readonly EchoDef _def;
    private readonly double _attack;
    private readonly float _scale;
    private float _age;
    private float _shot;

    public EchoSeed(LevelWorld world, Vector2 at, EchoDef def, double attack, float scale)
    {
        _world = world;
        _def = def;
        _attack = attack;
        _scale = scale;
        Position = _world.ClampToFloor(at);
        Name = "EcoSemente";
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        if (_age >= _def.Duration)
        {
            QueueFree();
            return;
        }

        _shot -= (float)delta;
        if (_shot <= 0f)
        {
            Fire();
        }

        QueueRedraw();
    }

    private void Fire()
    {
        var from = GlobalPosition - new Vector2(0, 46f * _scale);
        var range = (float)_def.Radius * _scale;
        var target = _world.Combat.Nearest(from, Team.Hunters, range);
        if (target is null)
        {
            _shot = 0.15f;
            return;
        }

        _shot = (float)EchoRules.SeedInterval;
        var dir = (target.HitCenter - from).Normalized();
        var hit = new HitInfo(EchoRules.Damage(_def, _attack), from, dir * 220f * _scale, 0.03, 1, false, 0);
        _world.AddFx(new EchoBolt(_world, from, dir, range + 80f, hit, _scale));
    }

    public override void _Draw()
    {
        var s = _scale;
        var grow = Mathf.Min(1f, _age / 0.35f);
        var fade = Mathf.Clamp(((float)_def.Duration - _age) / 1f, 0f, 1f);
        var sway = Mathf.Sin(_age * 3f) * 3f * s;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 30f * s * grow, new Color(0.35f, 0.2f, 0.45f, 0.45f * fade));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        var top = new Vector2(sway, -48f * s * grow);
        DrawLine(Vector2.Zero, top, new Color(0.38f, 0.26f, 0.2f, fade), 5f * s, true);
        DrawCircle(top, 15f * s * grow, new Color(0.62f, 0.3f, 0.78f, fade));
        DrawCircle(top + new Vector2(-4f * s, -4f * s), 5f * s * grow, new Color(0.95f, 0.8f, 1f, 0.8f * fade));
        for (var i = 0; i < 3; i++)
        {
            var a = (-Mathf.Pi / 2f) + ((i - 1) * 0.9f);
            DrawLine(top, top + (Vector2.FromAngle(a) * 26f * s * grow), new Color(0.9f, 0.75f, 1f, fade), 2.5f * s, true);
        }
    }
}
