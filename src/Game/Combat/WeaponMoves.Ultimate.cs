using System.Linq;
using Godot;
using Vespera.Core.Combat;
using Vespera.Game.Actors;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>
/// Golpe Supremo (issue #80): um por arma, com os números de <see cref="UltimateNumbers"/> (<c>extra.js</c>) e nome, recarga e cor
/// de <c>ultimates.json</c>. Lâmina: Fenda Rubra (três investidas cortantes, invulnerável). Arco: Chuva de Vésperas (chuva de
/// flechas na área à frente). Martelo: Terremoto da Forja (três ondas de choque que ferem e atordoam). Só usa efeitos que o
/// jogo já tem (corte em setor/círculo, anel, faíscas, atordoar). Os números do protótipo são de um caçador de ~24 px; no v2
/// o alcance do golpe da Lâmina é 120 px (48 no protótipo), por isso <see cref="ReachScale"/>.
/// </summary>
public abstract partial class WeaponMoves
{
    /// <summary>Distâncias do protótipo (px) para o v2: alcance da Lâmina 120 px / 48 px.</summary>
    private const float ReachScale = 2.5f;

    private const float BowOpening = 0.6f;
    private const float BowAimReach = 70f;
    private const float SwordAssistReach = 160f;
    private const float SwordSlashRange = 110f;
    private const float SwordSlashArc = 2.6f;
    private const float BossStunFactor = 0.5f;

    private float _ultTime;
    private float _ultDuration;
    private int _ultStep;
    private float _ultRain;
    private Vector2 _ultCenter;

    /// <summary>O Golpe Supremo está em curso (o caçador não responde a mais nada até ele acabar).</summary>
    public bool UltimateActive { get; private set; }

    /// <summary>Solta o Golpe Supremo da arma. False se já está em um ou se a arma está no meio de um golpe.</summary>
    public bool BeginUltimate(Vector2 move)
    {
        if (UltimateActive || Busy)
        {
            return false;
        }

        var steering = move.LengthSquared() > 0.04f;
        Aim = MouseAim() ?? (steering ? move.Normalized() : P.AimDirection);
        P.Face(Aim);
        _ultTime = 0f;
        _ultStep = 0;
        _ultRain = 0f;
        UltimateActive = true;
        var color = UltimateColor();
        P.Flash.Pulse(color, 0.06f, 0.25f, 0.7f);
        P.World?.Feel.Ring(P.GlobalPosition, 90f * S, color, 0.5f, 8f);
        P.World?.Feel.Shake(0.5f);
        CombatSfx.Play(P, CombatSound.Slam, Vector2.Zero);
        switch (WeaponId)
        {
            case "sword":
                _ultDuration = (UltimateNumbers.SwordDashes * (float)UltimateNumbers.SwordSegment) + (float)UltimateNumbers.SwordEnd;
                C.GrantInvulnerable(_ultDuration);
                StartSwordDash(move);
                break;
            case "bow":
                _ultDuration = (float)UltimateNumbers.BowEnd;
                C.GrantInvulnerable(BowOpening);
                _ultCenter = P.GlobalPosition + (Aim * BowAimReach * ReachScale * S);
                _ultCenter = P.World?.ClampToFloor(_ultCenter) ?? _ultCenter;
                P.PlayAnimation("tiro", restart: true);
                break;
            default:
                _ultDuration = ((UltimateNumbers.HammerWaves - 1) * (float)UltimateNumbers.HammerWaveEvery) + (float)UltimateNumbers.HammerEnd;
                C.GrantInvulnerable(_ultDuration * 0.5f);
                P.PlayAnimation("pesado", restart: true);
                break;
        }

        return true;
    }

    /// <summary>Um quadro do Golpe Supremo (só chamado enquanto <see cref="UltimateActive"/>).</summary>
    public MotionOrder TickUltimate(float dt, Vector2 move)
    {
        var order = WeaponId switch
        {
            "sword" => TickSword(move),
            "bow" => TickBow(dt),
            _ => TickHammer(),
        };
        _ultTime += dt;
        if (_ultTime >= _ultDuration)
        {
            EndUltimate();
        }

        return order;
    }

    private void EndUltimate()
    {
        if (!UltimateActive)
        {
            return;
        }

        UltimateActive = false;
        P.Flash.Tint = 0f;
        P.Sprite.SpeedScale = 1f;
    }

    /// <summary>Dano do Supremo como múltiplo do ataque, com a bênção de dano do Supremo da caçada.</summary>
    private double UltMult(double damage) => damage / System.Math.Max(1, C.Stats.Atk) * (1 + (Hunter.HuntService.Bless.UltDamagePct / 100));

    private Color UltimateColor() =>
        RiftSession.Db.Ultimates.TryGetValue(WeaponId, out var def) ? new Color(def.Color) : new Color("ffb347");

    /// <summary>Lâmina: cada investida avança <c>SwordSpeed</c> por <c>SwordSegment</c> e corta ao fim; entre elas mira de novo.</summary>
    private MotionOrder TickSword(Vector2 move)
    {
        var segment = (float)UltimateNumbers.SwordSegment;
        var done = Mathf.Min((int)(_ultTime / segment), UltimateNumbers.SwordDashes);
        while (_ultStep < done)
        {
            SwordSlash(_ultStep);
            _ultStep++;
            if (_ultStep < UltimateNumbers.SwordDashes)
            {
                StartSwordDash(move);
            }
        }

        if (_ultTime >= UltimateNumbers.SwordDashes * segment)
        {
            return new MotionOrder(0f, null, true);
        }

        return new MotionOrder(0f, Aim * (float)UltimateNumbers.SwordSpeed * ReachScale * S, true);
    }

    private void StartSwordDash(Vector2 move)
    {
        var steering = move.LengthSquared() > 0.04f;
        var mouse = MouseAim();
        var aim = mouse ?? (steering ? move.Normalized() : Aim);
        Aim = Assist(aim, SwordAssistReach * S, steering || mouse is not null ? AssistAngle : Mathf.Pi);
        P.Face(Aim);
        P.PlayAnimation($"ataque{(_ultStep % 3) + 1}", restart: true);
        P.Sprite.SpeedScale = 2f;
    }

    private void SwordSlash(int index)
    {
        var range = SwordSlashRange * S;
        var mult = UltMult(UltimateNumbers.SwordDamage(C.Stats.Atk));
        Strike(Chest, Aim, range, SwordSlashArc, mult, 220f, UltimateNumbers.Hitstop, heavy: true);
        P.World?.Feel.Ring(P.GlobalPosition + (Aim * range * 0.4f), range * 0.55f, UltimateColor(), 0.25f, 6f);
        CombatSfx.Play(P, index == UltimateNumbers.SwordDashes - 1 ? CombatSound.SwingHeavy : CombatSound.Swing, Vector2.Zero);
    }

    /// <summary>Arco: chuva de flechas na área marcada à frente por <c>BowRainSeconds</c>; o caçador fica quase parado.</summary>
    private MotionOrder TickBow(float dt)
    {
        var world = P.World;
        if (world is null || _ultTime >= (float)UltimateNumbers.BowRainSeconds)
        {
            return new MotionOrder(0f, null, true);
        }

        if (_ultTime < dt)
        {
            world.Feel.Ring(_ultCenter, (float)UltimateNumbers.BowSpreadX * ReachScale * S, UltimateColor(), 0.9f, 5f);
        }

        _ultRain -= dt;
        while (_ultRain <= 0f)
        {
            _ultRain += (float)UltimateNumbers.BowInterval;
            var angle = GD.Randf() * Mathf.Tau;
            var radius = Mathf.Sqrt(GD.Randf());
            var offset = new Vector2(Mathf.Cos(angle) * (float)UltimateNumbers.BowSpreadX, Mathf.Sin(angle) * (float)UltimateNumbers.BowSpreadY) * radius * ReachScale * S;
            var at = _ultCenter + offset;
            var mult = UltMult(UltimateNumbers.BowDamage(C.Stats.Atk));
            Strike(at, Vector2.Down, (float)UltimateNumbers.BowHitRadius * ReachScale * S, 0f, mult, 60f, 0.03, heavy: false);
            world.Feel.Spark(at, UltimateColor(), 4, 160f, Vector2.Up, 80f);
        }

        return new MotionOrder(0.25f, null, true);
    }

    /// <summary>Martelo: três ondas de choque saindo do caçador, uma a cada <c>HammerWaveEvery</c>; ferem e atordoam.</summary>
    private MotionOrder TickHammer()
    {
        var due = Mathf.Min((int)(_ultTime / (float)UltimateNumbers.HammerWaveEvery) + 1, UltimateNumbers.HammerWaves);
        while (_ultStep < due)
        {
            _ultStep++;
            Shockwave();
        }

        return new MotionOrder(0f, null, true);
    }

    private void Shockwave()
    {
        var world = P.World;
        if (world is null)
        {
            return;
        }

        var radius = (float)UltimateNumbers.HammerWaveMax * ReachScale * S;
        var center = P.GlobalPosition;
        var mult = UltMult(UltimateNumbers.HammerDamage(C.Stats.Atk));
        Strike(center - new Vector2(0, 20f * S), Aim, radius, 0f, mult, 380f, UltimateNumbers.Hitstop, heavy: true);
        foreach (var enemy in world.Enemies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Dead || enemy.HitCenter.DistanceTo(center) > radius + enemy.HitRadius)
            {
                continue;
            }

            var stun = (float)UltimateNumbers.HammerStun;
            enemy.Stun(enemy.Def.Boss ? stun * BossStunFactor : stun);
        }

        world.Feel.Ring(center, radius, UltimateColor(), 0.45f, 8f);
        world.Feel.Ring(center, radius * 0.6f, new Color("fff1c4"), 0.3f, 5f);
        world.Feel.Spark(center, new Color("c9a27a"), 22, 380f, Vector2.Up, 160f);
        world.Feel.Shake(0.75f);
        CombatSfx.Play(P, CombatSound.Slam, Vector2.Zero);
    }
}
