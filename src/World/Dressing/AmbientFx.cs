using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Efeitos sem interação do layout (<c>fx</c>, DECISIONS D44): fumaça de chaminé (sempre) e vagalumes (só à noite,
/// 19h–5h na cidade; em dungeons sempre). Partículas de CPU com poucos pontos; desligável em <see cref="WorldFx.Particles"/>.
/// </summary>
public partial class AmbientFx : Node2D
{
    private readonly List<CpuParticles2D> _fireflies = [];
    private double _check;
    private bool _dungeon;

    public int SmokeCount { get; private set; }

    public int FireflyCount => _fireflies.Count;

    /// <summary>Vagalumes acesos agora (noite).</summary>
    public bool FirefliesOn { get; private set; }

    public void Build(MapDef map, MapLayout layout)
    {
        Clear();
        _dungeon = map.Dungeon;
        if (!WorldFx.Particles)
        {
            return;
        }

        foreach (var fx in layout.Fx)
        {
            switch (fx.Kind)
            {
                case "smoke":
                    AddChild(CreateSmoke(new Vector2(fx.X, fx.Y), fx.Radius));
                    SmokeCount++;
                    break;
                case "fireflies":
                    var f = CreateFireflies(new Vector2(fx.X, fx.Y), fx.Radius);
                    AddChild(f);
                    _fireflies.Add(f);
                    break;
                case "embers":
                    AddChild(CreateEmbers(new Vector2(fx.X, fx.Y), fx.Radius));
                    break;
                default:
                    Log.Warning($"{map.Id}: fx '{fx.Kind}' desconhecido.");
                    break;
            }
        }

        UpdateNight();
    }

    public void Clear()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _fireflies.Clear();
        SmokeCount = 0;
        FirefliesOn = false;
    }

    public override void _Process(double delta)
    {
        _check -= delta;
        if (_check <= 0 && _fireflies.Count > 0)
        {
            _check = 1.0;
            UpdateNight();
        }
    }

    private void UpdateNight()
    {
        var night = _dungeon;
        if (!night && GameManager.Instance is { } gm)
        {
            try
            {
                var h = gm.State.Save.ClockMinutes / 60.0;
                night = h >= 19 || h % 24 < 5 || h >= 24;
            }
            catch (InvalidOperationException)
            {
                night = false;
            }
        }

        FirefliesOn = night;
        foreach (var f in _fireflies)
        {
            f.Emitting = night;
        }
    }

    private static CpuParticles2D CreateSmoke(Vector2 at, float spread)
    {
        var ramp = new Gradient
        {
            Offsets = [0f, 0.15f, 1f],
            Colors = [new Color(0.55f, 0.52f, 0.58f, 0f), new Color(0.6f, 0.57f, 0.62f, 0.38f), new Color(0.7f, 0.68f, 0.74f, 0f)],
        };
        var grow = new Curve();
        grow.AddPoint(new Vector2(0, 0.5f));
        grow.AddPoint(new Vector2(1, 1.8f));
        return new CpuParticles2D
        {
            Name = "Smoke",
            Position = at,
            Amount = 10,
            Lifetime = 3.2,
            Preprocess = 3.2,
            LocalCoords = false,
            Direction = new Vector2(0.25f, -1),
            Spread = 12f,
            Gravity = new Vector2(3, -4),
            InitialVelocityMin = 7f,
            InitialVelocityMax = 11f,
            DampingMin = 0.5f,
            DampingMax = 1.5f,
            ScaleAmountMin = 3f,
            ScaleAmountMax = 5f,
            ScaleAmountCurve = grow,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(MathF.Max(1f, spread * 0.5f), 1),
        };
    }

    private static CpuParticles2D CreateFireflies(Vector2 at, float radius)
    {
        var ramp = new Gradient
        {
            Offsets = [0f, 0.25f, 0.5f, 0.75f, 1f],
            Colors =
            [
                new Color(0.85f, 1f, 0.45f, 0f), new Color(0.9f, 1f, 0.5f, 0.95f), new Color(0.9f, 1f, 0.5f, 0.25f),
                new Color(0.95f, 1f, 0.55f, 0.9f), new Color(0.85f, 1f, 0.45f, 0f),
            ],
        };
        return new CpuParticles2D
        {
            Name = "Fireflies",
            Position = at,
            Amount = Math.Clamp((int)(radius / 8f), 6, 16),
            Lifetime = 4.5,
            Preprocess = 4.5,
            Emitting = false,
            LocalCoords = false,
            Direction = new Vector2(0, -1),
            Spread = 180f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 8f,
            OrbitVelocityMin = -0.06f,
            OrbitVelocityMax = 0.06f,
            ScaleAmountMin = 1f,
            ScaleAmountMax = 1.6f,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = radius,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }

    private static CpuParticles2D CreateEmbers(Vector2 at, float radius)
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 0.75f, 0.3f, 0.9f));
        ramp.SetColor(1, new Color(1f, 0.4f, 0.1f, 0f));
        return new CpuParticles2D
        {
            Name = "Embers",
            Position = at,
            Amount = 6,
            Lifetime = 1.4,
            LocalCoords = false,
            Direction = new Vector2(0, -1),
            Spread = 30f,
            Gravity = new Vector2(0, -10),
            InitialVelocityMin = 5f,
            InitialVelocityMax = 14f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.3f,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(MathF.Max(1f, radius * 0.5f), 1),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }
}
