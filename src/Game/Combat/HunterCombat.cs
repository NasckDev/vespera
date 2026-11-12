using System;
using Godot;
using Vespera.Core.Advancement;
using Vespera.Core.Combat;
using Vespera.Core.Rift;
using Vespera.Core.Rules;
using Vespera.Game.Actors;
using Vespera.Game.Boot;
using Vespera.Game.Combat.Echo;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Combat;

/// <summary>Ordem de movimento que o combate dá ao caçador neste quadro.</summary>
public readonly record struct MotionOrder(float SpeedScale, Vector2? Velocity, bool OwnsAnimation)
{
    public static MotionOrder Free => new(1f, null, false);
}

public enum HunterState
{
    Free,
    Dodging,
    Hurt,
    Swallowed,
    Dead,

    /// <summary>Caído no co-op: espera um aliado reanimar (segurar interagir perto) ou a queda do grupo inteiro.</summary>
    Downed,
}

/// <summary>
/// Combate do caçador (GDD §5.2): vida, fôlego, esquiva com invencibilidade, dano com recuo e piscada, morte e o
/// conjunto de golpes da arma (<see cref="WeaponMoves"/>). É o alvo dos monstros (<see cref="IHittable"/>).
/// Dirigido pelo <see cref="Player"/> uma vez por quadro de física, antes do movimento.
/// </summary>
public sealed partial class HunterCombat : Node, IHittable
{
    private const float BodyHeight = 62f;
    private const float BodyRadius = 30f;
    private const float BlinkRate = 22f;
    private const float AfterImageEvery = 0.05f;
    private const float DeathDelay = 2.4f;

    private const int EchoSlots = 2;
    private const float SlimeEvery = 0.035f;

    private readonly Player _player;
    private HunterTuning _tuning = new();
    private DamageCalculator _damage = null!;
    private StaminaMeter _stamina = null!;
    private float _stateTime;
    private float _invulnerable;
    private float _afterImageTimer;
    private Vector2 _dodgeVelocity;
    private Vector2 _knock;
    private readonly EchoCooldown[] _echoCooldowns = [new(), new()];
    private bool _ecoDash;
    private float _sheltered;
    private float _velaLeft;
    private double _velaHealPerSecond;
    private double _velaHealCarry;
    private int _velaCut;
    private float _slimeTimer;

    public HunterCombat(Player player)
    {
        _player = player;
        Name = "Combat";
    }

    public Team Team => Team.Hunters;

    public Vector2 HitCenter => _player.GlobalPosition - new Vector2(0, BodyHeight * _player.ActorScale * 0.5f);

    public float HitRadius => BodyRadius * _player.ActorScale;

    public bool CanBeHit => State is HunterState.Free or HunterState.Hurt && _invulnerable <= 0f && _sheltered <= 0f && !Disabled;

    public HunterState State { get; private set; } = HunterState.Free;

    public WeaponMoves Moves { get; private set; } = null!;

    public int Hp { get; private set; }

    public int MaxHp { get; private set; } = 100;

    public PlayerStats Stats { get; private set; }

    public double StaminaFraction => _stamina.Fraction;

    /// <summary>Sem combate (bots de colisão, cutscene, painel aberto).</summary>
    public bool Disabled { get; set; }

    /// <summary>Fora da luta: morto ou caído esperando reanimação (os monstros e o saque o ignoram).</summary>
    public bool IsDead => State is HunterState.Dead or HunterState.Downed;

    public bool IsDowned => State == HunterState.Downed;

    /// <summary>Co-op dentro da Brecha: cair vira <see cref="HunterState.Downed"/> em vez de morrer (a fase liga isto).</summary>
    public Func<bool>? CanGoDown { get; set; }

    /// <summary>Relógio do combate (segundos de jogo), usado pela fila do combo.</summary>
    public double Clock { get; private set; }

    public Player Player => _player;

    /// <summary>Recarga do Eco do slot 0: 0 = pronto, 1 = recém-usado (ícone da HUD).</summary>
    public float EcoCooldownFraction => EchoCooldownFraction(0);

    /// <summary>Recarga do Eco do <paramref name="slot"/>: 0 = pronto, 1 = recém-usado.</summary>
    public float EchoCooldownFraction(int slot) => slot >= 0 && slot < EchoSlots ? _echoCooldowns[slot].Fraction : 0f;

    /// <summary>Id do Eco do slot (0 = tecla Eco 1, 1 = Eco 2) deste caçador; null se vazio ou ainda bloqueado.</summary>
    public string? EcoInSlot(int slot) => EquippedEcho(slot)?.Id;

    public float Scale => _player.ActorScale;

    /// <summary>Caiu: chamado depois da animação de morte (a fase decide a Lanterna e a volta).</summary>
    public event Action<HunterCombat>? Died;

    /// <summary>Caiu no co-op (a fase confere se o grupo inteiro caiu).</summary>
    public event Action<HunterCombat>? Downed;

    public override void _Ready()
    {
        _tuning = RiftSession.Brecha.Hunter;
        _damage = new DamageCalculator(RiftSession.Db.Rules.Combat);
        _stamina = new StaminaMeter(_tuning.StaminaMax, _tuning.StaminaRegen, _tuning.StaminaRegenDelay);
        RefreshStats(fullHeal: true);
        Moves = WeaponMoves.For(this, _player.Hunter);
        _player.Sprite.FrameChanged += () => Moves.OnFrame(_player.Sprite.Animation, _player.Sprite.Frame);
        _player.Sprite.AnimationFinished += () => Moves.OnAnimationFinished(_player.Sprite.Animation);
    }

    /// <summary>Recalcula HP máximo, ataque, defesa e crítico pela progressão (rank, arma forjada, equipamento).</summary>
    public void RefreshStats(bool fullHeal)
    {
        Stats = HunterStats.Compute(RiftSession.Db, RiftSession.Save, HunterSprites.WeaponId(_player.Hunter), _player.Slot);
        var soup = Hunter.HuntService.SoupActive ? Core.Hunt.RunPerks.SoupBonus(Stats.Hp) : 0;
        MaxHp = Math.Max(1, Stats.Hp + Hunter.HuntService.Bless.MaxHp + soup - _velaCut);
        Hp = fullHeal ? MaxHp : Math.Min(Hp, MaxHp);
        RefreshSetEffects();
    }

    /// <summary>Troca de classe (seletor): refaz o conjunto de golpes.</summary>
    public void ChangeWeapon()
    {
        Moves?.Cancel();
        Moves = WeaponMoves.For(this, _player.Hunter);
        RefreshStats(fullHeal: true);
    }

    public MotionOrder Step(float dt, Vector2 move)
    {
        Clock += dt;
        _stateTime += dt;
        _invulnerable = Mathf.Max(0f, _invulnerable - dt);
        _stamina.Tick(dt);
        foreach (var cooldown in _echoCooldowns)
        {
            cooldown.Tick(dt);
        }

        _sheltered = Mathf.Max(0f, _sheltered - dt);
        TickVela(dt);
        TickSetEffects(dt);
        _knock = _knock.MoveToward(Vector2.Zero, 1800f * Scale * dt);
        UpdateBlink();
        if (Disabled || _player.Frozen)
        {
            return MotionOrder.Free;
        }

        switch (State)
        {
            case HunterState.Dead:
            case HunterState.Downed:
            case HunterState.Swallowed:
                return new MotionOrder(0f, Vector2.Zero, true);
            case HunterState.Dodging:
                return StepDodge(dt);
            case HunterState.Hurt:
                if (_stateTime >= _tuning.HurtStun)
                {
                    State = HunterState.Free;
                }

                return new MotionOrder(0f, _knock, true);
        }

        var input = _player.Input;
        if (input.IsJustPressed(InputActions.Dodge) && TryDodge(move))
        {
            return StepDodge(dt);
        }

        if (((input.IsJustPressed(InputActions.Skill) && TryEcho(0, move)) || (input.IsJustPressed(InputActions.Skill2) && TryEcho(1, move))) && State == HunterState.Dodging)
        {
            return StepDodge(dt);
        }

        if (input.IsJustPressed(InputActions.Potion))
        {
            DrinkPotion();
        }

        var order = Moves.Tick(dt, move);
        return _knock.LengthSquared() > 1f ? order with { Velocity = (order.Velocity ?? Vector2.Zero) + _knock } : order;
    }

    public HitOutcome ReceiveHit(in HitInfo hit)
    {
        if (!CanBeHit)
        {
            return HitOutcome.Miss;
        }

        var damage = RiftSession.Cheat ? 0 : _damage.ToPlayer(IncomingDamage(hit.Damage), Stats.Def, RiftSession.Random);
        Hp = Math.Max(0, Hp - damage);
        Hunter.HuntService.HunterHit(damage);
        var feel = _player.Feel;
        feel?.Number(HitCenter, damage.ToString(), new Color("ff6b6b"), 1.1f);
        feel?.Shake(0.45f);
        feel?.HitStop(0.07);
        feel?.Spark(HitCenter, new Color("e85d5d"), 10, 260f);
        CombatSfx.Play(_player, CombatSound.Hurt, Vector2.Zero);
        _player.Flash.Pulse(new Color(1f, 0.35f, 0.35f), 0.05f, 0.16f, 0.7f);
        Moves.Cancel();
        _knock = hit.Push;
        _invulnerable = (float)(_tuning.HurtInvulnerable + Hunter.HuntService.Bless.HurtInvulnerable);
        if (Hp <= 0)
        {
            TryLastStand();
        }

        if (Hp <= 0)
        {
            Die();
            return new HitOutcome(damage, true, false, true);
        }

        HurtBurst(damage);
        Enter(HunterState.Hurt);
        _player.PlayAnimation("dano", restart: true);
        return new HitOutcome(damage, false, false, true);
    }

    /// <summary>Engolido pela Lodo-Mãe: some dentro dela até <see cref="Release"/>.</summary>
    public bool TrySwallow()
    {
        if (!CanBeHit || RiftSession.Cheat)
        {
            return false;
        }

        Moves.Cancel();
        Enter(HunterState.Swallowed);
        _player.Sprite.Visible = false;
        return true;
    }

    /// <summary>Dano direto enquanto engolido (sem recuo).</summary>
    public void Digest(int damage)
    {
        Hp = Math.Max(0, Hp - damage);
        Hunter.HuntService.HunterHit(damage);
        _player.Feel?.Number(HitCenter, damage.ToString(), new Color("ff6b6b"), 0.9f);
        if (Hp <= 0)
        {
            _player.Sprite.Visible = true;
            Die();
        }
    }

    public void Release(Vector2 push)
    {
        if (State != HunterState.Swallowed)
        {
            return;
        }

        _player.Sprite.Visible = true;
        _knock = push;
        _invulnerable = (float)(_tuning.HurtInvulnerable + Hunter.HuntService.Bless.HurtInvulnerable);
        Enter(HunterState.Hurt);
        _player.PlayAnimation("dano", restart: true);
    }

    /// <summary>Volta à vida cheia (respawn na Praça).</summary>
    public void Revive()
    {
        RefreshStats(fullHeal: true);
        _stamina.Refill();
        _player.Sprite.Visible = true;
        _player.Sprite.Modulate = Colors.White;
        _invulnerable = 1.2f;
        Enter(HunterState.Free);
    }

    /// <summary>Reanimado por um aliado no co-op: levanta com <paramref name="hp"/> de vida e um instante invencível.</summary>
    public void ReviveFromDown(int hp)
    {
        if (State != HunterState.Downed)
        {
            return;
        }

        Hp = Math.Clamp(hp, 1, MaxHp);
        _stamina.Refill();
        _player.Sprite.Visible = true;
        _player.Sprite.Modulate = Colors.White;
        _invulnerable = 1.5f;
        Enter(HunterState.Free);
        _player.PlayAnimation("parado", restart: true);
        _player.Flash.Pulse(new Color(1f, 0.85f, 0.5f), 0.08f, 0.35f, 0.8f);
        CombatSfx.Play(_player, CombatSound.Pickup, Vector2.Zero, 2f);
    }

    /// <summary>Vida trazida da sala anterior.</summary>
    public void SetHp(int hp) => Hp = Math.Clamp(hp, 1, MaxHp);

    /// <summary>Cura (poção, lanterna de descanso): número verde e brilho.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        var before = Hp;
        Hp = Math.Min(MaxHp, Hp + amount);
        _player.Feel?.Number(HitCenter, $"+{Hp - before}", new Color("7be38a"), 1.2f);
        _player.Feel?.Spark(HitCenter, new Color("9cf0a4"), 16, 160f, Vector2.Up, 120f);
        _player.Flash.Pulse(new Color(0.55f, 1f, 0.6f), 0.08f, 0.3f, 0.6f);
    }

    /// <summary>Bebe uma poção (tecla R): cura parte da vida; sem poção ou com a vida cheia, só avisa.</summary>
    public void DrinkPotion()
    {
        if (Hunter.HuntService.TryDrink(this))
        {
            Components.WorldSfx.Play(_player, Components.WorldSound.Water, Vector2.Zero);
            return;
        }

        var none = Hunter.HuntService.State.Potions <= 0;
        _player.Feel?.Toast(none ? "Sem poções. A Tia Nilza vende na Praça." : "A vida já está cheia.");
    }

    /// <summary>Eco equipado no <paramref name="slot"/> do jogador deste caçador (null = vazio ou sem progressão).</summary>
    public EchoDef? EquippedEcho(int slot)
    {
        var engine = Coop.PartyProgression.EngineFor(_player.Slot);
        if (engine is null)
        {
            return null;
        }

        var id = engine.IsEchoSlotUnlocked(slot) ? engine.EchoIn(slot) : null;
        if (id is null && slot == 0 && _player.Slot == 0 && Hunter.HuntService.HasEcoViscoso)
        {
            id = EchoCaster.Viscoso;
        }

        return id is not null && engine.Catalog.Echoes.TryGetValue(id, out var def) ? def : null;
    }

    /// <summary>
    /// Usa o Eco do <paramref name="slot"/>. Sem recarga e com Eco equipado, solta o efeito (cada Eco tem o seu) e inicia a
    /// recarga. O Viscoso é o avanço: nesse caso o estado vira <see cref="HunterState.Dodging"/>.
    /// </summary>
    public bool TryEcho(int slot, Vector2 move)
    {
        if (slot < 0 || slot >= EchoSlots || EquippedEcho(slot) is not { } def || !_echoCooldowns[slot].Ready)
        {
            return false;
        }

        var dir = move.LengthSquared() > 0.01f ? move.Normalized() : _player.AimDirection;
        if (def.Id == EchoCaster.Viscoso)
        {
            StartEcoDash(def, dir);
        }
        else if (!EchoCaster.Cast(this, def, dir))
        {
            return false;
        }

        _echoCooldowns[slot].Start(def.Cooldown);
        return true;
    }

    /// <summary>Eco Viscoso: avanço longo sem custo de fôlego que deixa um rastro de lodo que lentifica os monstros.</summary>
    private void StartEcoDash(EchoDef def, Vector2 dir)
    {
        Moves.Cancel();
        _player.Face(dir);
        _dodgeVelocity = dir * (float)(_tuning.DodgeDistance * def.Radius * Scale / _tuning.DodgeSeconds);
        _invulnerable = Mathf.Max(_invulnerable, (float)_tuning.DodgeInvulnerable);
        _ecoDash = true;
        _gelDash = false;
        _slimeTimer = 0f;
        Enter(HunterState.Dodging);
        _player.PlayAnimation("esquiva", restart: true, fitSeconds: (float)_tuning.DodgeSeconds);
        CombatSfx.Play(_player, CombatSound.Slam, Vector2.Zero, -6f);
    }

    /// <summary>Abrigo do Eco do Lar: invulnerável (sem piscar) pelos próximos <paramref name="seconds"/>.</summary>
    public void Shelter(float seconds) => _sheltered = Mathf.Max(_sheltered, seconds);

    /// <summary>Eco da Vela: cura contínua e, enquanto dura, o rank fica um nível abaixo (menos vida máxima).</summary>
    public void StartVela(EchoDef def)
    {
        var engine = Coop.PartyProgression.EngineFor(_player.Slot);
        _velaCut = engine is null ? 0 : EchoRules.VelaHpPenalty(engine.Catalog, engine.Rank);
        _velaLeft = (float)def.Duration;
        _velaHealPerSecond = EchoRules.HealPerSecond(def, MaxHp - _velaCut);
        RefreshStats(fullHeal: false);
        _player.Flash.Pulse(new Color(1f, 0.85f, 0.45f), 0.08f, (float)def.Duration, 0.45f);
    }

    private void TickVela(float dt)
    {
        if (_velaLeft <= 0f)
        {
            return;
        }

        _velaLeft -= dt;
        if (!IsDead)
        {
            _velaHealCarry += _velaHealPerSecond * dt;
            var whole = (int)_velaHealCarry;
            if (whole > 0)
            {
                _velaHealCarry -= whole;
                Hp = Math.Min(MaxHp, Hp + whole);
            }
        }

        if (_velaLeft <= 0f)
        {
            _velaCut = 0;
            _velaHealCarry = 0;
            RefreshStats(fullHeal: false);
            _player.Feel?.Toast("A vela apagou: o rank volta.");
        }
    }

    private bool TryDodge(Vector2 move)
    {
        if (!_stamina.TrySpend(_tuning.DodgeCost * Math.Max(0.2, 1 + (Hunter.HuntService.Bless.DodgeCostPct / 100))))
        {
            return false;
        }

        Moves.Cancel();
        var dir = move.LengthSquared() > 0.01f ? move.Normalized() : _player.AimDirection;
        _player.Face(dir);
        var bless = Hunter.HuntService.Bless;
        _dodgeVelocity = dir * (float)(_tuning.DodgeDistance * (1 + (bless.DodgeDistancePct / 100)) * Scale / _tuning.DodgeSeconds);
        _invulnerable = Mathf.Max(_invulnerable, (float)(_tuning.DodgeInvulnerable + bless.DodgeInvulnerable + (_setFx.Has(SetEffects.DodgeIframes) ? SetEffects.DodgeIframesBonusSeconds : 0)));
        _ecoDash = false;
        _gelDash = _setFx.Has(SetEffects.DodgeGel);
        Hunter.HuntService.Dodged(dir.X);
        Enter(HunterState.Dodging);
        _player.PlayAnimation("esquiva", restart: true, fitSeconds: (float)_tuning.DodgeSeconds);
        CombatSfx.Play(_player, CombatSound.Dodge, Vector2.Zero);
        _afterImageTimer = 0f;
        return true;
    }

    private MotionOrder StepDodge(float dt)
    {
        if ((_ecoDash || _gelDash) && (_slimeTimer -= dt) <= 0f && _player.World is { } world)
        {
            _slimeTimer = SlimeEvery;
            world.AddGroundFx(new SlimeTrail(world, _player.GlobalPosition, _setFx.Has(SetEffects.DodgeGel)));
        }

        _afterImageTimer -= dt;
        if (_afterImageTimer <= 0f)
        {
            _afterImageTimer = AfterImageEvery;
            AfterImage.Spawn(_player);
        }

        var t = _stateTime / (float)_tuning.DodgeSeconds;
        if (t >= 1f)
        {
            _ecoDash = false;
            _gelDash = false;
            Enter(HunterState.Free);
            return new MotionOrder(1f, _dodgeVelocity * 0.25f, false);
        }

        var ease = 1.25f - (0.7f * t);
        return new MotionOrder(0f, _dodgeVelocity * ease, true);
    }

    private void Die()
    {
        if (CanGoDown?.Invoke() == true)
        {
            GoDown();
            return;
        }

        Moves.Cancel();
        Enter(HunterState.Dead);
        _player.Sprite.Modulate = Colors.White;
        _player.PlayAnimation("morte", restart: true);
        _player.Feel?.SlowMotion(0.35f, 0.9);
        _player.Feel?.Shake(0.7f);
        CombatSfx.Play(_player, CombatSound.Fall, Vector2.Zero);
        var timer = GetTree().CreateTimer(DeathDelay, processAlways: false);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(this) && State == HunterState.Dead)
            {
                Died?.Invoke(this);
            }
        };
    }

    private void GoDown()
    {
        Moves.Cancel();
        Enter(HunterState.Downed);
        _player.Sprite.Visible = true;
        _player.Sprite.Modulate = Colors.White;
        _player.PlayAnimation("morte", restart: true);
        _player.Feel?.Shake(0.5f);
        CombatSfx.Play(_player, CombatSound.Fall, Vector2.Zero);
        Downed?.Invoke(this);
    }

    private void Enter(HunterState state)
    {
        State = state;
        _stateTime = 0f;
    }

    private void UpdateBlink()
    {
        var blinking = _invulnerable > 0f && State is HunterState.Free or HunterState.Hurt && _stateTime > 0.05f;
        var alpha = blinking && Mathf.Sin(_stateTime * BlinkRate) > 0f ? 0.45f : 1f;
        _player.Sprite.Modulate = _player.Sprite.Modulate with { A = alpha };
    }
}
