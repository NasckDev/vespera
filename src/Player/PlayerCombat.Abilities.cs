using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Rules;

namespace Vespera.Player;

/// <summary>Ecos (<c>useEco</c>, <c>combat.js:226-241</c>) e Golpes Supremos (<c>tryUlt</c>/<c>updUlt</c>, <c>extra.js:14-65</c>).</summary>
public sealed partial class PlayerCombat
{
    private readonly List<MonsterBody> _uivoHits = new(16);
    private readonly List<MonsterBody>[] _ultHits = [new(16), new(16), new(16)];
    private readonly List<MonsterBody> _dagTargets = new(UltimateNumbers.DagTargets);
    private float _uivoT;
    private Vector2 _uivoDir;
    private float _orbsT;
    private float _orbsCd;
    private int _orbsK;

    private string? _ult;
    private float _ultT;
    private int _ultStep;
    private float _ultAngle;
    private Vector2 _ultTarget;
    private float _ultCd;
    private bool _ultDone;
    private int _ultIndex;

    /// <summary>Supremo em curso (id da classe) ou null.</summary>
    public string? ActiveUltimate => _ult;

    // --- Ecos ---

    /// <summary>Usa o Eco do slot (0 = Q, 1 = segundo slot; DECISIONS B1-D41).</summary>
    public void UseEco(int slot)
    {
        var save = Game.Save;
        var ecoId = EcoSlots.Slot(save, slot);
        if (ecoId is null)
        {
            _combat.Note(save.Ecos.Count > 0 ? "Equipe um Eco no inventário (aba Ecos)." : "Nenhum Eco. Chefes às vezes deixam um ao morrer.", "i_eco");
            return;
        }

        var eco = Db.Ecos[ecoId];
        var cost = Game.EcoCost(ecoId);
        if (!_state.EcoReady)
        {
            return;
        }

        if (!_state.TrySpendEssence(cost))
        {
            _combat.Note($"Essência insuficiente ({Math.Floor(_state.Essence)}/{cost}).", "i_eco");
            return;
        }

        _state.StartEcoCooldown();
        EmitEssence();
        EmitCooldown(2 + slot, (float)PlayerCombatState.EcoCooldown, (float)PlayerCombatState.EcoCooldown);
        var p = Foot;
        _combat.Sfx("eco", p);
        _combat.Overlay.Float(p - new Vector2(0, 44), eco.Skill, CombatPalette.EcoText, true);
        var atk = Game.Stats.Atk;
        var a = Aim;
        switch (ecoId)
        {
            case "arauto":
                _orbsT = (float)EcoNumbers.VotiveDuration;
                _orbsCd = (float)EcoNumbers.VotiveFirstShot;
                _orbsK = 0;
                _combat.Glow.Burst(p - new Vector2(0, 14), 20, CombatPalette.Votive, new BurstOptions { V1 = 80 });
                break;
            case "abissal":
            {
                var target = p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (float)EcoNumbers.LureThrow;
                if (_combat.Time - _lastClick < 1)
                {
                    target = _combat.GetGlobalMousePosition() + new Vector2(0, 8);
                    var l = target.DistanceTo(p);
                    if (l > EcoNumbers.LureCursorMax)
                    {
                        target = p + ((target - p) / l * (float)EcoNumbers.LureCursorMax);
                    }
                }

                _combat.Hazards.PlayerLure(p - new Vector2(0, 12), target, EcoNumbers.LureDamage(atk));
                break;
            }

            case "ceifador":
            {
                var from = p - new Vector2(0, 12);
                _combat.Projectiles.Fire(ProjectileKind.PlayerChain, from, Vector2.FromAngle(a) * (float)EcoNumbers.ChainSpeed, (float)EcoNumbers.ChainRadius,
                    EcoNumbers.ChainDamage(atk), (float)EcoNumbers.ChainLife, CombatPalette.EcoText, origin: from);
                break;
            }

            case "uivo":
                _uivoT = (float)EcoNumbers.ChargeSeconds;
                _uivoDir = Vector2.FromAngle(a);
                _uivoHits.Clear();
                _state.GrantInvulnerability(EcoNumbers.ChargeInvulnerability);
                _combat.Shake(4);
                break;
        }
    }

    /// <summary>Investida Rubra: 0,32 s a 330 u/s, fere cada monstro uma vez a menos de <c>r + 14</c>.</summary>
    private void StepUivo(float dt)
    {
        _uivoT -= dt;
        _combat.MovePlayer(_uivoDir * (float)EcoNumbers.ChargeSpeed * dt);
        Trail(0.7f);
        var p = Foot;
        var atk = Game.Stats.Atk;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (CombatDirector.IsFoe(m) && !_uivoHits.Contains(m) && CombatMath.Hypot(m.X - p.X, m.Y - p.Y) < m.Radius + EcoNumbers.ChargeHitRadius)
            {
                _uivoHits.Add(m);
                _combat.HitMob(m, EcoNumbers.ChargeDamage(atk), p - (_uivoDir * 20), new HitOptions(EcoNumbers.ChargePart, 0, EcoNumbers.ChargeKnockback));
            }
        }

        _combat.Glow.Burst(p - new Vector2(0, 8), 2, CombatPalette.Charge, new BurstOptions { V1 = 30 });
    }

    /// <summary>Lanternas Votivas: a cada 0,55 s a próxima lanterna atira no monstro mais próximo a 110 u.</summary>
    private void StepVotive(float dt)
    {
        if (_orbsT <= 0)
        {
            return;
        }

        _orbsT -= dt;
        _orbsCd -= dt;
        if (_orbsCd > 0)
        {
            return;
        }

        _orbsCd = (float)EcoNumbers.VotiveInterval;
        var oi = _orbsK++ % 3;
        var oa = (_combat.Time * 3) + (oi * 2.094f);
        var p = Foot;
        var origin = new Vector2(p.X + (MathF.Cos(oa) * 22), p.Y - 14 + (MathF.Sin(oa) * 12));
        MonsterBody? target = null;
        var best = EcoNumbers.VotiveRange;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (!CombatDirector.IsFoe(m))
            {
                continue;
            }

            var d = CombatMath.Hypot(m.X - origin.X, m.Y - (m.HitHeight * 0.5) - origin.Y);
            if (d < best)
            {
                best = d;
                target = m;
            }
        }

        if (target is null)
        {
            return;
        }

        var ta = MathF.Atan2((float)(target.Y - (target.HitHeight * 0.5) - origin.Y), (float)(target.X - origin.X));
        _combat.Projectiles.Fire(ProjectileKind.PlayerBolt, origin, Vector2.FromAngle(ta) * (float)EcoNumbers.VotiveBoltSpeed, (float)EcoNumbers.VotiveBoltRadius,
            EcoNumbers.VotiveDamage(Game.Stats.Atk), (float)EcoNumbers.VotiveBoltLife, CombatPalette.Cyan, part: (float)EcoNumbers.VotivePart);
    }

    // --- Golpe Supremo ---

    /// <summary><c>tryUlt</c>: recarga da classe, hitstop 0,12, tremor ≥ 6, e o golpe começa.</summary>
    public void TryUltimate()
    {
        if (Dead || _combat.World.IsTransitioning)
        {
            return;
        }

        var cls = Game.Save.ClassId;
        var ult = Db.Ultimates[cls];
        if (!_state.UltimateReady)
        {
            _combat.Note($"{ult.Name} recarrega em {Math.Ceiling(_state.UltCooldown)} s.", "i_alert");
            return;
        }

        if (_ult is not null)
        {
            return;
        }

        _state.StartUltimateCooldown(ult.Cooldown);
        EmitCooldown(4, (float)ult.Cooldown, (float)ult.Cooldown);
        _combat.Clock.Set(UltimateNumbers.Hitstop);
        _combat.Shake(UltimateNumbers.Shake);
        var p = Foot;
        _combat.Sfx("eco", p);
        _combat.Sfx("boom", p);
        if (_combat.Brecha.InBrecha)
        {
            Game.CountQuest(QuestCounters.Ults);
        }

        var color = CombatPalette.Of(ult.Color);
        _combat.Overlay.Float(p - new Vector2(0, 48), ult.Name, color, true);
        _combat.Glow.Burst(p - new Vector2(0, 14), 30, new[] { color, Colors.White, CombatPalette.Ambar }, new BurstOptions { V1 = 150 });
        _ult = cls;
        _ultT = 0;
        _ultStep = 0;
        _ultAngle = Aim;
        _ultTarget = UltimateTarget();
        _ultCd = 0;
        _ultDone = false;
        _ultIndex = 0;
        foreach (var list in _ultHits)
        {
            list.Clear();
        }

        if (cls == "dag")
        {
            PickDaggerTargets();
            _ultCd = (float)UltimateNumbers.DagFirst;
            _state.GrantInvulnerability(UltimateNumbers.DagInvulnerability);
        }

        GameManager.Instance.SaveGame();
    }

    /// <summary><c>ultTarget</c>: o cursor se o mouse mexeu há menos de 2,5 s (até 150 u), senão 90×70 u na mira.</summary>
    private Vector2 UltimateTarget()
    {
        var p = Foot;
        if (_combat.Time - _mouseMoved < UltimateNumbers.CursorRecentSeconds)
        {
            var t = _combat.GetGlobalMousePosition() + new Vector2(0, 8);
            var l = t.DistanceTo(p);
            return l > UltimateNumbers.CursorMax ? p + ((t - p) / l * (float)UltimateNumbers.CursorMax) : t;
        }

        return p + new Vector2(MathF.Cos(Aim) * (float)UltimateNumbers.AimDistanceX, MathF.Sin(Aim) * (float)UltimateNumbers.AimDistanceY);
    }

    private void PickDaggerTargets()
    {
        _dagTargets.Clear();
        var p = Foot;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            if (CombatDirector.IsFoe(enemies[i].Body))
            {
                _dagTargets.Add(enemies[i].Body);
            }
        }

        _dagTargets.Sort((a, b) => CombatMath.Hypot(a.X - p.X, a.Y - p.Y).CompareTo(CombatMath.Hypot(b.X - p.X, b.Y - p.Y)));
        if (_dagTargets.Count > UltimateNumbers.DagTargets)
        {
            _dagTargets.RemoveRange(UltimateNumbers.DagTargets, _dagTargets.Count - UltimateNumbers.DagTargets);
        }
    }

    private void EndUltimate()
    {
        _ult = null;
        _dagTargets.Clear();
    }

    private void StepUltimate(float dt)
    {
        if (_ult is null || Dead)
        {
            return;
        }

        _ultT += dt;
        var atk = Game.Stats.Atk;
        var el = Game.Stats.Element;
        var color = CombatPalette.Of(Db.Ultimates[_ult].Color);
        var enemies = _combat.Enemies;
        var p = Foot;
        switch (_ult)
        {
            case "sword":
            {
                _state.GrantInvulnerability(UltimateNumbers.SwordInvulnerability);
                var seg = (int)(_ultT / UltimateNumbers.SwordSegment);
                if (seg > _ultStep)
                {
                    _ultStep = seg;
                    _ultHits[0].Clear();
                    _ultAngle = Aim + (seg % 2 == 1 ? (float)UltimateNumbers.SwordSwing : -(float)UltimateNumbers.SwordSwing);
                    _combat.Sfx("swing", p);
                    _combat.Shake(4);
                    _combat.Fx.Arc(p - new Vector2(0, 11), _ultAngle, 3.4f, 46, 0.22f, color, 7, seg % 2 == 1);
                }

                if (_ultStep < UltimateNumbers.SwordDashes)
                {
                    _combat.MovePlayer(Vector2.FromAngle(_ultAngle) * (float)UltimateNumbers.SwordSpeed * dt);
                    Trail(0.8f);
                    p = Foot;
                    for (var i = 0; i < enemies.Count; i++)
                    {
                        var m = enemies[i].Body;
                        if (CombatDirector.IsFoe(m) && !_ultHits[0].Contains(m)
                            && CombatMath.Hypot(m.X - p.X, m.Y - (0.4 * m.HitHeight) - p.Y + 10) < m.Radius + UltimateNumbers.SwordHitRadius)
                        {
                            _ultHits[0].Add(m);
                            _combat.HitMob(m, UltimateNumbers.SwordDamage(atk), new Vector2(p.X - (MathF.Cos(_ultAngle) * 20), p.Y), new HitOptions(1.4, 0, 200, el));
                        }
                    }
                }

                if (_ultT > UltimateNumbers.SwordEnd)
                {
                    EndUltimate();
                }

                break;
            }

            case "bow":
                _ultCd -= dt;
                if (_ultCd <= 0 && _ultT < UltimateNumbers.BowRainSeconds)
                {
                    _ultCd = (float)UltimateNumbers.BowInterval;
                    var r = _combat.FxRandom;
                    var at = _ultTarget + new Vector2((float)r.Range(-UltimateNumbers.BowSpreadX, UltimateNumbers.BowSpreadX), (float)r.Range(-UltimateNumbers.BowSpreadY, UltimateNumbers.BowSpreadY));
                    _combat.Hazards.RainArrow(at, UltimateNumbers.BowDamage(atk));
                    if (r.NextDouble() < 0.4)
                    {
                        _combat.Sfx("bow", at);
                    }
                }

                if (_ultT > UltimateNumbers.BowEnd)
                {
                    EndUltimate();
                }

                break;
            case "staff":
                if (_ultT < UltimateNumbers.StaffPullSeconds)
                {
                    for (var i = 0; i < enemies.Count; i++)
                    {
                        var m = enemies[i].Body;
                        if (!CombatDirector.IsFoe(m))
                        {
                            continue;
                        }

                        var dx = _ultTarget.X - m.X;
                        var dy = _ultTarget.Y - m.Y;
                        var d = CombatMath.Hypot(dx, dy);
                        if (d < UltimateNumbers.StaffPullRadius && d > 6)
                        {
                            var sp = m.IsBoss ? UltimateNumbers.StaffPullSpeedBoss : UltimateNumbers.StaffPullSpeed;
                            _combat.Move(m, dx / d * sp * dt, dy / d * sp * dt);
                        }
                    }

                    if (_combat.FxRandom.NextDouble() < 0.7)
                    {
                        var at = _ultTarget + new Vector2((float)_combat.FxRandom.Range(-60, 60), (float)_combat.FxRandom.Range(-40, 40));
                        _combat.Glow.Burst(at, 1, CombatPalette.OrbTrail, new BurstOptions { V1 = 20, Gravity = 0 });
                    }
                }
                else if (!_ultDone)
                {
                    _ultDone = true;
                    _combat.Shake(10);
                    _combat.Sfx("boom", _ultTarget);
                    _combat.Fx.Ring(_ultTarget, 6, 80, 0.4f, CombatPalette.Orb with { A = 0.95f });
                    _combat.Fx.Ring(_ultTarget, 4, 60, 0.4f, CombatPalette.Creme with { A = 0.9f }, 0.08f);
                    _combat.Glow.Burst(_ultTarget, 60, CombatPalette.OrbBoom, new BurstOptions { V1 = 220, L1 = 0.9f });
                    for (var i = 0; i < enemies.Count; i++)
                    {
                        var m = enemies[i].Body;
                        if (CombatDirector.IsFoe(m) && CombatMath.Hypot(m.X - _ultTarget.X, m.Y - _ultTarget.Y) < UltimateNumbers.StaffBlastRadius + m.Radius)
                        {
                            _combat.HitMob(m, UltimateNumbers.StaffDamage(atk), _ultTarget, new HitOptions(1.6, 0, 120, el));
                            m.Stun = m.IsBoss ? UltimateNumbers.StaffStunBoss : UltimateNumbers.StaffStun;
                        }
                    }
                }

                if (_ultT > UltimateNumbers.StaffEnd)
                {
                    EndUltimate();
                }

                break;
            case "hammer":
            {
                var wave = (int)(_ultT / UltimateNumbers.HammerWaveEvery);
                if (wave < UltimateNumbers.HammerWaves && wave >= _ultStep)
                {
                    _ultStep = wave + 1;
                    _combat.Shake(8);
                    _combat.Sfx("boom", p);
                    _combat.Fx.Ring(p, 6, 90, 0.45f, color with { A = 0.95f });
                    _combat.Fx.Burst(p, 30, CombatPalette.Smash, new BurstOptions { V1 = 170, Up = 40, Gravity = 200 });
                }

                for (var w = 0; w < UltimateNumbers.HammerWaves; w++)
                {
                    if (UltimateNumbers.HammerWaveRadiusAt(w, _ultT) is not { } r)
                    {
                        continue;
                    }

                    for (var i = 0; i < enemies.Count; i++)
                    {
                        var m = enemies[i].Body;
                        if (!CombatDirector.IsFoe(m) || _ultHits[w].Contains(m))
                        {
                            continue;
                        }

                        var d = CombatMath.Hypot(m.X - p.X, (m.Y - p.Y) * 1.3);
                        if (Math.Abs(d - r) < UltimateNumbers.HammerBand)
                        {
                            _ultHits[w].Add(m);
                            _combat.HitMob(m, UltimateNumbers.HammerDamage(atk), p, new HitOptions(2, 0, 140, el));
                            m.Stun = Math.Max(m.Stun, UltimateNumbers.HammerStun);
                        }
                    }
                }

                if (_ultT > UltimateNumbers.HammerEnd)
                {
                    EndUltimate();
                }

                break;
            }

            case "dag":
                _ultCd -= dt;
                if (_ultCd <= 0)
                {
                    _ultCd = (float)UltimateNumbers.DagEvery;
                    var m = _ultIndex < _dagTargets.Count ? _dagTargets[_ultIndex] : null;
                    _ultIndex++;
                    if (m is { Dead: false })
                    {
                        var behind = new Vector2((float)(m.X - (m.Face * UltimateNumbers.DagBehind)), (float)m.Y);
                        if (_combat.CanStand(behind.X, behind.Y, 4, false))
                        {
                            Player.GlobalPosition = behind;
                        }

                        Aim = m.Face > 0 ? 0 : MathF.PI;
                        Player.Aim = Aim;
                        var at = Foot;
                        _combat.Fx.Arc(at - new Vector2(0, 11), Aim, 2.4f, 30, 0.16f, color, 5, false);
                        _combat.Glow.Burst(at - new Vector2(0, 12), 14, new[] { color, new Color("#2a1a35") }, new BurstOptions { V1 = 90 });
                        _combat.HitMob(m, UltimateNumbers.DagDamage(atk), new Vector2((float)(m.X - (m.Face * 20)), (float)m.Y), new HitOptions(1.2, UltimateNumbers.DagBackstab, 60, el));
                        _combat.Sfx("hit", at);
                    }

                    if (_ultIndex >= _dagTargets.Count || _ultIndex >= UltimateNumbers.DagTargets)
                    {
                        EndUltimate();
                    }
                }

                if (_ultT > UltimateNumbers.DagEnd)
                {
                    EndUltimate();
                }

                break;
        }
    }

    /// <summary>Área do Supremo do Arco (elipse 58×42) e a estrela do Cajado (<c>drawAir</c> de <c>extra.js</c>).</summary>
    private void DrawUltimate(FxLayer layer, float time)
    {
        if (_ult == "bow")
        {
            layer.DrawSetTransform(_ultTarget, 0, new Vector2(1, 42f / 58f));
            layer.DrawArc(Vector2.Zero, 58, 0, MathF.Tau, 40, CombatPalette.Ambar with { A = 0.7f }, 1.2f);
            layer.DrawSetTransform(Vector2.Zero);
        }
        else if (_ult == "staff" && !_ultDone)
        {
            var r = 10 + (MathF.Sin(time * 12) * 2);
            layer.DrawTextureRect(_glow, new Rect2(_ultTarget - new Vector2(50, 50), new Vector2(100, 100)), false, CombatPalette.Brecha with { A = 0.5f });
            layer.DrawTextureRect(_glow, new Rect2(_ultTarget - new Vector2(r * 2, r * 2), new Vector2(r * 4, r * 4)), false, CombatPalette.Ambar);
            var er = (80 * (1 - (_ultT / (float)UltimateNumbers.StaffPullSeconds))) + 10;
            layer.DrawSetTransform(_ultTarget, 0, new Vector2(1, 0.7f));
            layer.DrawArc(Vector2.Zero, MathF.Max(1, er), 0, MathF.Tau, 40, CombatPalette.Orb with { A = 0.6f }, 1);
            layer.DrawSetTransform(Vector2.Zero);
        }
    }
}
