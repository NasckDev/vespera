using Godot;
using Vespera.Game.Actors;
using Vespera.Game.Combat;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Selo da saída enquanto a sala tem monstros: uma cortina de energia magenta erguida sobre o retângulo da passagem
/// (com corpo sólido no pé), que ondula e se desfaz quando a sala fica limpa.
/// </summary>
public partial class Barrier : Node2D
{
    private const float DissolveSeconds = 0.6f;

    private readonly Rect2 _rect;
    private readonly float _height;
    private StaticBody2D _body = null!;
    private float _t;
    private float _open = -1f;

    public Barrier(Rect2 rect, float height)
    {
        _rect = rect;
        _height = height;
        Position = new Vector2(rect.GetCenter().X, rect.End.Y);
    }

    public bool IsOpen { get; private set; }

    public Rect2 Rect => _rect;

    public override void _Ready()
    {
        _body = new StaticBody2D { CollisionLayer = Player.WorldLayer, CollisionMask = 0 };
        _body.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = _rect.Size },
            Position = new Vector2(0, -_rect.Size.Y / 2f),
        });
        AddChild(_body);
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _open = 0f;
        _body.QueueFree();
        CombatSfx.Play(this, CombatSound.BarrierDown, Vector2.Zero);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        if (_open >= 0f)
        {
            _open += dt / DissolveSeconds;
            if (_open >= 1f)
            {
                QueueFree();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var w = _rect.Size.X;
        var d = _rect.Size.Y;
        var fade = _open < 0f ? 1f : 1f - _open;
        var rise = _open < 0f ? 1f : 1f - (_open * 0.6f);
        var magenta = new Color("e64a8e");
        var hot = new Color("ffc2e0");

        // Base no chão: o retângulo da passagem aceso.
        DrawRect(new Rect2(-w / 2f, -d, w, d), magenta with { A = 0.55f * fade });
        DrawRect(new Rect2(-w / 2f, -d, w, d), hot with { A = 0.8f * fade }, false, 2f);

        // Cortina: faixas verticais que sobem e tremulam, mais fortes embaixo.
        var horizontal = w >= d;
        var span = horizontal ? w : d;
        var sheet = _height * rise;
        Vector2[] quad = horizontal
            ? [new(-w / 2f, -d / 2f), new(w / 2f, -d / 2f), new(w / 2f, (-d / 2f) - sheet), new(-w / 2f, (-d / 2f) - sheet)]
            : [new(0, -d), new(0, 0), new(0, -sheet), new(0, -d - sheet)];
        if (horizontal)
        {
            var bottom = magenta with { A = 0.42f * fade };
            var top = magenta with { A = 0f };
            DrawPolygon(quad, [bottom, bottom, top, top]);
        }
        else
        {
            DrawPolygon([new(-4f, -d), new(4f, 0), new(4f, -sheet), new(-4f, -d - sheet)],
                [magenta with { A = 0.42f * fade }, magenta with { A = 0.42f * fade }, magenta with { A = 0f }, magenta with { A = 0f }]);
        }

        var strands = Mathf.Max(6, (int)(span / 7f));
        for (var i = 0; i <= strands; i++)
        {
            var k = (float)i / strands;
            var basePoint = horizontal ? new Vector2((-w / 2f) + (k * w), -d / 2f) : new Vector2(0, -d + (k * d));
            var flicker = 0.55f + (0.45f * Mathf.Sin((_t * 7f) + (i * 1.7f)));
            var h = _height * rise * (0.75f + (0.25f * Mathf.Sin((_t * 3f) + (i * 0.9f))));
            var top = basePoint + new Vector2(Mathf.Sin((_t * 4f) + i) * 2f, -h);
            DrawLine(basePoint, top, magenta with { A = 0.5f * flicker * fade }, 7f, true);
            DrawLine(basePoint, basePoint.Lerp(top, 0.6f), hot with { A = 0.65f * flicker * fade }, 2.2f, true);
        }

        var sweep = (_t * 0.8f) % 1f;
        var y = -d / 2f - (_height * rise * sweep);
        var a = horizontal ? new Vector2(-w / 2f, y) : new Vector2(0, -d + y + (d / 2f));
        var b = horizontal ? new Vector2(w / 2f, y) : new Vector2(0, y + (d / 2f));
        DrawLine(a, b, hot with { A = 0.5f * (1f - sweep) * fade }, 2f, true);
    }
}
