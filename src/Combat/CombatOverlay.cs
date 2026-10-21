using System;
using Godot;
using Vespera.Core.Combat.Ai;

namespace Vespera.Combat;

/// <summary>
/// Camada de tela do combate (<c>drawOverlayWorld</c>, <c>render.js:212-230</c>, e <c>render.js:236-237</c>): números
/// flutuantes (pool fixo), barras de vida dos monstros feridos (e das elites, com losangos das partes e o nome), flash
/// vermelho ao ser ferido e o tom rosado da isca. Tamanhos da v2 (12U/15U a 1280 px) na base 640×360.
/// </summary>
public partial class CombatOverlay : Node2D
{
    public const int MaxFloats = 96;
    private const int SmallFont = 6;
    private const int BigFont = 8;
    private const float RedFlashSeconds = 0.35f;

    private readonly Floater[] _floats = new Floater[MaxFloats];
    private readonly Random _random = new();
    private CombatDirector _combat = null!;
    private Font _font = null!;
    private int _next;
    private int _live;
    private float _red;
    private float _charm;

    public int LiveFloats => _live;

    public void Setup(CombatDirector combat)
    {
        _combat = combat;
        _font = GD.Load<Font>("res://assets/fonts/PixelifySans.ttf");
    }

    /// <summary><c>addFloat</c>: texto no mundo que sobe 16 u/s (grande 22) e dura 0,8 s (grande 1,1).</summary>
    public void Float(Vector2 world, string text, Color color, bool big, float alpha = 1)
    {
        ref var f = ref _floats[_next];
        if (f.Text is null)
        {
            _live++;
        }

        f.World = world + new Vector2((float)((_random.NextDouble() * 8) - 4), 0);
        f.Text = text;
        f.Color = color;
        f.Big = big;
        f.T = 0;
        f.Alpha = alpha;
        _next = (_next + 1) % MaxFloats;
    }

    public void RedFlash() => _red = RedFlashSeconds;

    public void Charm() => _charm = 0.1f;

    public void Clear()
    {
        Array.Clear(_floats);
        _live = 0;
        _red = 0;
        _charm = 0;
    }

    public void Step(float dt)
    {
        _red = MathF.Max(0, _red - dt);
        _charm = MathF.Max(0, _charm - dt);
        var live = 0;
        for (var i = 0; i < MaxFloats; i++)
        {
            ref var f = ref _floats[i];
            if (f.Text is null)
            {
                continue;
            }

            f.T += dt;
            f.World.Y -= (f.Big ? 22 : 16) * dt;
            if (f.T >= (f.Big ? 1.1f : 0.8f))
            {
                f.Text = null;
                continue;
            }

            live++;
        }

        _live = live;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var view = GetViewportRect().Size;
        var xf = GetViewport().GetCanvasTransform();
        DrawBars(xf);
        for (var i = 0; i < MaxFloats; i++)
        {
            ref var f = ref _floats[i];
            if (f.Text is null)
            {
                continue;
            }

            var max = f.Big ? 1.1f : 0.8f;
            var k = f.T / max;
            var a = (1 - (MathF.Max(0, k - 0.6f) / 0.4f)) * f.Alpha;
            var size = (int)((f.Big ? BigFont : SmallFont) * (f.T < 0.08f ? 1.3f : 1f));
            var at = xf * f.World;
            DrawString(_font, at + new Vector2(-60, 0), f.Text, HorizontalAlignment.Center, 120, size + 1, new Color(0.03f, 0.02f, 0.06f, a * 0.8f));
            DrawString(_font, at + new Vector2(-60, -0.5f), f.Text, HorizontalAlignment.Center, 120, size, f.Color with { A = a });
        }

        if (_red > 0)
        {
            DrawRect(new Rect2(Vector2.Zero, view), new Color(180 / 255f, 20 / 255f, 50 / 255f, _red * 0.5f));
        }

        if (_charm > 0)
        {
            DrawRect(new Rect2(Vector2.Zero, view), CombatPalette.Brecha with { A = 0.12f });
        }
    }

    /// <summary>Barra 26U (elite ×1,4) acima da cabeça, losangos das partes, nome da elite.</summary>
    private void DrawBars(Transform2D xf)
    {
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (m.Dead || m.IsDummy || m.IsBoss || m.SpawnT > 0 || (m.Hp >= m.MaxHp && !m.IsElite))
            {
                continue;
            }

            var q = xf * new Vector2((float)m.X, (float)(m.Y - (m.HitHeight * m.Def.Scale) - 8));
            var bw = MathF.Round(13 * (m.IsElite ? 1.4f : 1f));
            var x0 = q.X - (bw / 2);
            DrawRect(new Rect2(x0 - 0.5f, q.Y - 0.5f, bw + 1, 2.5f), new Color(0.03f, 0.02f, 0.05f, 1));
            DrawRect(new Rect2(x0, q.Y, bw, 1.5f), new Color(0.11f, 0.09f, 0.15f, 1));
            var frac = (float)Math.Clamp(m.Hp / m.MaxHp, 0, 1);
            DrawRect(new Rect2(x0, q.Y, bw * frac, 1.5f), CombatPalette.HpBarTop);
            for (var p = 0; p < m.Def.Parts.Count; p++)
            {
                var color = m.IsPartBroken(p) ? CombatPalette.PartBroken : CombatPalette.Brecha;
                HazardSystem.Diamond(this, new Vector2(x0 + (p * 3.5f) + 1, q.Y - 2.5f), 1.2f, color);
            }

            if (m.IsElite)
            {
                DrawString(_font, new Vector2(q.X - 60, q.Y - 4), m.Def.Name, HorizontalAlignment.Center, 120, 5, CombatPalette.Brecha);
            }
        }
    }

    private struct Floater
    {
        public Vector2 World;
        public string? Text;
        public Color Color;
        public bool Big;
        public float T;
        public float Alpha;
    }
}
