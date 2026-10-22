using System;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Data;
using Vespera.Core.State;
using Vespera.World;

namespace Vespera.Player;

/// <summary>
/// Combate do jogador (<c>updPlayer</c>, <c>combat.js:43-142</c>; spec 02 §3.2-3.3, §3.5-3.8, §3.16) sobre o estado
/// puro <see cref="PlayerCombatState"/>: mira (mouse, analógico direito ou direção do movimento), esquiva com energia,
/// as 5 armas da v2, poção, troca de arma (a classe trava), Ecos em 2 slots e o Golpe Supremo da classe. O
/// <see cref="Player"/> continua dono do movimento; aqui só se usam os ganchos dele (<c>SpeedMultiplier</c>,
/// <c>MovementLocked</c>, <c>AttackPose</c>, <c>Aim</c>). Avançado pelo <see cref="CombatDirector"/> a cada frame de física.
/// </summary>
public sealed partial class PlayerCombat : IDisposable
{
    private const float KnockbackDecay = 0.0005f;
    private const float KnockbackWindow = 0.5f;
    private const float MouseAimWindow = 0.5f;
    private const float EnergyEmitStep = 5f;

    private static readonly string[] CooldownIds = ["dash", "potion", "eco0", "eco1", "ultimate"];

    private readonly CombatDirector _combat;
    private readonly Texture2D _glow;
    private readonly bool[] _cooldownReady = [true, true, true, true, false];
    private readonly ShaderMaterial _flash;
    private PlayerCombatState _state;
    private Vector2 _dashDir = Vector2.Down;
    private Vector2 _knock;
    private Vector2 _pullTarget;
    private float _pullT;
    private float _lastHurt = -99;
    private float _lastClick = -9;
    private float _mouseMoved = -9;
    private float? _moveAngle;
    private float _deadT;
    private float _regenCarry;
    private float _lastEnergy = -1;
    private float _lastFlash = -1;
    private float _potionCd;

    public PlayerCombat(CombatDirector combat, Texture2D glow)
    {
        _combat = combat;
        _glow = glow;
        _state = new PlayerCombatState(GameManager.Db.UltimateData.InitialCooldown);
        _flash = FlashMaterial.Create();
        Player.Sprite.Material = _flash;
    }

    public PlayerCombatState State => _state;

    public bool Dead { get; private set; }

    /// <summary>Ângulo de mira atual (rad, 0 = direita).</summary>
    public float Aim { get; private set; } = MathF.PI / 2;

    private bool _bowLatched;

    private Vector2 _lastHitAt;

    /// <summary>Tom do "hit" no golpe final do combo (POLI-15): 0,8 = quase uma terça menor, soa pesado sem virar outro som.</summary>
    public const float HitPitchBig = 0.8f;

    /// <summary>Arco carregando por alternância (smoke test).</summary>
    public bool BowLatched => _bowLatched;

    /// <summary>Investida Rubra (Eco do Alfa) em curso: imune e sem outras ações.</summary>
    public bool ChargingEco => _uivoT > 0;

    private Player Player => _combat.Player;

    private GameState Game => _combat.State;

    private GameDatabase Db => _combat.Db;

    private WeaponDef Weapon => Db.Weapons[Game.Save.CurrentWeapon];

    private Vector2 Foot => Player.GlobalPosition;

    public void Dispose()
    {
        // Sem sinais C# próprios: o diretor desconecta o EventBus.
    }

    /// <summary>Jogo novo ou save carregado: estado de combate zerado e visual da classe.</summary>
    public void OnGameLoaded()
    {
        _state = new PlayerCombatState(Db.UltimateData.InitialCooldown);
        Dead = false;
        Player.ClassId = Game.Save.ClassId;
        Player.MovementLocked = false;
        Player.Body.Rotation = 0;
        EndUltimate();
        _uivoT = 0;
        _orbsT = 0;
        Emit(EventBus.SignalName.WeaponChanged, Game.Save.CurrentWeapon);
        EmitEssence();
        EmitEnergy(force: true);
        EmitCooldown(4, (float)_state.UltCooldown, (float)Db.UltimateData.InitialCooldown);
    }

    /// <summary>Troca de mapa (<c>goMap</c>): sem empurrão, puxão, carga, windup nem Supremo em curso.</summary>
    public void OnMapChanged()
    {
        _knock = Vector2.Zero;
        _bowLatched = false;
        _pullT = 0;
        _uivoT = 0;
        _state.CancelActions();
        EndUltimate();
    }

    public void HandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _lastClick = _combat.Time;
        }
        else if (@event is InputEventMouseMotion)
        {
            _mouseMoved = _combat.Time;
        }
    }

    /// <summary>Gameplay bloqueado (UI/pausa): nada anda. A carga alternada do arco termina (POLI-14).</summary>
    public void Frozen() => ReleaseBowLatch();

    /// <summary>
    /// POLI-14: a carga do arco no modo "alternar" só vale enquanto o jogador tem o controle; ao pausar, abrir diálogo, morrer,
    /// trocar de mapa ou sofrer um Supremo/transição ela é cancelada (sem soltar flecha), em vez de voltar armada depois.
    /// </summary>
    private void ReleaseBowLatch()
    {
        if (_bowLatched)
        {
            _bowLatched = false;
            _state.CancelActions();
        }
    }

    public void Step(float dt)
    {
        var stats = Game.Stats;
        _state.TickTimers(dt);
        _potionCd = MathF.Max(0, _potionCd - dt);
        if (Dead)
        {
            ReleaseBowLatch();
            _deadT += dt;
            Player.MovementLocked = true;
            Player.Body.Rotation = -MathF.PI / 2 * MathF.Min(1, _deadT * 3);
            return;
        }

        Regenerate(dt, stats.Regen);
        StepVotive(dt);
        var locked = _combat.World.IsTransitioning;
        var move = locked ? Vector2.Zero : Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        if (move != Vector2.Zero)
        {
            move = move.Normalized();
            _moveAngle = move.Angle();
        }

        Aim = ComputeAim();
        Player.Aim = Aim;
        StepKnockbackAndPull(dt);

        if (_uivoT > 0)
        {
            ReleaseBowLatch();
            StepUivo(dt);
            Player.MovementLocked = true;
            Player.AttackPose = true;
            StepUltimate(dt);
            UpdateVisuals();
            return;
        }

        // Esquiva
        if (!locked && Input.IsActionJustPressed(InputActions.Dodge) && _state.TryStartDash())
        {
            _dashDir = move != Vector2.Zero ? move : Vector2.FromAngle(Aim);
            Player.PlayDodge(_dashDir, (float)PlayerCombatState.DashSeconds); // quadros da esquiva no tempo do dash (C2-D3)
            _combat.Sfx("dash", Foot);
            _combat.Fx.Burst(Foot, 8, CombatPalette.Dash, new BurstOptions { V1 = 40, Gravity = 0 });
            EmitEnergy(force: true);
            EmitCooldown(0, (float)PlayerCombatState.DashCooldown, (float)PlayerCombatState.DashCooldown);
        }

        var dist = (float)_state.AdvanceDash(dt);
        if (dist > 0)
        {
            _combat.MovePlayer(_dashDir * dist);
            Trail(0.6f);
        }

        Player.MovementLocked = _state.IsDashing;
        Player.SpeedMultiplier = (float)_state.SpeedMultiplier(stats.Speed) * (_combat.Clock.Hitstop > 0 ? (float)CombatClock.HitstopScale : 1f);

        _state.TickAttackTimers(dt);
        if (!locked)
        {
            StepAttack(dt, stats);
            StepActions();
        }
        else
        {
            ReleaseBowLatch();
        }

        Player.AttackPose = _state.AttackTime > 0 || _state.IsCharging || _state.IsWindingUp;
        StepUltimate(dt);
        UpdateVisuals();
        EmitEnergy(force: false);
        EmitReadyCooldowns();
        EmitStatuses(stats.Speed);
    }

    private readonly bool[] _statusOn = new bool[3];
    private static readonly string[] StatusIds = ["shield", "slow", "speed"];

    /// <summary>Estados com ícone no HUD (C2-D7): só emite quando liga/desliga.</summary>
    private void EmitStatuses(double statSpeed)
    {
        SetStatus(0, _state.Shielded && !_state.IsDashing);
        SetStatus(1, _state.Chill > 0);
        SetStatus(2, statSpeed > 1.001);
    }

    private void SetStatus(int index, bool on)
    {
        if (_statusOn[index] != on)
        {
            _statusOn[index] = on;
            EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerStatusChanged, StatusIds[index], on);
        }
    }

    // --- Mira ---

    /// <summary>
    /// <c>aimAngle</c> (D9): mouse segurado ou clicado há menos de 0,5 s → do jogador (<c>y − 12</c>) até o cursor;
    /// senão o analógico direito; senão o último ângulo de movimento (ou a direção cardinal).
    /// </summary>
    private float ComputeAim()
    {
        var p = Foot;
        if (Input.IsMouseButtonPressed(MouseButton.Left) || _combat.Time - _lastClick < MouseAimWindow)
        {
            var mouse = _combat.GetGlobalMousePosition();
            return MathF.Atan2(mouse.Y - (p.Y - 12), mouse.X - p.X);
        }

        var stick = Input.GetVector(InputActions.AimLeft, InputActions.AimRight, InputActions.AimUp, InputActions.AimDown);
        if (stick.LengthSquared() > 0.35f * 0.35f)
        {
            _moveAngle = stick.Angle();
            return Assisted(stick.Angle());
        }

        if (_moveAngle is { } a)
        {
            return Assisted(a);
        }

        return Player.Direction switch
        {
            FacingDirection.Right => 0,
            FacingDirection.Left => MathF.PI,
            FacingDirection.Up => -MathF.PI / 2,
            _ => MathF.PI / 2,
        };
    }

    /// <summary>Ângulo máximo (rad) que a mira assistida corrige e quanto dele puxa (C1-D12).</summary>
    public const float AimAssistCone = 0.38f;

    public const float AimAssistPull = 0.6f;

    public const float AimAssistRange = 150f;

    /// <summary>
    /// Mira assistida leve (opção, só no controle): se um monstro está a até 150 u e a menos de ~22° da mira, a mira
    /// gira 60% do caminho até ele. Mouse nunca é corrigido. Percorre a lista de monstros sem alocar.
    /// </summary>
    private float Assisted(float aim)
    {
        if (!GameSettings.AimAssist || !UI.Common.InputHints.UsingGamepad)
        {
            return aim;
        }

        var p = Foot - new Vector2(0, 10);
        var best = AimAssistCone;
        var target = aim;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (!m.IsTargetable)
            {
                continue;
            }

            var to = new Vector2((float)m.X - p.X, (float)(m.Y - (m.HitHeight * 0.5)) - p.Y);
            if (to.LengthSquared() > AimAssistRange * AimAssistRange)
            {
                continue;
            }

            var diff = MathF.Abs(Mathf.AngleDifference(aim, to.Angle()));
            if (diff < best)
            {
                best = diff;
                target = to.Angle();
            }
        }

        return Mathf.LerpAngle(aim, target, AimAssistPull);
    }

    // --- Ataques ---

    private void StepAttack(float dt, Core.Rules.PlayerStats stats)
    {
        var w = Weapon;
        var held = Input.IsActionPressed(InputActions.Attack) || Input.IsActionPressed(InputActions.AttackHeavy);
        var pressed = Input.IsActionJustPressed(InputActions.Attack) || Input.IsActionJustPressed(InputActions.AttackHeavy);
        switch (w.Kind)
        {
            case WeaponKind.Bow:
            {
                if (GameSettings.BowToggle)
                {
                    // C1-D12: "Carga do arco: alternar" — apertar começa a carregar, apertar de novo solta a flecha.
                    if (pressed)
                    {
                        _bowLatched = !_bowLatched;
                    }

                    held = _bowLatched;
                }

                if (_state.IsDashing)
                {
                    held = false;
                    _bowLatched = false;
                }

                var shot = _state.UpdateBow(w, dt, held, stats.Atk);
                if (_state.Charge >= PlayerCombatState.MaxCharge && _combat.FxRandom.NextDouble() < dt * 20)
                {
                    var at = Foot + new Vector2(MathF.Cos(Aim) * 12, -14 + (MathF.Sin(Aim) * 12));
                    _combat.Glow.Burst(at, 1, CombatPalette.Ambers, new BurstOptions { V1 = 20, Gravity = 0 });
                }

                if (shot is { } arrow)
                {
                    FireArrow(arrow, w, stats);
                }

                break;
            }

            case WeaponKind.Smash when _state.IsWindingUp:
                if (_state.TickWindup(dt))
                {
                    Smash(w, stats);
                }

                break;
            default:
                // Segurar repete o ataque, menos o martelo (combat.js:102).
                if ((pressed || (held && w.Kind != WeaponKind.Smash)) && _state.CanAttack)
                {
                    Attack(w, stats);
                }

                break;
        }
    }

    /// <summary><c>playerAttack</c>.</summary>
    private void Attack(WeaponDef w, Core.Rules.PlayerStats stats)
    {
        var p = Foot;
        var ec = ElementColor(stats);
        switch (w.Kind)
        {
            case WeaponKind.Melee:
            {
                var swing = _state.StartMelee(w);
                var n = MeleeHit(Aim, (float)swing.Arc, (float)swing.Range, stats.Atk * swing.DamageMult, w, swing.Knockback, stats.Element);
                var origin = p - new Vector2(0, 11);
                _combat.Fx.Arc(origin, Aim, (float)swing.Arc, (float)swing.Range, swing.Big ? 0.22f : 0.15f,
                    ec ?? (swing.Big ? CombatPalette.Ambar : CombatPalette.Of(w.Color)), swing.Big ? 5 : 3, swing.ComboStep % 2 == 1);
                if (w.Id == "dag")
                {
                    _combat.Fx.Arc(origin, Aim + 0.25f, (float)swing.Arc * 0.7f, (float)swing.Range * 0.85f, 0.12f, ec ?? CombatPalette.Pink, 2, true, 0.05f);
                }

                _combat.Sfx("swing", p);
                if (n > 0)
                {
                    // POLI-15: o som do acerto sai de onde o golpe pegou (não do jogador); o golpe final do combo é mais grave e pesado.
                    _combat.Sfx("hit", _lastHitAt, swing.Big ? HitPitchBig : 1f, swing.Big ? 0.04f : 0.07f);
                    if (swing.Big)
                    {
                        // C1-D10: o golpe final do combo pesa mais (pausa e tremor um pouco maiores que o acerto comum).
                        _combat.Clock.AtLeast(0.07);
                        _combat.Shake(3);
                    }
                }

                break;
            }

            case WeaponKind.Smash:
                _state.StartSmash(w);
                _combat.Fx.Ring(p + new Vector2(MathF.Cos(Aim) * 20, MathF.Sin(Aim) * 14), 30, 6, 0.24f, CombatPalette.Ambar with { A = 0.8f });
                break;
            case WeaponKind.Orb:
            {
                var at = p + new Vector2(MathF.Cos(Aim) * 12, -14 + (MathF.Sin(Aim) * 10));
                _combat.Projectiles.Fire(ProjectileKind.Orb, at, Vector2.FromAngle(Aim) * 190, 5, stats.Atk, 1.05f, ec ?? CombatPalette.Orb,
                    part: (float)w.PartMult, element: stats.Element);
                _state.StartOrb(w);
                _combat.Sfx("magic", p);
                _combat.Glow.Burst(p + new Vector2(MathF.Cos(Aim) * 14, -14), 6, CombatPalette.OrbTrail, new BurstOptions { V1 = 50, Gravity = 0 });
                break;
            }
        }
    }

    /// <summary><c>fireArrow</c>.</summary>
    private void FireArrow(ArrowShot shot, WeaponDef w, Core.Rules.PlayerStats stats)
    {
        var p = Foot;
        var at = p + new Vector2(MathF.Cos(Aim) * 10, -13 + (MathF.Sin(Aim) * 6));
        _combat.Projectiles.Fire(ProjectileKind.Arrow, at, Vector2.FromAngle(Aim) * (float)shot.Speed, (float)ArrowShot.Radius, shot.Damage, (float)ArrowShot.Life,
            ElementColor(stats) ?? CombatPalette.Creme, part: (float)w.PartMult, knockback: (float)shot.Knockback, pierce: shot.Pierce, full: shot.Pierce, element: stats.Element);
        _combat.Sfx("bow", p);
    }

    /// <summary><c>smash()</c>: setor 2,9 rad / 36 u, empurrão 220, anéis, tremor ≥ 6, hitstop 0,08 se acertou.</summary>
    private void Smash(WeaponDef w, Core.Rules.PlayerStats stats)
    {
        var swing = PlayerCombatState.SmashSwing(w);
        var n = MeleeHit(Aim, (float)swing.Arc, (float)swing.Range, stats.Atk, w, swing.Knockback, stats.Element);
        var c = Foot + new Vector2(MathF.Cos(Aim) * 20, MathF.Sin(Aim) * 14);
        _combat.Fx.Ring(c, 4, 42, 0.3f, CombatPalette.Ambar with { A = 0.9f });
        _combat.Fx.Ring(c, 2, 30, 0.3f, CombatPalette.Brecha with { A = 0.8f }, 0.05f);
        _combat.Fx.Burst(c, 18, CombatPalette.Smash, new BurstOptions { V1 = 120, Up = 40, Gravity = 200 });
        _combat.Shake(6);
        _combat.Sfx("boom", c);
        if (n > 0)
        {
            _combat.Clock.Set(0.08);
        }
    }

    /// <summary><c>meleeHit</c>: setor a partir de <c>(p.x, p.y − 10)</c> contra <c>(m.x, m.y − hh/2)</c>; props também.</summary>
    private int MeleeHit(float aim, float arc, float range, double damage, WeaponDef w, double knockback, string? element)
    {
        var origin = Foot - new Vector2(0, 10);
        var n = 0;
        var enemies = _combat.Enemies;
        for (var i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i].Body;
            if (!m.IsTargetable)
            {
                continue;
            }

            if (CombatMath.InSector(origin.X, origin.Y, m.X, m.Y - (m.HitHeight * 0.5), m.Radius, aim, arc, range))
            {
                if (n == 0)
                {
                    _lastHitAt = new Vector2((float)m.X, (float)(m.Y - (m.HitHeight * 0.5)));
                }

                _combat.HitMob(m, damage, origin, new HitOptions(w.PartMult, w.Backstab ?? 0, knockback, element));
                n++;
            }
        }

        _combat.BreakPropsInSector(origin, aim, arc, range, damage);
        if (n > 0)
        {
            _combat.Clock.AtLeast(0.05); // C1-D10: era 0,035 s — o acerto da Lâmina "escorregava"
            _combat.Shake(2);
        }

        return n;
    }

    private Color? ElementColor(Core.Rules.PlayerStats stats) => stats.Element is { } el ? CombatPalette.Of(Db.Elements[el].Color) : null;

    // --- Ações: Ecos, poção, armas, Supremo ---

    private void StepActions()
    {
        if (Input.IsActionJustPressed(InputActions.Eco1))
        {
            UseEco(0);
        }
        else if (Input.IsActionJustPressed(InputActions.Eco2))
        {
            UseEco(1);
        }

        if (Input.IsActionJustPressed(InputActions.Potion))
        {
            UsePotion();
        }

        for (var i = 0; i < InputActions.WeaponSlots.Length; i++)
        {
            if (Input.IsActionJustPressed(InputActions.WeaponSlots[i]) && i < Db.WeaponOrder.Count)
            {
                SwitchWeapon(Db.WeaponOrder[i].Id);
            }
        }

        if (Input.IsActionJustPressed(InputActions.WeaponNext))
        {
            CycleWeapon(1);
        }
        else if (Input.IsActionJustPressed(InputActions.WeaponPrev))
        {
            CycleWeapon(-1);
        }

        if (Input.IsActionJustPressed(InputActions.Ultimate))
        {
            TryUltimate();
        }
    }

    /// <summary><c>usePotion</c>: cura 45% (recarga 1 s); avisa sem poções ou com vida cheia.</summary>
    public void UsePotion()
    {
        switch (Game.TryUsePotion(_combat.Time, out var healed))
        {
            case PotionResult.Used:
                _combat.Overlay.Float(Foot - new Vector2(0, 34), $"+{healed}", CombatPalette.HealText, true);
                _combat.Sfx("magic", Foot);
                _combat.Glow.Burst(Foot - new Vector2(0, 12), 16, CombatPalette.Potion, new BurstOptions { V1 = 50, Up = 50, Gravity = -20 });
                _potionCd = (float)Db.Rules.Potion.Cooldown;
                EmitCooldown(1, _potionCd, _potionCd);
                break;
            case PotionResult.NoPotions:
                _combat.Note("Sem poções. Lúcia vende na estalagem.", "i_potion");
                break;
            case PotionResult.FullHealth:
                _combat.Note("Vida já está cheia.", "i_potion");
                break;
        }
    }

    /// <summary><c>switchWeapon</c> (<c>extra.js:90-93</c>): a classe é permanente; outra arma só avisa.</summary>
    public void SwitchWeapon(string weaponId)
    {
        var cls = Game.Save.ClassId;
        if (weaponId == cls)
        {
            return;
        }

        _combat.Note($"Sua classe é {ClassName(cls)}. A arma {ClassName(weaponId)} fica bloqueada.", "i_alert");
    }

    /// <summary>Roda do mouse/bumpers (<c>main.js:101</c>): próxima arma possuída.</summary>
    private void CycleWeapon(int step)
    {
        var order = Db.WeaponOrder;
        var cur = Game.Save.CurrentWeapon;
        var start = 0;
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i].Id == cur)
            {
                start = i;
            }
        }

        for (var k = 1; k <= order.Count; k++)
        {
            var next = order[(start + (k * step) + (order.Count * 2)) % order.Count].Id;
            if (Game.Save.WeaponLevel(next) > 0)
            {
                SwitchWeapon(next);
                return;
            }
        }
    }

    private string ClassName(string id) => Db.Classes.TryGetValue(id, out var c) ? c.Name : id;

    // --- Dano no jogador ---

    /// <summary>
    /// <c>hurtPlayer</c>: ignora com i-frames, esquiva ou Investida; dano pela defesa (Core), 0,75 s de i-frames,
    /// empurrão 170, tremor 5, hitstop 0,06, flash vermelho e frio no Inverno. True se o dano entrou.
    /// </summary>
    public bool Hurt(double damage, Vector2 source)
    {
        if (Dead || _uivoT > 0)
        {
            return false;
        }

        var chill = _combat.Brecha.InBrecha ? _combat.Brecha.SeasonDef?.ChillOnHitSeconds ?? 0 : 0;
        if (!_state.TryHurt(chill))
        {
            return false;
        }

        var dealt = _combat.Calculator.ToPlayer(damage, Game.Stats.Def, _combat.Rng);
        var p = Foot;
        _lastHurt = _combat.Time;
        var away = p - source;
        _knock = (away == Vector2.Zero ? Vector2.Down : away.Normalized()) * (float)PlayerCombatState.HurtKnockback;
        _combat.Shake(5);
        _combat.Clock.Set(0.06);
        _combat.Overlay.RedFlash();
        _combat.Overlay.Float(p - new Vector2(0, 34), $"-{dealt}", CombatPalette.HurtText, true);
        _combat.Glow.Burst(p - new Vector2(0, 14), 10, CombatPalette.Hurt, new BurstOptions { V1 = 110 });
        if (Game.Damage(dealt))
        {
            Die();
        }

        return true;
    }

    /// <summary><c>killPlayer</c>: som, tremor 9, hitstop 0,15, partículas; a Interface mostra a tela de morte.</summary>
    private void Die()
    {
        Dead = true;
        _bowLatched = false;
        _deadT = 0;
        _state.CancelActions();
        EndUltimate();
        _uivoT = 0;
        _orbsT = 0;
        Player.MovementLocked = true;
        Player.AttackPose = false;
        _combat.Sfx("die", Foot);
        _combat.Shake(9);
        _combat.Clock.Set(0.15);
        _combat.Glow.Burst(Foot - new Vector2(0, 10), 30, CombatPalette.Death, new BurstOptions { V1 = 120 });
    }

    /// <summary>Volta da morte (<c>respawnAfterDeath</c>): 1,5 s de i-frames e Essência 0.</summary>
    public void Revive()
    {
        Dead = false;
        _deadT = 0;
        _knock = Vector2.Zero;
        _pullT = 0;
        _state.Respawn(Db.Rules.Death.RespawnInvulnerability);
        Player.MovementLocked = false;
        Player.Body.Rotation = 0;
        EmitEssence();
    }

    /// <summary>Puxão no jogador (corrente do Ceifador).</summary>
    public void Pull(Vector2 target, float seconds)
    {
        _pullTarget = target;
        _pullT = seconds;
    }

    private void StepKnockbackAndPull(float dt)
    {
        if (_combat.Time - _lastHurt > KnockbackWindow)
        {
            _knock = Vector2.Zero;
        }

        if (_knock != Vector2.Zero)
        {
            _combat.MovePlayer(_knock * dt);
            _knock *= MathF.Pow(KnockbackDecay, dt);
            if (MathF.Abs(_knock.X) + MathF.Abs(_knock.Y) < 3)
            {
                _knock = Vector2.Zero;
            }
        }

        if (_pullT > 0)
        {
            _combat.MovePlayer((_pullTarget - Foot) * MathF.Min(1, dt * 9));
            _pullT -= dt;
        }
    }

    /// <summary>Regeneração do equipamento depois de 4 s sem dano (acumula frações).</summary>
    private void Regenerate(float dt, double regen)
    {
        if (regen <= 0 || !_state.CanRegenerate || Game.Hp >= Game.MaxHp)
        {
            _regenCarry = 0;
            return;
        }

        _regenCarry += (float)(regen * dt);
        if (_regenCarry >= 1)
        {
            var heal = (int)_regenCarry;
            _regenCarry -= heal;
            Game.Heal(heal);
        }
    }

    // --- Essência ---

    public void AddHitEssence()
    {
        _state.SetEssence(_combat.Calculator.EssenceAfterHit(_state.Essence, Game.Stats.Essence));
        EmitEssence();
    }

    /// <summary>Absorver um Eco enche a Essência (<c>combat.js:501</c>).</summary>
    public void FillEssence()
    {
        _state.SetEssence(Db.Rules.Combat.EssenceMax);
        EmitEssence();
    }

    // --- Visual ---

    private void UpdateVisuals()
    {
        var blink = _state.Invulnerable > 0 && ((int)(_combat.Time * 18) % 2) == 1;
        Player.Body.Modulate = new Color(1, 1, 1, blink ? 0.5f : 1);
        var flash = (float)Math.Min(1, _state.HurtTime * 3) * GameSettings.HitFlashScale; // POLI-7: reduzir flashes
        if (!Mathf.IsEqualApprox(flash, _lastFlash))
        {
            _lastFlash = flash;
            _flash.SetShaderParameter(FlashMaterial.FlashParam, flash);
        }
    }

    /// <summary>Rastro (<c>p.trail</c>): fantasma do sprite atual na camada aditiva.</summary>
    private void Trail(float alpha)
    {
        // o quadro visível de verdade (andar/esquiva desenhados, kit ou atlas), não só o recorte do atlas
        if (Player.Animator.TryGetGhost(out var texture, out var anchor, out var unitsPerPixel))
        {
            _combat.Glow.Ghost(texture, Player.Body.GlobalPosition, anchor, unitsPerPixel, Player.Body.Scale.X < 0, alpha);
        }
    }

    /// <summary>Carga do arco sobre a cabeça e lanternas votivas (<c>drawPlayer</c>).</summary>
    public void DrawAir(FxLayer layer, float time)
    {
        var p = Foot;
        if (_state.IsCharging)
        {
            var k = (float)(_state.Charge / PlayerCombatState.MaxCharge);
            var color = k >= 0.94f ? CombatPalette.Ambar : CombatPalette.Creme with { A = 0.7f };
            layer.DrawArc(p - new Vector2(0, 38), 5, -MathF.PI / 2, (-MathF.PI / 2) + (MathF.Tau * k), 16, color, 1);
        }

        if (_orbsT > 0 && AtlasLibrary.TryGet("lantern", out var lantern))
        {
            for (var i = 0; i < 3; i++)
            {
                var oa = (time * 3) + (i * 2.094f);
                var at = new Vector2(p.X + (MathF.Cos(oa) * 22), p.Y - 14 + (MathF.Sin(oa) * 12) + 7);
                var s = 0.7f / lantern.Density;
                layer.DrawSetTransform(at, 0, new Vector2(s, s));
                layer.DrawTexture(lantern.Texture, -lantern.Anchor);
            }

            layer.DrawSetTransform(Vector2.Zero);
        }

        DrawUltimate(layer, time);
    }

    // --- Sinais para o HUD (só quando muda, sem alocar por frame) ---

    private static void Emit(StringName signal, Variant arg) => EventBus.Instance.EmitSignal(signal, arg);

    private void EmitEssence() =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.EssenceChanged, (float)_state.Essence, (float)Db.Rules.Combat.EssenceMax);

    private void EmitEnergy(bool force)
    {
        var e = (float)_state.Energy;
        if (!force && MathF.Abs(e - _lastEnergy) < EnergyEmitStep && !(e >= PlayerCombatState.EnergyMax && _lastEnergy < PlayerCombatState.EnergyMax))
        {
            return;
        }

        _lastEnergy = e;
        EventBus.Instance.EmitSignal(EventBus.SignalName.StaminaChanged, e, (float)PlayerCombatState.EnergyMax);
    }

    /// <summary>Recarga começou: o HUD anima a contagem sozinho até o sinal de pronto (remaining 0).</summary>
    private void EmitCooldown(int index, float remaining, float total)
    {
        _cooldownReady[index] = remaining <= 0;
        EventBus.Instance.EmitSignal(EventBus.SignalName.AbilityCooldownChanged, CooldownIds[index], remaining, total);
    }

    private void EmitReadyCooldowns()
    {
        CheckReady(0, _state.DashCd <= 0, PlayerCombatState.DashCooldown);
        CheckReady(1, _potionCd <= 0, Db.Rules.Potion.Cooldown);
        CheckReady(2, _state.EcoCd <= 0, PlayerCombatState.EcoCooldown);
        CheckReady(3, _state.EcoCd <= 0, PlayerCombatState.EcoCooldown);
        CheckReady(4, _state.UltimateReady, 0);
    }

    private void CheckReady(int index, bool ready, double total)
    {
        if (ready && !_cooldownReady[index])
        {
            EmitCooldown(index, 0, (float)total);
        }
    }
}
