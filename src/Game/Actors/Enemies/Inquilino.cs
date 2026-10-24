using System.Collections.Generic;
using Godot;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// O Inquilino (Guardião do Ato VI, Ruas Invertidas): uma casa de palafita andando em quatro pernas. Três fases
/// (limiares em <c>boss.phases</c>), uma frase pela janela acesa em cada uma (ROTEIRO-V2 §13, fala 4;
/// <c>data/dialogue/ato6/inquilino.json</c>):
/// <list type="number">
/// <item>"Eu morava aqui." — anda até o caçador e esmaga: círculo âmbar de <c>telegraph</c> s na frente da casa.</item>
/// <item>"Derrubaram pra Torre." — também atira móveis pela janela: círculos âmbar onde cada um vai cair.</item>
/// <item>"Entra." — abre a porta e puxa (faixa âmbar até o caçador; quem ficar nela é arrastado até a porta) e o fogo
/// fecha a arena de fora para dentro (borda âmbar sempre visível; fora dela queima).</item>
/// </list>
/// Pernas (<c>boss.cores</c>, <c>boss.coreHp</c> de dano cada): cada perna quebrada deixa a casa mais lenta.
/// A sub-arena dentro da casa (quebrar 3 vigas) espera a arte da sala (issue #47); por ora o puxão leva até a porta.
/// </summary>
public partial class Inquilino(LevelWorld world) : Enemy("inquilino", world)
{
    private const float StompRange = 330f;
    private const float StompRadius = 170f;
    private const float StompReach = 150f;
    private const float FurnitureRadius = 90f;
    private const float FurnitureFlight = 0.7f;
    private const float PullLength = 1100f;
    private const float PullWidth = 150f;
    private const float PullSpeed = 520f;
    private const float PullSeconds = 1.3f;
    private const float DoorReach = 150f;
    private const float FireStart = 760f;
    private const float FireEnd = 260f;
    private const float FireSeconds = 40f;
    private const float FireTick = 1.1f;
    private const float AttackHold = 0.9f;
    private const float LegSlow = 0.12f;

    private static readonly Color WindowColor = new("f4e7c6");
    private static readonly Color FireColor = new("f2a541");

    private readonly List<(TelegraphMarker Marker, Vector2 At, float Time)> _furniture = [];
    private TelegraphMarker? _stompMarker;
    private Vector2 _stompAt;
    private TelegraphMarker? _pullMarker;
    private Vector2 _pullDir;
    private float _pulling;
    private float _cooldown = 1.6f;
    private int _turn;
    private double _legDamage;
    private Vector2 _fireCenter;
    private float _fireTime;
    private float _fireTick;
    private float _fireRingTimer;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    /// <summary>Pernas quebradas (0 a <c>boss.cores</c>).</summary>
    public int LegsBroken { get; private set; }

    /// <summary>Raio da área ainda sem fogo (fase 3); infinito antes.</summary>
    public float FireRadius => Phase < 3 ? float.MaxValue : Mathf.Lerp(FireStart, FireEnd, Mathf.Clamp(_fireTime / FireSeconds, 0f, 1f)) * A;

    public override bool Unstoppable => true;

    private static Vespera.Core.Rift.BossTuning Tuning => RiftSession.Load("ruas_invertidas").Boss;

    public override float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.55f;

    /// <summary>Janela acesa (a "cara" que fala), no alto da casa.</summary>
    private Vector2 Window => GlobalPosition - new Vector2(0, Sheet.StandHeight * Sheet.SpriteScale * 0.75f);

    private Vector2 Door => GlobalPosition - new Vector2(0, Sheet.StandHeight * Sheet.SpriteScale * 0.3f);

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
        _fireCenter = World.PlayArea.GetCenter();
        Speak("Eu morava aqui.");
    }

    protected override Vector2 Think(float dt)
    {
        TickFurniture(dt);
        TickFire(dt);
        UpdatePhase();
        _cooldown -= dt;
        if (_pulling > 0f)
        {
            return TickPull(dt);
        }

        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    ReleaseTelegraph();
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

        var target = Target;
        if (target is null)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && ChooseAttack(target, to.Length()))
        {
            return Vector2.Zero;
        }

        if (to.Length() < StompRange * A * 0.6f)
        {
            PlayIf("parado");
            return Vector2.Zero;
        }

        PlayIf("andar");
        var slow = 1f - (LegsBroken * LegSlow);
        return to.Normalized() * (float)Speed * A * slow * (Phase == 3 ? 1.25f : 1f);
    }

    private bool ChooseAttack(Player target, float distance)
    {
        var options = Phase switch
        {
            1 => new[] { "esmagar" },
            2 => new[] { "esmagar", "arremessar" },
            _ => new[] { "puxar", "esmagar", "arremessar" },
        };
        var pick = options[_turn % options.Length];
        if (pick == "esmagar" && distance > StompRange * A)
        {
            pick = Phase >= 2 ? "arremessar" : string.Empty;
        }

        if (pick.Length == 0)
        {
            return false;
        }

        _turn++;
        _cooldown = (float)Period * (Phase == 3 ? 0.8f : 1f);
        switch (pick)
        {
            case "esmagar":
                StartStomp(target);
                break;
            case "arremessar":
                ThrowFurniture(target);
                break;
            default:
                StartPull(target);
                break;
        }

        return true;
    }

    private void UpdatePhase()
    {
        var phases = Tuning.Phases;
        var fraction = Hp / MaxHp;
        var next = fraction <= phases[1] ? 3 : fraction <= phases[0] ? 2 : 1;
        if (next <= Phase)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.5f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        if (Phase == 2)
        {
            Story.StoryService.SetFlag("inquilino_fase_2");
            Speak("Derrubaram pra Torre.");
        }
        else
        {
            Story.StoryService.SetFlag("inquilino_fase_3");
            Speak("Entra.");
            _fireTime = 0f;
            _fireTick = FireTick;
        }
    }

    /// <summary>A casa fala pela janela acesa: a frase sobe da janela e fica no aviso da tela.</summary>
    private void Speak(string line)
    {
        Flash.Pulse(WindowColor, 0.1f, 0.5f);
        World.Feel.Number(Window, line, WindowColor, 1.6f);
        World.Feel.Toast($"A janela acende: \"{line}\"");
    }

    private void StartStomp(Player target)
    {
        Enter(EnemyState.Telegraph);
        Play("esmagar");
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        var dir = (target.GlobalPosition - GlobalPosition).Normalized();
        _stompAt = World.ClampToFloor(GlobalPosition + (dir * StompReach * A));
        _stompMarker = WarnCircle(_stompAt, StompRadius * A, (float)Def.Telegraph);
    }

    private void StartPull(Player target)
    {
        Enter(EnemyState.Telegraph);
        Play("puxar");
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        _pullDir = (target.GlobalPosition - GlobalPosition).Normalized();
        _pullMarker = WarnLane(GlobalPosition, _pullDir, PullLength * A, PullWidth * A, (float)Def.Telegraph);
    }

    private void ReleaseTelegraph()
    {
        Flash.Tint = 0f;
        if (_stompMarker is { } stomp)
        {
            stomp.Release();
            _stompMarker = null;
            CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero);
            World.Feel.Shake(0.35f);
            StrikeCircle(_stompAt, StompRadius * A, 1.3f, 380f);
            Enter(EnemyState.Recover);
            return;
        }

        if (_pullMarker is { } pull)
        {
            pull.Release();
            _pullMarker = null;
            _pulling = InLane(_pullDir) ? PullSeconds : 0f;
            World.Feel.Spark(Door, new Color("e64a8e"), 18, 260f * A);
            Enter(EnemyState.Recover);
        }
    }

    private bool InLane(Vector2 dir)
    {
        if (Target is not { } target)
        {
            return false;
        }

        var rel = target.GlobalPosition - GlobalPosition;
        var along = rel.Dot(dir);
        var across = Mathf.Abs(rel.Dot(dir.Orthogonal()));
        return along > 0f && along < PullLength * A && across < (PullWidth * A * 0.5f) + target.Combat.HitRadius;
    }

    /// <summary>Arrasta o caçador até a porta; chegando lá, a porta bate nele.</summary>
    private Vector2 TickPull(float dt)
    {
        _pulling -= dt;
        if (Target is not { } target)
        {
            _pulling = 0f;
            return Vector2.Zero;
        }

        var to = GlobalPosition - target.GlobalPosition;
        if (to.Length() <= DoorReach * A || _pulling <= 0f)
        {
            _pulling = 0f;
            CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero);
            TouchPlayer(Door, DoorReach * A, 1.1f, 420f);
            Enter(EnemyState.Recover);
            return Vector2.Zero;
        }

        target.GlobalPosition = World.ClampToFloor(target.GlobalPosition + (to.Normalized() * PullSpeed * A * dt));
        World.Feel.Spark(target.GlobalPosition, new Color("e64a8e"), 1, 80f * A);
        return Vector2.Zero;
    }

    private void ThrowFurniture(Player target)
    {
        Enter(EnemyState.Attack);
        Play("arremessar");
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -4f);
        var count = Phase + 1;
        for (var i = 0; i < count; i++)
        {
            var spread = i == 0 ? Vector2.Zero : new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 360f * A;
            var at = World.ClampToFloor(target.GlobalPosition + spread);
            var seconds = (float)Def.Telegraph + FurnitureFlight + (i * 0.2f);
            _furniture.Add((WarnCircle(at, FurnitureRadius * A, seconds), at, seconds));
        }
    }

    private void TickFurniture(float dt)
    {
        for (var i = _furniture.Count - 1; i >= 0; i--)
        {
            var (marker, at, time) = _furniture[i];
            time -= dt;
            if (time > 0f)
            {
                if (time < FurnitureFlight * 0.5f)
                {
                    var fall = time / (FurnitureFlight * 0.5f);
                    World.Feel.Spark(at - new Vector2(0, fall * 380f * A), RootColor, 1, 30f * A);
                }

                _furniture[i] = (marker, at, time);
                continue;
            }

            marker.Release();
            _furniture.RemoveAt(i);
            CombatSfx.Play(this, CombatSound.LodoLand, Vector2.Zero, -2f);
            World.Feel.Spark(at, RootColor, 14, 260f * A);
            StrikeCircle(at, FurnitureRadius * A, 1, 300f);
            World.Feel.Shake(0.1f);
        }
    }

    /// <summary>Fase 3: o fogo fecha a arena de fora para dentro. A borda âmbar fica sempre à vista; fora dela queima.</summary>
    private void TickFire(float dt)
    {
        if (Phase < 3 || Dead)
        {
            return;
        }

        _fireTime += dt;
        _fireRingTimer -= dt;
        var radius = FireRadius;
        if (_fireRingTimer <= 0f)
        {
            _fireRingTimer = 0.45f;
            World.Feel.Ring(_fireCenter, radius, FireColor, 0.5f, 7f * A);
            var angle = GD.Randf() * Mathf.Tau;
            World.Feel.Spark(_fireCenter + (Vector2.FromAngle(angle) * radius), FireColor, 4, 140f * A, Vector2.Up, 40f);
        }

        _fireTick -= dt;
        if (_fireTick > 0f)
        {
            return;
        }

        _fireTick = FireTick;
        if (Target is { } target && target.GlobalPosition.DistanceTo(_fireCenter) > radius)
        {
            var dir = (_fireCenter - target.GlobalPosition).Normalized();
            TouchPlayer(target.Combat.HitCenter, 1f, 0.35f, 0f);
            World.Feel.Spark(target.GlobalPosition, FireColor, 8, 180f * A, -dir);
        }
    }

    protected override void OnHurt(in HitInfo hit)
    {
        var legs = Tuning.Cores;
        if (LegsBroken >= legs || Tuning.CoreHp <= 0)
        {
            return;
        }

        _legDamage += hit.Damage;
        if (_legDamage < Tuning.CoreHp)
        {
            return;
        }

        _legDamage = 0;
        LegsBroken++;
        World.Feel.Shake(0.25f);
        World.Feel.Spark(GlobalPosition, RootColor, 16, 300f * A, Vector2.Up, 70f);
        World.Feel.Toast(LegsBroken < legs
            ? $"Uma perna da casa quebrou ({LegsBroken}/{legs}). Ela anda mais devagar."
            : "As quatro pernas quebraram. A casa se arrasta.");
    }

    private void PlayIf(string anim)
    {
        if (Sprite.Animation != anim)
        {
            Play(anim);
        }
    }

    protected override void OnDied()
    {
        foreach (var (marker, _, _) in _furniture)
        {
            marker.Release();
        }

        _furniture.Clear();
        _stompMarker?.Release();
        _pullMarker?.Release();
        _pulling = 0f;
        World.Feel.Shake(0.8f);
        World.Feel.Toast("A janela apaga. A casa deita no terreno.");
    }
}
