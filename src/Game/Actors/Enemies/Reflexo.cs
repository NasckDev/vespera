using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Core.Final;
using Vespera.Core.Story;
using Vespera.Game.Boot;
using Vespera.Game.Combat;
using Vespera.Game.Story;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// O Reflexo (Guardião do Final, ROTEIRO-V2 §13 FINAL; arte issue #52): o próprio caçador do lado de baixo. Lê o estilo
/// do jogador (<see cref="ReflexoProfile"/>, das estatísticas salvas) e luta com ele (<see cref="ReflexoPattern"/>):
/// <list type="bullet">
/// <item>Golpes de Lâmina (investida em faixa), Arco (uma faixa por flecha) e Martelo (salto e círculo), alternados na
/// ordem das armas mais usadas pelo jogador; a cada fase de vida (66% e 33%) soma mais uma arma e o aviso encurta.</item>
/// <item>Todo golpe tem aviso âmbar: contorno no corpo, os 2 primeiros quadros da tira (arma em brasa) e a marca no chão.</item>
/// <item>Esquiva para o lado em que o jogador mais esquiva, com a chance do ritmo de esquiva dele.</item>
/// <item>Uma lanterna magenta acesa em volta dele por Lanterna perdida no jogo (o buff: vida e dano a mais).</item>
/// <item>Uma frase por minuto de luta, com a voz do jogador (<c>data/dialogue/final/reflexo.json</c>).</item>
/// </list>
/// </summary>
public partial class Reflexo : Enemy
{
    public const string LinesDialogue = "final/reflexo";
    private const float RecoverSeconds = 0.45f;
    private const float BladeWidth = 120f;
    private const float LeapSeconds = 0.28f;
    private const float FirstCooldown = 1.2f;

    private static readonly Color Magenta = new("e64a8e");
    private static readonly Color Cream = new("f4e7c6");
    private static ReflexoTuning? _tuning;

    private readonly ReflexoPattern _pattern;
    private readonly List<TelegraphMarker> _markers = [];
    private readonly List<(Vector2 From, Vector2 Dir)> _lanes = [];
    private ReflexoAttack _attack;
    private Vector2 _strikeAt;
    private Vector2 _leapFrom;
    private float _cooldown = FirstCooldown;
    private float _dodgeCooldown;
    private float _dodging;
    private float _leaping;
    private float _fightSeconds;
    private int _lastMinute = -1;
    private bool _playerWasSwinging;
    private Lamps _lamps = null!;

    public Reflexo(LevelWorld world)
        : base("reflexo", world)
    {
        var stats = Hunter.HuntService.State.Stats ?? new Core.Hunt.CombatStats();
        var catalog = Hunter.HuntService.BlessingCatalog;
        var families = catalog.Blessings.ToDictionary(b => b.Id, b => b.Family);
        var names = catalog.Families.ToDictionary(f => f.Key, f => f.Value.Name);
        var profile = ReflexoProfile.From(stats, families, names);
        _pattern = new ReflexoPattern(profile, Tuning, RiftSession.Load(FinalStory.BrechaId).Boss.Phases);
        SetHealth(MaxHp * profile.Buff.HpMult);
    }

    /// <summary>Ritmo do Reflexo (seção <c>"reflexo"</c> de <c>src/Data/brecha_final.json</c>).</summary>
    public static ReflexoTuning Tuning => _tuning ??= ReflexoTuning.FromBrechaJson(
        UserFile.ReadText(RiftSession.BrechaFiles[FinalStory.BrechaId]) ?? throw new System.InvalidOperationException("brecha_final.json ausente"));

    public ReflexoProfile Profile => _pattern.Profile;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    /// <summary>Golpe em curso ou o último que deu (QA e bot).</summary>
    public ReflexoAttack CurrentAttack => _attack;

    /// <summary>Esquivas que ele já fez nesta luta.</summary>
    public int Dodges { get; private set; }

    /// <summary>Frases que ele já disse nesta luta (uma por minuto).</summary>
    public List<string> Lines { get; } = [];

    public override bool Unstoppable => true;

    public override bool CanBeHit => base.CanBeHit && _dodging <= 0f;

    private float Human => World.Data.HumanHeight;

    private double Damage(double mult) => mult * Profile.Buff.DamageMult;

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
        _lamps = new Lamps(Profile.Buff.Lamps, Human) { Name = "Lanternas" };
        AddChild(_lamps);
        if (Profile.Buff.Lamps > 0)
        {
            World.Feel.Toast($"{Profile.Buff.Lamps} lanternas acesas em volta dele. Uma por Lanterna que você perdeu.");
        }

        SayMinuteLine(0);
    }

    protected override Vector2 Think(float dt)
    {
        _fightSeconds += dt;
        _cooldown -= dt;
        _dodgeCooldown -= dt;
        UpdatePhase();
        var minute = _pattern.MinuteAt(_fightSeconds);
        if (minute != _lastMinute)
        {
            SayMinuteLine(minute);
        }

        if (_dodging > 0f)
        {
            return TickDodge(dt);
        }

        if (_leaping > 0f)
        {
            return TickLeap(dt);
        }

        switch (State)
        {
            case EnemyState.Telegraph:
                TickTelegraph();
                return Vector2.Zero;
            case EnemyState.Attack:
            case EnemyState.Recover:
                if (StateTime >= RecoverSeconds)
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
        if (TryDodge(target))
        {
            return Vector2.Zero;
        }

        var next = _pattern.Peek(Phase, minute);
        var to = target.GlobalPosition - GlobalPosition;
        var distance = to.Length();
        if (_cooldown <= 0f && InRange(distance, next))
        {
            StartAttack(target, minute);
            return Vector2.Zero;
        }

        return Approach(to, distance, next);
    }

    /// <summary>Perto o bastante para a arma que vem: o Arco atira de longe, o Martelo salta até a marca, a Lâmina investe.</summary>
    private bool InRange(float distance, ReflexoAttack next) => next switch
    {
        ReflexoAttack.Arco => distance <= Tuning.BowRange * 0.8 * A,
        ReflexoAttack.Martelo => distance <= Tuning.HammerLeap * 1.2 * A,
        _ => distance <= (Tuning.BladeLunge + (Tuning.BladeReach * 0.8)) * A,
    };

    /// <summary>Anda até a distância da arma que vem (o Arco mantém distância, Lâmina e Martelo encostam).</summary>
    private Vector2 Approach(Vector2 to, float distance, ReflexoAttack next)
    {
        var keep = (next == ReflexoAttack.Arco ? Tuning.BowKeep : Tuning.BladeReach) * A;
        if (Mathf.Abs(distance - (float)keep) < 30f * A)
        {
            PlayIf("parado");
            return Vector2.Zero;
        }

        PlayIf("andar");
        var dir = distance > keep ? to.Normalized() : -to.Normalized();
        return dir * (float)Speed * A * (Phase == 3 ? 1.2f : 1f);
    }

    private void UpdatePhase()
    {
        var next = _pattern.Phase(Hp / MaxHp);
        if (next <= Phase)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.45f);
        CombatSfx.Play(this, CombatSound.BossRoar, Vector2.Zero);
        var pool = _pattern.Pool(Phase, _pattern.MinuteAt(_fightSeconds)).Select(WeaponName);
        World.Feel.Toast($"O Reflexo aprendeu: agora alterna {string.Join(", ", pool)}.");
    }

    /// <summary>Fala do minuto, com a voz do jogador: sobe da cabeça e fica no aviso da tela.</summary>
    private void SayMinuteLine(int minute)
    {
        _lastMinute = minute;
        var line = MinuteLine(minute);
        if (line.Length == 0)
        {
            return;
        }

        Lines.Add(line);
        World.Feel.Number(GlobalPosition - new Vector2(0, Human * 1.3f), line, Cream, 1.4f);
        World.Feel.Toast($"O Reflexo, com a sua voz: \"{line}\"");
    }

    /// <summary>Texto da fala do minuto (nó escolhido pela flag do minuto, que também fica gravada).</summary>
    private string MinuteLine(int minute)
    {
        var flag = ReflexoProfile.MinuteFlag(minute);
        if (flag is not null)
        {
            StoryService.Engine.SetFlag(flag);
        }

        var file = StoryService.LoadDialogue(LinesDialogue);
        var entry = file.Entries.FirstOrDefault(e => flag is null ? e.Conditions.Count == 0 : e.Conditions.Contains(flag));
        if (entry is null || !file.Nodes.TryGetValue(entry.Node, out var node))
        {
            return string.Empty;
        }

        var tokens = new Dictionary<string, string>(Profile.Tokens());
        var runner = new DialogueRunner(file, StoryService.Engine, StoryService.PlayerName, tokens);
        return runner.Resolve(node.Text);
    }

    private bool TryDodge(Player target)
    {
        var swinging = target.Combat.Moves.Busy;
        var started = swinging && !_playerWasSwinging;
        _playerWasSwinging = swinging;
        if (!started || _dodgeCooldown > 0f || target.GlobalPosition.DistanceTo(GlobalPosition) > Human * 3.5f)
        {
            return false;
        }

        _dodgeCooldown = (float)Tuning.DodgeCooldown;
        if (!_pattern.ShouldDodge(Phase, GD.Randf()))
        {
            return false;
        }

        Dodges++;
        _dodging = (float)Tuning.DodgeSeconds;
        Sprite.Modulate = Sprite.Modulate with { A = 0.45f };
        CombatSfx.Play(this, CombatSound.Dodge, Vector2.Zero);
        World.Feel.Spark(HitCenter, Magenta, 8, 200f * A);
        return true;
    }

    private Vector2 TickDodge(float dt)
    {
        _dodging -= dt;
        if (_dodging <= 0f)
        {
            Sprite.Modulate = Sprite.Modulate with { A = 1f };
        }

        var speed = (float)(Tuning.DodgeDistance / Tuning.DodgeSeconds) * A;
        return new Vector2(_pattern.DodgeDirection * speed, 0f);
    }

    private void StartAttack(Player target, int minute)
    {
        _attack = _pattern.Next(Phase, minute);
        _cooldown = (float)_pattern.Cooldown(Phase);
        var seconds = (float)_pattern.Telegraph(Phase);
        var dir = (target.GlobalPosition - GlobalPosition).Normalized();
        Enter(EnemyState.Telegraph);
        Play(AnimFor(_attack));
        Sprite.Pause();
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        _lanes.Clear();
        switch (_attack)
        {
            case ReflexoAttack.Lamina:
                var length = (float)(Tuning.BladeLunge + Tuning.BladeReach) * A;
                _lanes.Add((GlobalPosition, dir));
                _markers.Add(WarnLane(GlobalPosition, dir, length, BladeWidth * A, seconds));
                break;
            case ReflexoAttack.Arco:
                var arrows = _pattern.Arrows(Phase);
                for (var i = 0; i < arrows; i++)
                {
                    var angle = (i - ((arrows - 1) * 0.5f)) * (float)Tuning.BowSpread;
                    var aim = dir.Rotated(angle);
                    _lanes.Add((GlobalPosition, aim));
                    _markers.Add(WarnLane(GlobalPosition, aim, (float)Tuning.BowRange * A, (float)Tuning.BowWidth * A, seconds));
                }

                break;
            default:
                var reach = Mathf.Min(GlobalPosition.DistanceTo(target.GlobalPosition), (float)Tuning.HammerLeap * A);
                _strikeAt = World.ClampToFloor(GlobalPosition + (dir * reach));
                _markers.Add(WarnCircle(_strikeAt, (float)Tuning.HammerRadius * A, seconds));
                break;
        }
    }

    /// <summary>Aviso: os 2 primeiros quadros da tira são a arma em brasa; o segundo entra na metade do aviso.</summary>
    private void TickTelegraph()
    {
        var seconds = (float)_pattern.Telegraph(Phase);
        Sprite.Frame = StateTime >= seconds * 0.5f ? 1 : 0;
        if (StateTime < seconds)
        {
            return;
        }

        foreach (var marker in _markers)
        {
            marker.Release();
        }

        _markers.Clear();
        Flash.Tint = 0f;
        Sprite.Frame = 2;
        Sprite.Play();
        Enter(EnemyState.Attack);
        switch (_attack)
        {
            case ReflexoAttack.Lamina:
                ReleaseBlade();
                break;
            case ReflexoAttack.Arco:
                ReleaseArrows();
                break;
            default:
                _leapFrom = GlobalPosition;
                _leaping = LeapSeconds;
                CombatSfx.Play(this, CombatSound.HammerSwing, Vector2.Zero);
                break;
        }
    }

    private void ReleaseBlade()
    {
        var (from, dir) = _lanes[0];
        var length = (float)(Tuning.BladeLunge + Tuning.BladeReach) * A;
        CombatSfx.Play(this, CombatSound.SwingHeavy, Vector2.Zero);
        GlobalPosition = World.ClampToFloor(from + (dir * (float)Tuning.BladeLunge * A));
        StreakLine(from, dir, length, BladeWidth * A * 0.5f);
        StrikeLane(from, dir, length, BladeWidth * A, Damage(Tuning.BladeDamage), 320f);
    }

    private void ReleaseArrows()
    {
        CombatSfx.Play(this, CombatSound.BowShot, Vector2.Zero);
        var length = (float)Tuning.BowRange * A;
        foreach (var (from, dir) in _lanes)
        {
            StreakLine(from - new Vector2(0, BodyLift), dir, length, (float)Tuning.BowWidth * A * 0.35f);
            StrikeLane(from, dir, length, (float)Tuning.BowWidth * A, Damage(Tuning.BowDamage), 220f);
        }
    }

    /// <summary>Martelo: salta do ponto de partida até a marca e afunda o chão no pouso.</summary>
    private Vector2 TickLeap(float dt)
    {
        _leaping -= dt;
        var t = 1f - Mathf.Clamp(_leaping / LeapSeconds, 0f, 1f);
        GlobalPosition = _leapFrom.Lerp(_strikeAt, t);
        Sprite.Position = new Vector2(0, -Mathf.Sin(t * Mathf.Pi) * Human * 0.6f);
        if (_leaping > 0f)
        {
            return Vector2.Zero;
        }

        Sprite.Position = Vector2.Zero;
        CombatSfx.Play(this, CombatSound.Slam, Vector2.Zero);
        World.Feel.Shake(0.4f);
        World.Feel.Spark(_strikeAt, Magenta, 18, 300f * A, Vector2.Up, 90f);
        StrikeCircle(_strikeAt, (float)Tuning.HammerRadius * A, Damage(Tuning.HammerDamage), 380f);
        Enter(EnemyState.Recover);
        return Vector2.Zero;
    }

    /// <summary>Rastro magenta do golpe (corte da Lâmina ou flecha), some rápido.</summary>
    private void StreakLine(Vector2 from, Vector2 dir, float length, float width)
    {
        var line = new Line2D
        {
            Name = "RastroDoReflexo",
            Width = width,
            DefaultColor = Magenta with { A = 0.8f },
            Points = [from, from + (dir * length)],
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        World.AddFx(line);
        var tween = line.CreateTween();
        tween.TweenProperty(line, "modulate:a", 0f, 0.25f);
        tween.TweenCallback(Callable.From(line.QueueFree));
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
        foreach (var marker in _markers)
        {
            marker.Release();
        }

        _markers.Clear();
        _leaping = 0f;
        _dodging = 0f;
        Sprite.Position = Vector2.Zero;
        Sprite.Modulate = Sprite.Modulate with { A = 1f };
        _lamps.GoOut();
        World.Feel.Shake(0.7f);
    }

    public static string AnimFor(ReflexoAttack attack) => attack switch
    {
        ReflexoAttack.Arco => "ataque_arco",
        ReflexoAttack.Martelo => "ataque_martelo",
        _ => "ataque_lamina",
    };

    public static string WeaponName(ReflexoAttack attack) => attack switch
    {
        ReflexoAttack.Arco => "Arco",
        ReflexoAttack.Martelo => "Martelo",
        _ => "Lâmina",
    };

    /// <summary>As lanternas do buff: chamas magenta girando devagar em volta dele, uma por Lanterna perdida (até 12 à vista).</summary>
    private sealed partial class Lamps(int count, float human) : Node2D
    {
        private const int MaxShown = 12;
        private float _spin;
        private float _alpha = 1f;

        public void GoOut() => _alpha = 0.999f;

        public override void _Process(double delta)
        {
            _spin += (float)delta * 0.6f;
            if (_alpha < 1f)
            {
                _alpha = Mathf.Max(0f, _alpha - ((float)delta * 1.5f));
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            var shown = Mathf.Min(count, MaxShown);
            for (var i = 0; i < shown; i++)
            {
                var angle = _spin + (i * Mathf.Tau / shown);
                var at = new Vector2(Mathf.Cos(angle) * human * 0.75f, (Mathf.Sin(angle) * human * 0.3f) - (human * 0.5f));
                var flicker = 0.75f + (0.25f * Mathf.Sin((_spin * 7f) + i));
                DrawCircle(at, human * 0.09f, Magenta with { A = 0.25f * _alpha * flicker });
                DrawCircle(at, human * 0.035f, new Color(1f, 0.75f, 0.9f, _alpha * flicker));
            }
        }
    }
}
