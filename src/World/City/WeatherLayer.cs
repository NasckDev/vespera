using System;
using Godot;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Clima da estação em espaço de tela (spec 01 §4.6, <c>drawWeather</c>, <c>render.js:84-97</c>): neve no Inverno,
/// folhas no Outono, pétalas na Primavera e vaga-lumes aditivos no Verão. Desligado em interiores e no Núcleo.
/// D6: anda com <c>dt</c> (a v2 somava 0,016 por frame). Partículas num array fixo, desenhadas com
/// <c>DrawRect</c> num único nó: nada é alocado por frame.
/// </summary>
public partial class WeatherLayer : Node2D
{
    /// <summary>A v2 mede em px do dispositivo; a referência do projeto é 720p (como o tremor, D29).</summary>
    private const float ReferenceHeight = 720f;

    /// <summary>Escala da UI da v2 numa janela 1280×720: <c>clamp(min(1280/1280, 720/760), .78, 1.6)</c>.</summary>
    private const float UiScale = 720f / 760f;

    /// <summary>Passo de 1 frame da v2 a 60 fps.</summary>
    private const float V2Frame = 0.016f;

    private const int MaxParticles = 90;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly Random _random = new();
    private Config? _config;
    private string? _season;
    private bool _enabled;
    private Vector2 _view;
    private CanvasItemMaterial? _additive;

    public CityLife City { get; set; } = null!;

    /// <summary>Partículas vivas (testes).</summary>
    public int Count { get; private set; }

    /// <summary>Estação do clima atual, ou null sem clima.</summary>
    public string? ActiveSeason => _enabled ? _season : null;

    private struct Particle
    {
        public float X;
        public float Y;
        public float Phase;
        public float Size;
        public Color Color;
    }

    private sealed record Config(Color[] Colors, float Vx, float Vy, int Count, float Size, bool Fireflies);

    private static readonly Config Winter = new([new Color("#e8f0ff"), new Color("#c4d4f0")], -10, 40, 90, 2, false);
    private static readonly Config Autumn = new([new Color("#d96a2a"), new Color("#b8462a"), new Color("#e6a541")], -30, 28, 40, 3, false);
    private static readonly Config Spring = new([new Color("#ff9ac6"), new Color("#ffd2e6")], -20, 22, 35, 2, false);
    private static readonly Config Summer = new([new Color("#F2A541"), new Color("#ffe08a")], 0, -6, 25, 2, true);

    public void SetMap(MapDef map)
    {
        // render.js:85 — sem clima em interiores e no tema core; troca de mapa limpa (G.weather = []).
        _enabled = !map.Indoor && map.Theme != "core";
        Count = 0;
        _season = null;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_enabled)
        {
            if (Count > 0)
            {
                Count = 0;
                QueueRedraw();
            }

            return;
        }

        var season = City.CurrentSeasonId();
        if (season != _season)
        {
            _season = season;
            _config = season switch
            {
                "inverno" => Winter,
                "outono" => Autumn,
                "primavera" => Spring,
                "verao" => Summer,
                _ => null,
            };
            Count = 0;
            _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            Material = _config is { Fireflies: true } ? _additive : null;
        }

        if (_config is not { } cfg)
        {
            return;
        }

        _view = GetViewportRect().Size;
        var px = _view.Y / ReferenceHeight; // px do dispositivo (720p) → px da tela base
        while (Count < cfg.Count)
        {
            _particles[Count++] = new Particle
            {
                X = Range(0, _view.X),
                Y = Range(0, _view.Y),
                Phase = Range(0, 6),
                Color = cfg.Colors[_random.Next(cfg.Colors.Length)],
                Size = cfg.Size * UiScale * Range(0.7f, 1.3f) * px,
            };
        }

        var dt = (float)Math.Min(0.05, delta);
        var frames = dt / V2Frame;
        for (var i = 0; i < Count; i++)
        {
            ref var w = ref _particles[i];
            w.Phase += dt;
            if (cfg.Fireflies)
            {
                w.X += MathF.Sin(w.Phase) * 0.6f * frames * px;
                w.Y += ((MathF.Cos(w.Phase * 0.7f) * 0.4f * frames) + (cfg.Vy * dt)) * px;
            }
            else
            {
                w.X += (cfg.Vx + (MathF.Sin(w.Phase) * 12f)) * dt * UiScale * px;
                w.Y += cfg.Vy * dt * UiScale * px;
            }

            if (w.Y > _view.Y + 5)
            {
                w.Y = -5;
                w.X = Range(0, _view.X);
            }

            if (w.Y < -6)
            {
                w.Y = _view.Y;
            }

            if (w.X < -5)
            {
                w.X = _view.X + 4;
            }

            if (w.X > _view.X + 6)
            {
                w.X = -4;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_enabled || _config is not { } cfg)
        {
            return;
        }

        for (var i = 0; i < Count; i++)
        {
            ref var w = ref _particles[i];
            var alpha = cfg.Fireflies ? 0.4f + (0.4f * MathF.Sin(w.Phase * 3f)) : 0.75f;
            var size = MathF.Max(1f, w.Size);
            DrawRect(new Rect2(MathF.Round(w.X), MathF.Round(w.Y), size, size), w.Color with { A = alpha });
        }
    }

    private float Range(float min, float max) => min + ((float)_random.NextDouble() * (max - min));
}
