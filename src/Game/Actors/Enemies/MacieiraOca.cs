using System.Collections.Generic;
using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// Macieira Oca (Guardiã do Ato II, Pomar Podre). Lenta, enorme, três fases (limiares em <c>boss.phases</c>):
/// <list type="number">
/// <item>Lança maçãs podres: círculo âmbar onde cada uma vai cair; ao cair, explode em roxo.</item>
/// <item>Arranca as raízes e anda; alterna maçãs (2) com raízes que brotam do chão numa faixa âmbar até o caçador.</item>
/// <item>Mais rápida: 3 maçãs, leque de 3 faixas de raízes e chama Ratos-Lanterna (máx. <c>boss.maxMinions</c>).</item>
/// </list>
/// Todo golpe tem aviso âmbar (<see cref="Enemy.WarnCircle"/>/<see cref="Enemy.WarnLane"/>) de <c>telegraph</c> s.
/// O único "diálogo" dela: ao acordar, uma maçã roxa cai aos pés do caçador (ROTEIRO-V2 §13, fala 6).
/// </summary>
public partial class MacieiraOca(LevelWorld world) : Enemy("macieira_oca", world)
{
    private const float AppleRadius = 95f;
    private const float AppleFlight = 0.75f;
    private const float RootLength = 950f;
    private const float RootWidth = 110f;
    private const float FanAngle = 0.38f;
    private const float WalkStop = 260f;
    private const float AttackHold = 0.9f;

    private readonly List<(TelegraphMarker Marker, Vector2 At, float Time)> _apples = [];
    private readonly List<(TelegraphMarker Marker, Vector2 Dir)> _roots = [];
    private float _cooldown = 2.2f;
    private bool _woke;
    private bool _rootsNext;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    public override bool Unstoppable => true;

    public override float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.4f;

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
    }

    protected override Vector2 Think(float dt)
    {
        TickApples(dt);
        var target = Target;
        if (!_woke && target is not null)
        {
            _woke = true;
            // A boca de casca abre e cai uma maçã roxa aos pés do caçador.
            World.Feel.Spark(target.GlobalPosition, RotColor, 10, 120f * A, Vector2.Up, 40f);
            CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero, -4f);
        }

        UpdatePhase();
        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    ReleaseRoots();
                    Enter(EnemyState.Recover);
                }

                return Vector2.Zero;
            case EnemyState.Attack:
            case EnemyState.Recover:
                if (StateTime >= AttackHold)
                {
                    Enter(EnemyState.Idle);
                    Play("parado");
                }

                return Vector2.Zero;
        }

        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        if (_cooldown <= 0f)
        {
            _cooldown = (float)Period * (Phase == 3 ? 0.7f : 1f);
            if (Phase >= 2 && _rootsNext)
            {
                StartRoots(target);
            }
            else
            {
                ThrowApples(target);
            }

            _rootsNext = !_rootsNext;
            return Vector2.Zero;
        }

        // Fase 1 enraizada; da 2 em diante arranca as raízes e anda devagar até o caçador.
        var to = target.GlobalPosition - GlobalPosition;
        if (Phase == 1 || to.Length() < WalkStop * A)
        {
            if (Sprite.Animation != "parado")
            {
                Play("parado");
            }

            return Vector2.Zero;
        }

        if (Sprite.Animation != "andar")
        {
            Play("andar");
        }

        return to.Normalized() * (float)Speed * A * (Phase == 3 ? 1.4f : 1f);
    }

    private void UpdatePhase()
    {
        var phases = RiftSession.Brecha.Boss.Phases;
        var fraction = Hp / MaxHp;
        var next = fraction <= phases[1] ? 3 : fraction <= phases[0] ? 2 : 1;
        if (next <= Phase)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.5f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        World.Feel.Toast(Phase == 2
            ? "A Macieira Oca arranca as raízes do chão e vem andando. Saia das faixas âmbar!"
            : "A casca racha e brilha roxo. Ela chama os Ratos-Lanterna!");
        if (Phase == 3)
        {
            SummonRats(RiftSession.Brecha.Boss.SpawnPerJump);
        }
    }

    private void ThrowApples(Player target)
    {
        Enter(EnemyState.Attack);
        Play("lancar");
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -4f);
        var count = Phase;
        for (var i = 0; i < count; i++)
        {
            var spread = i == 0 ? Vector2.Zero : new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 320f * A;
            var at = World.ClampToFloor(target.GlobalPosition + spread);
            var seconds = (float)Def.Telegraph + AppleFlight + (i * 0.25f);
            _apples.Add((WarnCircle(at, AppleRadius * A, seconds), at, seconds));
        }

        if (Phase == 3)
        {
            SummonRats(1);
        }
    }

    private void TickApples(float dt)
    {
        for (var i = _apples.Count - 1; i >= 0; i--)
        {
            var (marker, at, time) = _apples[i];
            time -= dt;
            if (time > AppleFlight * 0.5f)
            {
                _apples[i] = (marker, at, time);
                continue;
            }

            // Rastro roxo caindo do alto no último trecho (a maçã chegando), depois a explosão.
            if (time > 0f)
            {
                var fall = Mathf.Clamp(time / (AppleFlight * 0.5f), 0f, 1f);
                World.Feel.Spark(at - new Vector2(0, fall * 420f * A), RotColor, 1, 30f * A);
                _apples[i] = (marker, at, time);
                continue;
            }

            marker.Release();
            _apples.RemoveAt(i);
            CombatSfx.Play(this, CombatSound.LodoLand, Vector2.Zero, -2f);
            StrikeCircle(at, AppleRadius * A, 1, 300f);
            World.Feel.Shake(0.12f);
        }
    }

    private void StartRoots(Player target)
    {
        Enter(EnemyState.Telegraph);
        Play("raizes");
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        var dir = (target.GlobalPosition - GlobalPosition).Normalized();
        var angles = Phase == 3 ? new[] { -FanAngle, 0f, FanAngle } : new[] { 0f };
        foreach (var angle in angles)
        {
            var d = dir.Rotated(angle);
            _roots.Add((WarnLane(GlobalPosition, d, RootLength * A, RootWidth * A, (float)Def.Telegraph), d));
        }
    }

    private void ReleaseRoots()
    {
        Flash.Tint = 0f;
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero);
        World.Feel.Shake(0.3f);
        foreach (var (marker, dir) in _roots)
        {
            marker.Release();
            StrikeLane(GlobalPosition, dir, RootLength * A, RootWidth * A, 1.2f, 340f);
        }

        _roots.Clear();
    }

    private void SummonRats(int count)
    {
        var alive = 0;
        foreach (var e in World.Enemies)
        {
            alive += e is RatoLanterna { Dead: false } ? 1 : 0;
        }

        var max = RiftSession.Brecha.Boss.MaxMinions;
        for (var i = 0; i < count && alive < max; i++, alive++)
        {
            var rat = EnemyFactory.Create("rato_lanterna", World);
            rat.DropsLoot = false;
            var side = i % 2 == 0 ? -1f : 1f;
            rat.Position = World.ClampToFloor(GlobalPosition + new Vector2(side * 200f * A, 120f * A));
            World.AddEnemy(rat);
            World.Feel.Spark(rat.Position, new Color("e64a8e"), 10, 200f * A, Vector2.Up, 60f);
        }
    }

    protected override void OnDied()
    {
        foreach (var (marker, _, _) in _apples)
        {
            marker.Release();
        }

        foreach (var (marker, _) in _roots)
        {
            marker.Release();
        }

        _apples.Clear();
        _roots.Clear();
        World.Feel.Shake(0.8f);
    }
}
