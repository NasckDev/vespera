using System;
using Godot;

namespace Vespera.Combat;

/// <summary>Parâmetros de uma explosão de partículas (<c>parts(x, y, n, col, o)</c>, <c>combat.js:10-13</c>).</summary>
public struct BurstOptions
{
    public float V0 = 20;
    public float V1 = 90;
    public float Up;
    public float L0 = 0.25f;
    public float L1 = 0.6f;

    /// <summary>Tamanho fixo; 0 = aleatório entre 1 e 2.2.</summary>
    public float Size;
    public float Gravity = 60;

    /// <summary>Ângulo central (NaN = em volta) e abertura.</summary>
    public float Angle = float.NaN;
    public float Spread = 0.6f;

    public BurstOptions()
    {
    }
}

/// <summary>
/// Camada de efeitos desenhada num único <c>_Draw</c> (sem um nó por efeito): partículas, arcos de golpe, anéis, linhas de
/// corrente e "fantasmas" de sprite (rastro da esquiva). Tudo em arrays fixos reaproveitados (pool em anel: quando cheio,
/// sobrescreve o mais antigo), então não aloca por frame. Duas instâncias: normal e aditiva (brilho).
/// </summary>
public partial class FxLayer : Node2D
{
    public const int MaxParticles = 700;
    public const int MaxShapes = 96;

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly Shape[] _shapes = new Shape[MaxShapes];
    private readonly Random _random = new();
    private int _nextParticle;
    private int _nextShape;
    private int _liveParticles;
    private int _liveShapes;

    private enum ShapeKind
    {
        None,
        Arc,
        Ring,
        Line,
        Ghost,
    }

    /// <summary>Desenho extra a cada frame (telegraphs, barras), chamado depois dos efeitos.</summary>
    public Action<FxLayer>? DrawExtra { get; set; }

    public int LiveParticles => _liveParticles;

    /// <summary>Partículas em volta de (x, y): velocidade <c>R(v0, v1)</c>, vy −up, vida <c>R(l0, l1)</c>.</summary>
    public void Burst(Vector2 at, int count, Color[] colors, in BurstOptions o)
    {
        for (var i = 0; i < count; i++)
        {
            var a = float.IsNaN(o.Angle) ? Range(0, MathF.Tau) : o.Angle + Range(-o.Spread, o.Spread);
            var sp = Range(o.V0, o.V1);
            ref var p = ref _particles[_nextParticle];
            if (p.Max <= 0 || p.T >= p.Max)
            {
                _liveParticles++;
            }

            p.Position = at;
            p.Velocity = new Vector2(MathF.Cos(a) * sp, (MathF.Sin(a) * sp) - o.Up);
            p.T = 0;
            p.Max = Range(o.L0, o.L1);
            p.Color = colors[_random.Next(colors.Length)];
            p.Size = o.Size > 0 ? o.Size : Range(1f, 2.2f);
            p.Gravity = o.Gravity;
            _nextParticle = (_nextParticle + 1) % MaxParticles;
        }
    }

    public void Burst(Vector2 at, int count, Color[] colors) => Burst(at, count, colors, new BurstOptions());

    /// <summary>Arco de golpe (<c>type:'arc'</c>): varre de 35% a 100% da abertura no primeiro terço da vida.</summary>
    public void Arc(Vector2 at, float angle, float arc, float radius, float life, Color color, float width, bool flip, float delay = 0)
    {
        ref var s = ref NextShape();
        s.Kind = ShapeKind.Arc;
        s.Position = at;
        s.Angle = angle;
        s.Arc = arc;
        s.R0 = radius;
        s.T = -delay;
        s.Max = life;
        s.Color = color;
        s.Width = width;
        s.Flip = flip;
    }

    /// <summary>Anel em elipse (×0.7 em y) que cresce de <paramref name="r0"/> a <paramref name="r1"/>.</summary>
    public void Ring(Vector2 at, float r0, float r1, float life, Color color, float delay = 0)
    {
        ref var s = ref NextShape();
        s.Kind = ShapeKind.Ring;
        s.Position = at;
        s.R0 = r0;
        s.R1 = r1;
        s.T = -delay;
        s.Max = life;
        s.Color = color;
    }

    /// <summary>Linha tracejada que some (corrente que puxou).</summary>
    public void Line(Vector2 from, Vector2 to, float life, Color color)
    {
        ref var s = ref NextShape();
        s.Kind = ShapeKind.Line;
        s.Position = from;
        s.To = to;
        s.T = 0;
        s.Max = life;
        s.Color = color;
    }

    /// <summary>Cópia de um sprite que some (rastro da esquiva/investida). Alfa inicial <paramref name="alpha"/>, −4/s.</summary>
    public void Ghost(Texture2D texture, Vector2 foot, Vector2 anchor, float scale, bool flip, float alpha)
    {
        ref var s = ref NextShape();
        s.Kind = ShapeKind.Ghost;
        s.Texture = texture;
        s.Position = foot;
        s.To = anchor;
        s.R0 = scale;
        s.Flip = flip;
        s.T = 0;
        s.Max = alpha / 4f;
        s.Width = alpha;
        s.Color = Colors.White;
    }

    public void Clear()
    {
        Array.Clear(_particles);
        Array.Clear(_shapes);
        _liveParticles = 0;
        _liveShapes = 0;
    }

    public void Step(float dt)
    {
        if (_liveParticles > 0)
        {
            var drag = MathF.Pow(0.2f, dt);
            var live = 0;
            for (var i = 0; i < MaxParticles; i++)
            {
                ref var p = ref _particles[i];
                if (p.Max <= 0 || p.T >= p.Max)
                {
                    continue;
                }

                p.T += dt;
                p.Position += p.Velocity * dt;
                p.Velocity.Y += p.Gravity * dt;
                p.Velocity.X *= drag;
                if (p.T < p.Max)
                {
                    live++;
                }
            }

            _liveParticles = live;
        }

        if (_liveShapes > 0)
        {
            var live = 0;
            for (var i = 0; i < MaxShapes; i++)
            {
                ref var s = ref _shapes[i];
                if (s.Kind == ShapeKind.None)
                {
                    continue;
                }

                s.T += dt;
                if (s.T >= s.Max)
                {
                    s.Kind = ShapeKind.None;
                    s.Texture = null;
                    continue;
                }

                live++;
            }

            _liveShapes = live;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        for (var i = 0; i < MaxShapes; i++)
        {
            ref var s = ref _shapes[i];
            if (s.Kind != ShapeKind.None && s.T >= 0)
            {
                DrawShape(ref s);
            }
        }

        DrawSetTransform(Vector2.Zero);
        for (var i = 0; i < MaxParticles; i++)
        {
            ref var p = ref _particles[i];
            if (p.Max <= 0 || p.T >= p.Max)
            {
                continue;
            }

            var a = 1f - (p.T / p.Max);
            DrawRect(new Rect2(MathF.Round(p.Position.X), MathF.Round(p.Position.Y), p.Size, p.Size), p.Color with { A = p.Color.A * a });
        }

        DrawExtra?.Invoke(this);
        DrawSetTransform(Vector2.Zero);
    }

    // --- Primitivas para quem desenha por cima (telegraphs, barras) ---

    /// <summary>Elipse preenchida + contorno (<c>tgCircle</c>: ×0.72 em y).</summary>
    public void DrawEllipse(Vector2 center, float radius, float yScale, Color fill, Color outline, float width = 1.2f)
    {
        DrawSetTransform(center, 0, new Vector2(1, yScale));
        if (fill.A > 0)
        {
            DrawCircle(Vector2.Zero, radius, fill);
        }

        if (outline.A > 0)
        {
            DrawArc(Vector2.Zero, radius, 0, MathF.Tau, 32, outline, width);
        }

        DrawSetTransform(Vector2.Zero);
    }

    /// <summary>Faixa a partir de <paramref name="from"/> na direção do ângulo (<c>tgLine</c>).</summary>
    public void DrawBand(Vector2 from, float angle, float length, float width, Color fill, Color outline)
    {
        DrawSetTransform(from, angle);
        var rect = new Rect2(0, -width / 2, length, width);
        DrawRect(rect, fill);
        DrawRect(rect, outline, false, 1);
        DrawSetTransform(Vector2.Zero);
    }

    /// <summary>Setor (<c>tgSector</c>: ×0.8 em y).</summary>
    public void DrawSector(Vector2 center, float angle, float arc, float radius, Color fill, Color outline)
    {
        DrawSetTransform(center, 0, new Vector2(1, 0.8f));
        var poly = SectorBuffer;
        poly[0] = Vector2.Zero;
        for (var i = 0; i <= 16; i++)
        {
            var a = angle - (arc / 2) + (arc * i / 16f);
            poly[i + 1] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }

        DrawColoredPolygon(poly, fill);
        DrawArc(Vector2.Zero, radius, angle - (arc / 2), angle + (arc / 2), 16, outline, 1);
        DrawSetTransform(Vector2.Zero);
    }

    private static readonly Vector2[] SectorBuffer = new Vector2[18];

    private void DrawShape(ref Shape s)
    {
        var k = s.T / s.Max;
        switch (s.Kind)
        {
            case ShapeKind.Arc:
            {
                var a0 = s.Angle - (s.Arc / 2);
                var a1 = s.Angle + (s.Arc / 2);
                var sweep = Mathf.Lerp(0.35f, 1f, MathF.Min(1, k * 3));
                var from = s.Flip ? a1 - ((a1 - a0) * sweep) : a0;
                var to = s.Flip ? a1 : a0 + ((a1 - a0) * sweep);
                var alpha = 1 - k;
                DrawArc(s.Position, s.R0 * 0.9f, from, to, 20, s.Color with { A = s.Color.A * alpha }, MathF.Max(1, s.Width * (1 - (k * 0.5f))));
                DrawArc(s.Position, s.R0 * 0.9f, from, to, 20, new Color(1, 1, 1, alpha * 0.7f), 1);
                break;
            }

            case ShapeKind.Ring:
            {
                var r = MathF.Max(0.1f, Mathf.Lerp(s.R0, s.R1, k));
                DrawSetTransform(s.Position, 0, new Vector2(1, 0.7f));
                DrawArc(Vector2.Zero, r, 0, MathF.Tau, 28, s.Color with { A = s.Color.A * (1 - k) }, 2);
                DrawSetTransform(Vector2.Zero);
                break;
            }

            case ShapeKind.Line:
                DrawDashedLine(s.Position, s.To, s.Color with { A = s.Color.A * (1 - k) }, 1.5f, 3);
                break;
            case ShapeKind.Ghost when s.Texture is not null:
            {
                var alpha = MathF.Max(0, s.Width - (s.T * 4)) * 0.35f;
                DrawSetTransform(s.Position, 0, new Vector2(s.Flip ? -s.R0 : s.R0, s.R0));
                DrawTexture(s.Texture, -s.To, new Color(1, 1, 1, alpha));
                DrawSetTransform(Vector2.Zero);
                break;
            }
        }
    }

    private ref Shape NextShape()
    {
        ref var s = ref _shapes[_nextShape];
        if (s.Kind == ShapeKind.None)
        {
            _liveShapes++;
        }

        _nextShape = (_nextShape + 1) % MaxShapes;
        return ref s;
    }

    private float Range(float min, float max) => min + ((float)_random.NextDouble() * (max - min));

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float T;
        public float Max;
        public float Size;
        public float Gravity;
        public Color Color;
    }

    private struct Shape
    {
        public ShapeKind Kind;
        public Vector2 Position;
        public Vector2 To;
        public float Angle;
        public float Arc;
        public float R0;
        public float R1;
        public float T;
        public float Max;
        public float Width;
        public bool Flip;
        public Color Color;
        public Texture2D? Texture;
    }
}
