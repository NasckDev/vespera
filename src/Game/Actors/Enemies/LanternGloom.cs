using System.Collections.Generic;
using Godot;
using Vespera.Game.World;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Escuro da sala com a lanterna do caçador acesa (Atos IV e V): a Traça-de-Arquivo "apaga" (a sala escurece e a luz
/// do caçador encolhe) e a Velória apaga a arena na fase 3 (CAMPANHA Atos IV e V). Quem quer escuro pede a cada quadro
/// (<see cref="Request"/>: escuridão 0–1 e raio da lanterna em alturas humanas); vale o pedido mais escuro do quadro e
/// sem pedido a sala clareia de novo. Um por sala, criado no primeiro pedido.
/// </summary>
public partial class LanternGloom : Node2D
{
    private const string NodeName = "LanternGloom";
    private const float FadeSpeed = 2.5f;
    private static readonly Color Dark = new(0.13f, 0.12f, 0.24f);
    private static GradientTexture2D? _glow;

    private readonly LevelWorld _world;
    private CanvasModulate? _modulate;
    private readonly Dictionary<Player, PointLight2D> _lights = [];
    private float _wanted;
    private float _wantedRadius = 3f;
    private float _darkness;
    private float _radius = 3f;

    private LanternGloom(LevelWorld world)
    {
        _world = world;
        Name = NodeName;
    }

    /// <summary>Escuridão aplicada agora (0 = sala normal).</summary>
    public float Darkness => _darkness;

    /// <summary>Raio atual da luz da lanterna, em alturas humanas.</summary>
    public float LanternRadius => _radius;

    /// <summary>Pede escuro neste quadro: <paramref name="darkness"/> 0–1 e a luz do caçador com <paramref name="radiusHumans"/>.</summary>
    public static void Request(LevelWorld world, float darkness, float radiusHumans)
    {
        var gloom = world.GetNodeOrNull<LanternGloom>(NodeName);
        if (gloom is null)
        {
            gloom = new LanternGloom(world);
            world.AddChild(gloom);
        }

        if (darkness > gloom._wanted)
        {
            gloom._wanted = Mathf.Clamp(darkness, 0f, 1f);
            gloom._wantedRadius = radiusHumans;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _darkness = Mathf.MoveToward(_darkness, _wanted, FadeSpeed * dt);
        _radius = Mathf.MoveToward(_radius, _wanted > 0f ? _wantedRadius : 3f, 4f * dt);
        _wanted = 0f;
        Apply();
    }

    private void Apply()
    {
        if (_darkness <= 0.001f)
        {
            _modulate?.QueueFree();
            _modulate = null;
            ClearLights();
            return;
        }

        if (_modulate is null || !IsInstanceValid(_modulate))
        {
            _modulate = new CanvasModulate { Name = "EscuroDaLanterna" };
            _world.AddChild(_modulate);
        }

        _modulate.Color = Colors.White.Lerp(Dark, _darkness);
        // Cada caçador leva a própria lanterna (no co-op, o J2 também enxerga em volta de si).
        var human = _world.Data.HumanHeight;
        foreach (var player in _world.Players)
        {
            if (!IsInstanceValid(player))
            {
                continue;
            }

            if (!_lights.TryGetValue(player, out var light) || !IsInstanceValid(light))
            {
                light = new PointLight2D { Name = "LuzDaLanterna", Texture = Glow(), Color = new Color("f2a541") };
                player.AddChild(light);
                _lights[player] = light;
            }

            light.Position = new Vector2(0, -human * 0.5f);
            light.Energy = 0.9f * _darkness;
            light.TextureScale = _radius * human / 128f;
        }
    }

    private void ClearLights()
    {
        foreach (var light in _lights.Values)
        {
            if (IsInstanceValid(light))
            {
                light.QueueFree();
            }
        }

        _lights.Clear();
    }

    public override void _ExitTree()
    {
        _modulate?.QueueFree();
        ClearLights();
    }

    /// <summary>Brilho redondo (centro claro, borda some) de 256 px.</summary>
    private static GradientTexture2D Glow() => _glow ??= new GradientTexture2D
    {
        Width = 256,
        Height = 256,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient { Colors = [Colors.White, new Color(1f, 1f, 1f, 0f)], Offsets = [0f, 1f] },
    };
}
