using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Iluminação da v2 (spec 01 §4.5/§4.6/§9.1) numa passada de tela cheia: escuridão pela hora e pelo mapa, furos de
/// luz (luzes do mapa, lâmpadas da rua que acendem ao escurecer, lanterna do jogador × <c>Stats.Light</c> e todo
/// <see cref="LightEmitter"/>), glow colorido aditivo, escurecimento da Lanterna perdida, tinta de estação e o rosa
/// da invasão. As luzes vão numa textura de dados 64×2 atualizada por frame sem alocar (só as visíveis, até
/// <see cref="MaxLights"/>). O clima fica no <see cref="WeatherLayer"/> por cima.
/// </summary>
public partial class LightingLayer : CanvasLayer
{
    public const int MaxLights = 64;

    /// <summary>Inverno <c>rgba(120,170,255,.07)</c> e Outono <c>rgba(255,140,60,.035)</c> fora de interiores.</summary>
    private static readonly Color WinterTint = new(120 / 255f, 170 / 255f, 1f, 0.07f);
    private static readonly Color AutumnTint = new(1f, 140 / 255f, 60 / 255f, 0.035f);

    private static readonly StringName LightDataParam = "light_data";
    private static readonly StringName LightCountParam = "light_count";
    private static readonly StringName DarknessParam = "darkness";
    private static readonly StringName ExtraDarkParam = "extra_dark";
    private static readonly StringName TintParam = "tint";
    private static readonly StringName PreTintParam = "pre_tint";
    private static readonly StringName ViewSizeParam = "view_size";
    private static readonly StringName DarkColorParam = "dark_color";

    private static IReadOnlyDictionary<string, IReadOnlyList<MapLight>>? _lightsJson;

    private Image _data = null!;
    private ImageTexture _texture = null!;
    private ShaderMaterial _material = null!;
    private TextureRect _rect = null!;
    private SubViewport _darkView = null!;
    private ColorRect _grade = null!;
    private IReadOnlyList<MapLight> _mapLights = [];
    private MapDef? _map;
    private bool _streetLamps;
    private Vector2 _viewSize;
    private float _time;

    public World World { get; set; } = null!;

    public CityLife City { get; set; } = null!;

    public WeatherLayer Weather { get; private set; } = null!;

    /// <summary>Escuridão aplicada neste frame (0 se abaixo de 0,02, como a v2).</summary>
    public float CurrentDarkness { get; private set; }

    /// <summary>Luzes enviadas ao shader neste frame (visíveis).</summary>
    public int CurrentLightCount { get; private set; }

    /// <summary>Cor da hora multiplicada sobre a rua neste frame (branco fora da rua).</summary>
    public Color CurrentGrade { get; private set; } = Colors.White;

    /// <summary>Lâmpadas da rua acesas (nível ≥ 0,5) neste frame.</summary>
    public int LitStreetLamps { get; private set; }

    /// <summary>Raio (u) da lanterna do jogador neste frame.</summary>
    public float PlayerLightRadius { get; private set; }

    public static IReadOnlyDictionary<string, IReadOnlyList<MapLight>> LightsJson =>
        _lightsJson ??= CityLighting.ParseLightsJson(FileAccess.GetFileAsString("res://assets/maps/lights.json"));

    public override void _Ready()
    {
        Layer = 5;
        _data = Image.CreateEmpty(MaxLights, 2, false, Image.Format.Rgbaf);
        _texture = ImageTexture.CreateFromImage(_data);
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/World/City/city_lighting.gdshader") };
        _material.SetShaderParameter(LightDataParam, _texture);
        var dark = CityLighting.DarkColor;
        _material.SetShaderParameter(DarkColorParam, new Vector3(dark.R, dark.G, dark.B));
        // Cor da hora na rua (B2-D51): multiplicada sobre o mundo, por baixo da escuridão.
        _grade = new ColorRect
        {
            Name = "Grade",
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul },
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.White,
            Visible = false,
        };
        _grade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_grade);
        // A escuridão é um campo suave: calculada na resolução base (640×360) numa SubViewport e esticada com filtro
        // linear, custa ~1/9 de rodar o shader por pixel da janela em 1080p (B2-D51). "Darkness" é quem aparece.
        _darkView = new SubViewport
        {
            Name = "DarknessView",
            TransparentBg = true,
            Disable3D = true,
            HandleInputLocally = false,
            Size = new Vector2I(640, 360),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_darkView);
        var pass = new ColorRect { Name = "DarknessPass", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
        pass.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _darkView.AddChild(pass);
        _rect = new TextureRect
        {
            Name = "Darkness",
            Texture = _darkView.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.PremultAlpha },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        Weather = new WeatherLayer { Name = "Weather", City = City };
        AddChild(Weather);
    }

    /// <summary>Mapa novo: luzes do <c>maps.json</c> (interiores) ou do <c>lights.json</c> (mapas hd).</summary>
    public void SetMap(MapDef map)
    {
        _map = map;
        _mapLights = map.LightsKey is { } key && LightsJson.TryGetValue(key, out var fromJson) ? fromJson : map.Lights;
        _streetLamps = !map.Indoor && !map.Dungeon; // só a Rua da Forja: as lâmpadas acendem ao escurecer (B2-D40)
        Weather.SetMap(map);
    }

    public override void _Process(double delta)
    {
        _time += (float)Math.Min(0.05, delta);
        if (_map is null || !GameManager.Instance.IsInsideTree())
        {
            return;
        }

        var state = GameManager.Instance.State;
        var clock = state.Save.ClockMinutes;
        var view = GetViewport().GetVisibleRect().Size;
        // Sem a escuridão na tela (testes de custo, menus que a escondem), a SubViewport também para.
        var showDark = _rect.IsVisibleInTree();
        var mode = showDark ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (_darkView.RenderTargetUpdateMode != mode)
        {
            _darkView.RenderTargetUpdateMode = mode;
        }

        if (view != _viewSize)
        {
            _viewSize = view;
            _darkView.Size = new Vector2I(Math.Max(1, (int)MathF.Ceiling(view.X)), Math.Max(1, (int)MathF.Ceiling(view.Y)));
            _material.SetShaderParameter(ViewSizeParam, view);
        }

        // Só a rua tem a cor da hora; interiores e Brechas seguem a v2.
        _grade.Visible = _streetLamps;
        if (_streetLamps)
        {
            var g = CityLighting.StreetGrade(clock);
            CurrentGrade = new Color(g.R, g.G, g.B);
            _grade.Color = CurrentGrade;
        }
        else
        {
            CurrentGrade = Colors.White;
        }

        var darkness = CityLighting.Darkness(_map, clock);
        CurrentDarkness = darkness < CityLighting.MinDarkness ? 0f : darkness;
        var extra = _map.Dungeon && state.HasLostLantern ? CityLighting.LostLanternExtra : 0f;

        var xf = GetViewport().GetCanvasTransform();
        var zoom = xf.X.Length();
        var count = 0;

        // Lanterna do jogador: (x ± 8 conforme o lado, y - 10), raio (morto ? 40 : 72) × luz.
        var player = World.Player;
        var flip = player.Body.Scale.X < 0;
        var playerPos = player.GlobalPosition + new Vector2(flip ? -8f : 8f, -10f);
        PlayerLightRadius = CityLighting.PlayerLightRadius(state.Hp <= 0, state.Stats.Light);
        count = Push(count, xf, zoom, playerPos, PlayerLightRadius, CityLighting.AmberKind, CityLighting.PlayerHoleAlpha, 1f, CityLighting.HoleRadiusScale);

        // Luzes dinâmicas (portais, Combate…).
        var emitters = LightEmitter.Active;
        for (var i = 0; i < emitters.Count && count < MaxLights; i++)
        {
            var e = emitters[i];
            if (e.Intensity <= 0f || !e.IsVisibleInTree())
            {
                continue;
            }

            count = Push(count, xf, zoom, e.GlobalPosition, e.Radius, e.Kind, CityLighting.HoleAlpha * e.Intensity, e.Intensity * e.GlowBoost, CityLighting.HoleRadiusScale);
        }

        // Luzes do mapa com cintilação; na rua, cada lâmpada acende no seu minuto.
        var lit = 0;
        for (var i = 0; i < _mapLights.Count && count < MaxLights; i++)
        {
            var l = _mapLights[i];
            var level = 1f;
            if (_streetLamps)
            {
                level = CityLighting.LampLevel(i, clock);
                if (level >= 0.5f)
                {
                    lit++;
                }

                if (level <= 0f)
                {
                    continue;
                }
            }

            var radius = l.Radius * CityLighting.Flicker(i, _time);
            var holeAlpha = (_streetLamps ? CityLighting.StreetLampHoleAlpha : CityLighting.HoleAlpha) * level;
            var holeScale = _streetLamps ? CityLighting.StreetLampHoleScale : CityLighting.HoleRadiusScale;
            count = Push(count, xf, zoom, new Vector2(l.X, l.Y), radius, l.Kind, holeAlpha, level, holeScale);
        }

        LitStreetLamps = lit;
        CurrentLightCount = count;
        _texture.Update(_data);
        _material.SetShaderParameter(LightCountParam, count);
        _material.SetShaderParameter(DarknessParam, CurrentDarkness);
        _material.SetShaderParameter(ExtraDarkParam, extra);
        _material.SetShaderParameter(TintParam, SeasonTint());
        _material.SetShaderParameter(PreTintParam, InvasionTint());
    }

    /// <summary>Grava a luz na coluna <paramref name="index"/> se ela aparece na tela; devolve o próximo índice livre.</summary>
    private int Push(int index, Transform2D xf, float zoom, Vector2 world, float radius, string kind, float holeAlpha, float glowLevel, float holeScale)
    {
        if (index >= MaxLights)
        {
            return index;
        }

        var screen = xf * world;
        var hole = radius * holeScale * zoom;
        if (screen.X + hole < 0 || screen.Y + hole < 0 || screen.X - hole > _viewSize.X || screen.Y - hole > _viewSize.Y)
        {
            return index;
        }

        var color = CityLighting.ColorOf(kind);
        _data.SetPixel(index, 0, new Color(screen.X, screen.Y, hole, holeAlpha));
        _data.SetPixel(index, 1, new Color(color.R, color.G, color.B, CityLighting.GlowAlphaFor(kind) * glowLevel));
        return index + 1;
    }

    private Color SeasonTint()
    {
        if (_map is null || _map.Indoor)
        {
            return Colors.Transparent;
        }

        return City.CurrentSeasonId() switch
        {
            "inverno" => WinterTint,
            "outono" => AutumnTint,
            _ => Colors.Transparent,
        };
    }

    /// <summary>Rua da Forja em invasão: <c>rgba(230,74,142, 0,06 + 0,04 sin(3t))</c> (<c>render.js:30</c>).</summary>
    private Color InvasionTint()
    {
        if (_map is not { Id: World.CityMapId } || !City.Brechas.InvasionActive)
        {
            return Colors.Transparent;
        }

        return new Color(230 / 255f, 74 / 255f, 142 / 255f, 0.06f + (0.04f * MathF.Sin(_time * 3f)));
    }
}
