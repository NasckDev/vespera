using System;
using Godot;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// Portal de Brecha na Rua da Forja (spec 01 §4.3, <c>drawPortal</c>, <c>render.js:182-188</c>). Abre com a sequência
/// do super kit (<c>assets/sprites/fx/portal_open_0..5.png</c>, ~1 s crescendo, D38) e depois pulsa alternando os quadros
/// abertos a cada 0,28 s, com escala <c>1 + 0,03 sin(2,6t)</c> e rótulo com bairro e timer em <c>y-92</c>.
/// B2-D51 (a véspera é um evento): poça magenta aditiva 150×56, aura vertical, onda de choque ao abrir, mais faíscas, e
/// três luzes magenta (a do portal, 125 u, que estoura a 1,7× na abertura, e duas subindo em coluna) que furam a escuridão
/// e somam glow por cima dela, então o portal se vê da rua inteira à noite.
/// </summary>
public partial class PortalVisual : Node2D
{
    /// <summary>Duração da abertura (6 quadros).</summary>
    public const float OpeningSeconds = 1f;

    /// <summary>Troca de quadro no loop (<c>floor(age/0,28) % 2</c>).</summary>
    public const float LoopFrameSeconds = 0.28f;

    private const int FrameCount = 6;
    private const int LoopFrameA = 3;
    private const int LoopFrameB = 4;

    /// <summary>Pivô no pé (centro da poça de luz) dos quadros recortados, em px da textura.</summary>
    private static readonly Vector2 FrameAnchor = new(199, 672);

    /// <summary>Densidade (px por u): o quadro aberto fica do tamanho do <c>por3</c> do atlas (53 u de largura).</summary>
    private const float FrameDensity = 7.1f;

    private static readonly Color Magenta = new(230 / 255f, 74 / 255f, 142 / 255f);
    private static readonly Color Amber = new("#F2A541");
    private static Texture2D[]? _frames;
    private static Texture2D? _glowTexture;
    private static Texture2D? _ringTexture;

    /// <summary>Raio da luz do portal aberto (u). A v2 usava 90; ficava discreto demais à noite.</summary>
    public const float LightRadius = 125f;

    /// <summary>Duração da onda de choque da abertura.</summary>
    public const float RingSeconds = 0.9f;
    private static CanvasItemMaterial? _additive;

    private Sprite2D _sprite = null!;
    private Sprite2D _glow = null!;
    private Sprite2D _aura = null!;
    private Sprite2D _ring = null!;
    private LightEmitter _column0 = null!;
    private LightEmitter _column1 = null!;
    private CpuParticles2D _particles = null!;
    private LightEmitter _light = null!;
    private Label? _title;
    private Label? _timer;
    private int _shownSeconds = -1;
    private bool? _shownUrgent;
    private float _age;
    private float _time;
    private float _closing = -1f;
    private int _frame = -1;

    /// <summary>Brecha deste portal.</summary>
    public BrechaInstance Brecha { get; set; } = null!;

    public WorldLabels? Labels { get; set; }

    /// <summary>Idade inicial: 0 toca a abertura; <see cref="OpeningSeconds"/> já começa no loop.</summary>
    public float StartAge { get; set; }

    /// <summary>Segundos desde que abriu (para testes).</summary>
    public float Age => _age;

    /// <summary>Ainda tocando a sequência de abertura.</summary>
    public bool IsOpening => _age < OpeningSeconds && _closing < 0;

    /// <summary>Quadro mostrado agora (0–5).</summary>
    public int Frame => _frame;

    public static Texture2D[] Frames => _frames ??= LoadFrames();

    public override void _Ready()
    {
        _age = StartAge;
        _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _glowTexture ??= CreateGlowTexture();
        _ringTexture ??= CreateRingTexture();

        _aura = new Sprite2D
        {
            Name = "Aura",
            Texture = _glowTexture,
            Material = _additive,
            Position = new Vector2(0, -46),
            Scale = new Vector2(84f / _glowTexture.GetWidth(), 150f / _glowTexture.GetHeight()),
            Modulate = Magenta with { A = 0f },
        };
        AddChild(_aura);

        _glow = new Sprite2D
        {
            Name = "Glow",
            Texture = _glowTexture,
            Material = _additive,
            Scale = new Vector2(150f / _glowTexture.GetWidth(), 56f / _glowTexture.GetHeight()),
            Modulate = Magenta,
        };
        AddChild(_glow);

        _ring = new Sprite2D
        {
            Name = "Shockwave",
            Texture = _ringTexture,
            Material = _additive,
            Visible = StartAge < RingSeconds,
            Modulate = Magenta with { A = 0f },
        };
        AddChild(_ring);

        _sprite = new Sprite2D
        {
            Name = "Frame",
            Centered = false,
            Offset = -FrameAnchor,
            Position = new Vector2(0, 3), // spr(n, x, y + 3)
        };
        AddChild(_sprite);

        _particles = new CpuParticles2D
        {
            Name = "Sparks",
            Amount = 30,
            Lifetime = 1.1,
            Position = new Vector2(0, -40),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(20, 36),
            Direction = Vector2.Up,
            Spread = 180f,
            InitialVelocityMin = 15f,
            InitialVelocityMax = 20f,
            Gravity = new Vector2(0, -25),
            ScaleAmountMin = 1f,
            ScaleAmountMax = 2.2f,
            ColorRamp = CreateSparkRamp(),
            Material = _additive,
            LocalCoords = false,
        };
        AddChild(_particles);

        _light = new LightEmitter { Name = "Light", Radius = LightRadius, Kind = CityLighting.MagentaKind, Position = new Vector2(0, -30), GlowBoost = 2.2f };
        AddChild(_light);
        _column0 = new LightEmitter { Name = "Column0", Radius = 46f, Kind = CityLighting.MagentaKind, Position = new Vector2(0, -105), GlowBoost = 1.8f };
        AddChild(_column0);
        _column1 = new LightEmitter { Name = "Column1", Radius = 34f, Kind = CityLighting.MagentaKind, Position = new Vector2(0, -165), GlowBoost = 1.8f };
        AddChild(_column1);

        if (Labels is not null)
        {
            _title = Labels.Rent(WorldLabels.Style.PortalTitle);
            WorldLabels.SetText(_title, Brecha.Bairro);
            _timer = Labels.Rent(WorldLabels.Style.PortalTimer);
        }

        UpdateVisual();
    }

    public override void _ExitTree()
    {
        Labels?.Return(_title);
        Labels?.Return(_timer);
        _title = null;
        _timer = null;
    }

    /// <summary>Fecha: encolhe e some em 0,4 s, depois se libera.</summary>
    public void Close()
    {
        if (_closing >= 0)
        {
            return;
        }

        _closing = 0f;
        _particles.Emitting = false;
    }

    public override void _Process(double delta)
    {
        var dt = (float)Math.Min(0.05, delta);
        _time += dt;
        _age += dt;
        if (_closing >= 0)
        {
            _closing += dt;
            if (_closing >= 0.4f)
            {
                QueueFree();
                return;
            }
        }

        UpdateVisual();
        UpdateLabels();
    }

    private void UpdateVisual()
    {
        int frame;
        float scale;
        if (_age < OpeningSeconds)
        {
            frame = Math.Min(FrameCount - 1, (int)(_age / OpeningSeconds * FrameCount));
            scale = 0.85f + (0.15f * (_age / OpeningSeconds));
        }
        else
        {
            frame = ((int)MathF.Floor(_age / LoopFrameSeconds) % 2) == 1 ? LoopFrameA : LoopFrameB;
            scale = 1f + (0.03f * MathF.Sin(_time * 2.6f));
        }

        var open = Math.Clamp(_age / OpeningSeconds, 0f, 1f);
        var fade = _closing >= 0 ? 1f - (_closing / 0.4f) : 1f;
        if (frame != _frame)
        {
            _frame = frame;
            _sprite.Texture = Frames[frame];
        }

        _sprite.Scale = Vector2.One * (scale * fade / FrameDensity);
        _glow.Modulate = Magenta with { A = (0.62f + (0.15f * MathF.Sin(_time * 3f))) * open * fade };
        _aura.Modulate = Magenta with { A = (0.3f + (0.08f * MathF.Sin(_time * 2.2f))) * open * fade };

        // Abertura: a luz estoura (1,7×) e assenta; a onda de choque corre pelo chão.
        var burst = _age < 1.6f ? 0.7f * MathF.Max(0f, 1f - (MathF.Abs(_age - 0.5f) / 1.1f)) : 0f;
        var pulse = 1f + (0.05f * MathF.Sin(_time * 2.6f));
        _light.Intensity = open * fade;
        _light.Radius = LightRadius * (0.5f + (0.5f * open)) * (pulse + burst);
        var column = open * fade * (0.75f + (0.25f * MathF.Sin((_time * 1.7f) + 1f)));
        _column0.Intensity = column;
        _column1.Intensity = column * 0.8f;
        if (_ring.Visible)
        {
            var t = _age / RingSeconds;
            if (t >= 1f)
            {
                _ring.Visible = false;
            }
            else
            {
                var size = 24f + (236f * (1f - ((1f - t) * (1f - t))));
                _ring.Scale = new Vector2(size / _ringTexture!.GetWidth(), size * 0.38f / _ringTexture.GetHeight());
                _ring.Modulate = Magenta with { A = 0.9f * (1f - t) };
            }
        }

        Modulate = Colors.White with { A = fade };
    }

    private void UpdateLabels()
    {
        if (_title is null || _timer is null)
        {
            return;
        }

        var visible = _closing < 0;
        _title.Visible = visible;
        _timer.Visible = visible;
        if (!visible)
        {
            return;
        }

        var seconds = (int)Math.Ceiling(Math.Max(0, Brecha.TimeLeft));
        var urgent = Brecha.TimeLeft < 60;
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            WorldLabels.SetText(_timer, SeasonCalendar.FormatTimer(Brecha.TimeLeft));
        }

        if (urgent != _shownUrgent)
        {
            _shownUrgent = urgent;
            _timer.Modulate = urgent ? Magenta : Amber;
        }

        var head = WorldLabels.ToScreen(this, GlobalPosition + new Vector2(0, -92));
        WorldLabels.PlaceCentered(_title, head);
        WorldLabels.PlaceCentered(_timer, head + new Vector2(0, _title.Size.Y));
    }

    private static Texture2D[] LoadFrames()
    {
        var frames = new Texture2D[FrameCount];
        for (var i = 0; i < FrameCount; i++)
        {
            frames[i] = GD.Load<Texture2D>($"res://assets/sprites/fx/portal_open_{i}.png");
        }

        return frames;
    }

    /// <summary><c>makeGlow</c>: radial com alpha 1 no centro, 0,55 a 40% e 0 na borda (a cor vem do modulate).</summary>
    private static GradientTexture2D CreateGlowTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, Colors.White with { A = 0f });
        gradient.AddPoint(0.4f, Colors.White with { A = 0.55f });
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    /// <summary>Anel radial (transparente no centro e na borda) para a onda de choque da abertura.</summary>
    private static GradientTexture2D CreateRingTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White with { A = 0f });
        gradient.SetColor(1, Colors.White with { A = 0f });
        gradient.AddPoint(0.72f, Colors.White with { A = 0.15f });
        gradient.AddPoint(0.88f, Colors.White);
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 128,
            Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    /// <summary>Cores das partículas <c>[C.brecha, '#ff8ab8', '#2a1a35']</c>, somindo no fim da vida.</summary>
    private static Gradient CreateSparkRamp()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color("#ff8ab8"));
        ramp.SetColor(1, new Color("#2a1a35") with { A = 0f });
        ramp.AddPoint(0.5f, Magenta);
        return ramp;
    }
}
