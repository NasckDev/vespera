using System;
using Godot;
using Vespera.Core.Combat.Ai;

namespace Vespera.Combat;

/// <summary>Telegraphs no chão e porta da Brecha (<c>drawGround</c>, <c>render.js:109-132</c>).</summary>
public partial class CombatDirector
{
    private void DrawGround(FxLayer g)
    {
        var pulse = 0.55f + (0.45f * MathF.Sin(_time * 18));
        Brecha.DrawDoor(g, _time);
        for (var i = 0; i < _enemies.Count; i++)
        {
            var m = _enemies[i].Body;
            if (m.Dead || m.IsDummy || m.SpawnT > 0)
            {
                continue;
            }

            var t = m.T;
            var foot = new Vector2((float)m.X, (float)m.Y);
            var hy = new Vector2(foot.X, foot.Y - 4);
            var aim = (float)Math.Atan2(m.Ldy, m.Ldx);
            switch (m.State)
            {
                case MonsterState.Wind:
                    Band(g, hy, aim, m.IsElite ? 78 : 58, (float)(m.Radius * 1.3), pulse);
                    break;
                case MonsterState.Bite when t < 0.65:
                    Band(g, hy, aim, 100, (float)(m.Radius * 1.2), pulse);
                    break;
                case MonsterState.Tide when t < 0.9:
                    HazardSystem.Telegraph(g, foot, (float)(115 * (t / 0.9)), pulse, 0.4f, CombatPalette.TelegraphFill);
                    break;
                case MonsterState.Swipe when t < 0.45:
                    g.DrawSector(foot - new Vector2(0, 6), aim, 2.2f, 46, CombatPalette.TelegraphFill with { A = pulse * 0.3f }, CombatPalette.TelegraphLine with { A = pulse });
                    break;
                case MonsterState.Chain when t < 0.7:
                    Band(g, new Vector2(foot.X, (float)(m.Y - (m.HitHeight * 0.6))), aim, 240, 4, pulse);
                    break;
                case MonsterState.Slam when t < 0.75:
                    HazardSystem.Telegraph(g, new Vector2(foot.X + (m.Face * 8), foot.Y), 38, pulse, (float)(t / 0.75), CombatPalette.SlamFill);
                    break;
                case MonsterState.Sweep when t < 0.7:
                    g.DrawSector(foot - new Vector2(0, 10), aim, 3f, 66, CombatPalette.TelegraphFill with { A = pulse * 0.3f }, CombatPalette.TelegraphLine with { A = pulse });
                    break;
                case MonsterState.Charge when t < 0.6:
                    Band(g, hy, aim, 130, (float)(m.Radius * 1.2), pulse);
                    break;
            }
        }
    }

    private static void Band(FxLayer g, Vector2 from, float angle, float length, float width, float alpha) =>
        g.DrawBand(from, angle, length, width, CombatPalette.TelegraphFill with { A = alpha * 0.35f }, CombatPalette.TelegraphLine with { A = alpha });

    /// <summary>Camada de cima: perigos no ar e extras do jogador (carga do arco, lanternas votivas).</summary>
    private void DrawAir(FxLayer layer)
    {
        Hazards.DrawAir(layer);
        PlayerCombat.DrawAir(layer, _time);
        DrawDangerIcons(layer);
    }

    private static readonly Vector2[] Triangle = new Vector2[3];
    private static readonly Color[] TriangleColor = new Color[1];

    /// <summary>O monstro está mostrando o aviso de golpe no chão (as mesmas janelas do <see cref="DrawGround"/>).</summary>
    public static bool TelegraphActive(MonsterBody m) => !m.Dead && !m.IsDummy && m.SpawnT <= 0 && m.State switch
    {
        MonsterState.Wind => true,
        MonsterState.Bite => m.T < 0.65,
        MonsterState.Tide => m.T < 0.9,
        MonsterState.Swipe => m.T < 0.45,
        MonsterState.Chain => m.T < 0.7,
        MonsterState.Slam => m.T < 0.75,
        MonsterState.Sweep => m.T < 0.7,
        MonsterState.Charge => m.T < 0.6,
        _ => false,
    };

    /// <summary>
    /// C1-D11: aviso de perigo que não depende só da cor — um triângulo com "!" sobre a cabeça de quem prepara um
    /// golpe, junto do desenho no chão. Maior e com contorno grosso no alto contraste. Sem alocação (arrays fixos).
    /// </summary>
    private void DrawDangerIcons(FxLayer g)
    {
        var contrast = Autoload.GameSettings.HighContrast;
        var r = contrast ? 7.5f : 5.5f;
        var bob = MathF.Sin(_time * 14) * 0.8f;
        for (var i = 0; i < _enemies.Count; i++)
        {
            var m = _enemies[i].Body;
            if (!TelegraphActive(m))
            {
                continue;
            }

            var c = new Vector2((float)m.X, (float)(m.Y - (m.HitHeight * m.Def.Scale)) - 14f - r + bob);
            Triangle[0] = c + new Vector2(0, -r);
            Triangle[1] = c + new Vector2(r * 1.05f, r * 0.8f);
            Triangle[2] = c + new Vector2(-r * 1.05f, r * 0.8f);
            TriangleColor[0] = CombatPalette.TelegraphLine;
            g.DrawPolygon(Triangle, TriangleColor);
            var ink = new Color(0.08f, 0.04f, 0.1f);
            g.DrawPolyline(Triangle, ink, contrast ? 1.6f : 1f);
            g.DrawLine(Triangle[2], Triangle[0], ink, contrast ? 1.6f : 1f);
            g.DrawRect(new Rect2(c.X - 0.75f, c.Y - (r * 0.45f), 1.5f, r * 0.7f), ink);
            g.DrawRect(new Rect2(c.X - 0.75f, c.Y + (r * 0.4f), 1.5f, 1.5f), ink);
        }
    }
}
