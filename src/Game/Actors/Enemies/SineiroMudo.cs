using System.Collections.Generic;
using Godot;
using Vespera.Core.Rift;
using Vespera.Game.Combat;
using Vespera.Game.World;
using Vespera.Game.World.Rift;

namespace Vespera.Game.Actors.Enemies;

/// <summary>
/// O Sineiro Mudo (Guardião do Ato V, Sino Mudo): gárgula de pedra do tamanho do sino, sem boca. Nunca fala: a cada fase
/// uma palavra aparece gravada em pedra no alto da arena (<c>ato5/sineiro</c>; ROTEIRO-V2 §13 Ato V, fala 4). Três fases
/// (limiares em <c>boss.phases</c>, CAMPANHA Ato V):
/// <list type="number">
/// <item><b>FESTA</b>: badaladas em área — bate os punhos no chão; anel âmbar em volta antes (0,8 s).</item>
/// <item><b>CORDA</b>: voa e mergulha (sombra âmbar no chão 0,8 s antes) e puxa cordas do escuro: círculos âmbar onde
/// elas caem; quem fica embaixo leva o golpe e fica preso um instante. Partes: 2 asas (<c>boss.cores</c>,
/// <c>boss.coreHp</c>); cada asa quebrada = um mergulho a menos por voo.</item>
/// <item><b>CULPA</b>: silêncio total (sem música, sem som de golpe; <see cref="Audio.Silence"/>) — tudo junto e mais
/// rápido, só com os avisos visuais.</item>
/// </list>
/// </summary>
public partial class SineiroMudo(LevelWorld world) : Enemy("sineiro_mudo", world)
{
    public const string WordsDialogue = "ato5/sineiro";

    private const float TollRange = 380f;
    private const float TollRadius = 300f;
    private const float DiveRadius = 180f;
    private const float FlySeconds = 0.6f;
    private const float FlyHeight = 260f;
    private const float RopeRadius = 85f;
    private const float RopeHold = 1.2f;
    private const float RopeSlow = 0.15f;
    private const float RopeSpread = 260f;
    private const float AttackHold = 0.9f;

    private static readonly Color Stone = new("8a8fa8");
    private static readonly Color Amber = new("f2a541");

    private readonly List<(TelegraphMarker Marker, Vector2 At, float Time)> _ropes = [];
    private TelegraphMarker? _tollMarker;
    private TelegraphMarker? _diveMarker;
    private Vector2 _diveAt;
    private float _cooldown = 2f;
    private int _turn;
    private int _divesLeft;
    private float _air = -1f;
    private double _wingDamage;
    private float _held;
    private bool _silenced;
    private StoneWord? _word;

    /// <summary>1, 2 ou 3.</summary>
    public int Phase { get; private set; } = 1;

    /// <summary>Asas quebradas (0 a <c>boss.cores</c>).</summary>
    public int WingsBroken { get; private set; }

    /// <summary>No ar (voo do mergulho): não leva golpe.</summary>
    public bool Airborne => _air >= 0f;

    /// <summary>Palavra gravada agora (FESTA, CORDA ou CULPA).</summary>
    public string Word { get; private set; } = string.Empty;

    public override bool Unstoppable => true;

    public override bool CanBeHit => base.CanBeHit && !Airborne;

    public override float BodyLift => Sheet.StandHeight * Sheet.SpriteScale * 0.4f;

    private static BossTuning Tuning => RiftSession.Load("sino_mudo").Boss;

    protected override void OnSpawned()
    {
        Enter(EnemyState.Idle);
        Play("parado");
        CarveWord();
    }

    protected override Vector2 Think(float dt)
    {
        TickRopes(dt);
        TickHeld(dt);
        UpdatePhase();
        if (Airborne)
        {
            return TickFlight(dt);
        }

        _cooldown -= dt;
        switch (State)
        {
            case EnemyState.Telegraph:
                if (StateTime >= Def.Telegraph)
                {
                    ReleaseToll();
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

        if (Target is not { } target)
        {
            return Vector2.Zero;
        }

        FaceTowards(target.GlobalPosition);
        var to = target.GlobalPosition - GlobalPosition;
        if (_cooldown <= 0f && ChooseAttack(target, to.Length()))
        {
            return Vector2.Zero;
        }

        if (to.Length() < TollRange * A * 0.6f)
        {
            PlayIf("parado");
            return Vector2.Zero;
        }

        PlayIf("andar");
        return to.Normalized() * (float)Speed * A * (Phase == 3 ? 1.2f : 1f);
    }

    private bool ChooseAttack(Player target, float distance)
    {
        var options = Phase switch
        {
            1 => new[] { "badalar" },
            2 => new[] { "voar", "badalar", "cordas" },
            _ => new[] { "voar", "cordas", "badalar" },
        };
        var pick = options[_turn % options.Length];
        if (pick == "badalar" && distance > TollRange * A)
        {
            pick = Phase >= 2 ? "cordas" : string.Empty;
        }

        if (pick.Length == 0)
        {
            return false;
        }

        _turn++;
        _cooldown = (float)Period * (Phase == 3 ? 0.75f : 1f);
        switch (pick)
        {
            case "badalar":
                StartToll();
                break;
            case "cordas":
                PullRopes(target);
                break;
            default:
                TakeOff();
                break;
        }

        return true;
    }

    private void UpdatePhase()
    {
        var phases = Tuning.Phases;
        var fraction = Hp / MaxHp;
        var next = fraction <= phases[1] ? 3 : fraction <= phases[0] ? 2 : 1;
        if (next <= Phase || Airborne)
        {
            return;
        }

        Phase = next;
        World.Feel.Shake(0.5f);
        if (Phase == 2)
        {
            Story.StoryService.SetFlag("sineiro_fase_2");
        }
        else
        {
            Story.StoryService.SetFlag("sineiro_fase_3");
            // CULPA: o som some de vez (música, golpes, passos). Só os avisos âmbar continuam.
            if (!_silenced)
            {
                _silenced = true;
                Audio.Silence.Begin();
            }
        }

        CarveWord();
    }

    /// <summary>A palavra da fase em pedra (FESTA, CORDA, CULPA), do diálogo <c>ato5/sineiro</c>.</summary>
    private void CarveWord()
    {
        var node = Phase switch { 1 => "a", 2 => "b", _ => "c" };
        var fallback = Phase switch { 1 => "FESTA", 2 => "CORDA", _ => "CULPA" };
        Word = Story.StoryService.LoadDialogue(WordsDialogue).Nodes.TryGetValue(node, out var line) ? line.Text : fallback;
        _word?.QueueFree();
        var area = World.PlayArea;
        _word = new StoneWord(Word, World.Data.HumanHeight * 0.9f)
        {
            Name = "PalavraEmPedra",
            Position = new Vector2(area.GetCenter().X, area.Position.Y + (World.Data.HumanHeight * 1.4f)),
        };
        World.AddGroundFx(_word);
        World.Feel.Shake(0.3f);
        Boot.Log.Info($"sineiro mudo: fase {Phase}, palavra '{Word}'");
    }

    // ---------------------------------------------------------------- FESTA: badalada

    private void StartToll()
    {
        Enter(EnemyState.Telegraph);
        Play("badalar");
        Flash.Tint = 0.35f;
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero);
        _tollMarker = WarnCircle(GlobalPosition, TollRadius * A, (float)Def.Telegraph);
    }

    private void ReleaseToll()
    {
        Flash.Tint = 0f;
        _tollMarker?.Release();
        _tollMarker = null;
        Enter(EnemyState.Recover);
        CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero);
        World.Feel.Shake(0.4f);
        for (var i = 1; i <= 3; i++)
        {
            World.Feel.Ring(GlobalPosition, TollRadius * A * i / 3f, Amber, 0.3f + (0.1f * i), 7f * A);
        }

        StrikeCircle(GlobalPosition, TollRadius * A, 1.2, 400f);
    }

    // ---------------------------------------------------------------- CORDA: voo, mergulho e cordas

    private void TakeOff()
    {
        _divesLeft = Mathf.Max(1, 2 - WingsBroken + (Phase == 3 ? 1 : 0));
        Enter(EnemyState.Attack);
        Play("voar");
        CombatSfx.Play(this, CombatSound.Dodge, Vector2.Zero, -2f);
        _air = 0f;
        _diveMarker = null;
        CollisionLayer = 0;
    }

    private Vector2 TickFlight(float dt)
    {
        _air += dt;
        var lift = Mathf.Clamp(_air / FlySeconds, 0f, 1f);
        if (_diveMarker is null)
        {
            Sprite.Position = new Vector2(0, -FlyHeight * A * lift);
            if (_air >= FlySeconds && Target is { } target)
            {
                // Sombra no chão onde vai cair (0,8 s).
                _diveAt = World.ClampToFloor(target.GlobalPosition);
                _diveMarker = WarnCircle(_diveAt, DiveRadius * A, (float)Def.Telegraph);
                _air = FlySeconds;
            }

            return Vector2.Zero;
        }

        // Paira em cima da sombra até o aviso acabar.
        var to = _diveAt - GlobalPosition;
        if (_air < FlySeconds + Def.Telegraph)
        {
            return to.Length() > 6f ? to.Normalized() * Mathf.Min(to.Length() / dt, 900f * A) : Vector2.Zero;
        }

        Dive();
        return Vector2.Zero;
    }

    private void Dive()
    {
        _diveMarker?.Release();
        _diveMarker = null;
        GlobalPosition = _diveAt;
        Sprite.Position = Vector2.Zero;
        Play("mergulhar");
        CombatSfx.Play(this, CombatSound.BossLand, Vector2.Zero);
        World.Feel.Shake(0.45f);
        World.Feel.Ring(_diveAt, DiveRadius * A, Stone, 0.4f, 9f * A);
        World.Feel.Spark(_diveAt, Stone, 18, 280f * A, Vector2.Up, 80f);
        StrikeCircle(_diveAt, DiveRadius * A, 1.3, 420f);
        _divesLeft--;
        if (_divesLeft > 0)
        {
            _air = 0f;
            Play("voar");
            return;
        }

        _air = -1f;
        CollisionLayer = EnemyLayer;
        Enter(EnemyState.Recover);
    }

    private void PullRopes(Player target)
    {
        Enter(EnemyState.Attack);
        Play("cordas");
        CombatSfx.Play(this, CombatSound.Telegraph, Vector2.Zero, -4f);
        var count = Phase == 3 ? 4 : 3;
        for (var i = 0; i < count; i++)
        {
            var spread = i == 0 ? Vector2.Zero : Vector2.FromAngle(Mathf.Tau * i / (count - 1) + (GD.Randf() * 0.6f)) * RopeSpread * A;
            var at = World.ClampToFloor(target.GlobalPosition + spread);
            var seconds = (float)Def.Telegraph + (i * 0.12f);
            _ropes.Add((WarnCircle(at, RopeRadius * A, seconds), at, seconds));
        }
    }

    private void TickRopes(float dt)
    {
        for (var i = _ropes.Count - 1; i >= 0; i--)
        {
            var (marker, at, time) = _ropes[i];
            time -= dt;
            if (time > 0f)
            {
                _ropes[i] = (marker, at, time);
                continue;
            }

            marker.Release();
            _ropes.RemoveAt(i);
            DrawRope(at);
            if (StrikeCircle(at, RopeRadius * A, 0.7, 0f) && Target is { } target)
            {
                // Preso pela corda: quase não anda por um instante.
                _held = RopeHold;
                target.Wade = RopeSlow;
                World.Feel.Number(target.Combat.HitCenter - new Vector2(0, 30f * A), "preso", Amber, 0.7f);
            }
        }
    }

    private void DrawRope(Vector2 at)
    {
        var rope = new Line2D
        {
            Name = "Corda",
            Width = 10f * A,
            DefaultColor = new Color("8a5a3c"),
            Points = [at - new Vector2(0, World.Data.HumanHeight * 6f), at],
        };
        World.AddGroundFx(rope);
        var tween = rope.CreateTween();
        tween.TweenInterval(RopeHold);
        tween.TweenProperty(rope, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(rope.QueueFree));
    }

    private void TickHeld(float dt)
    {
        if (_held <= 0f)
        {
            return;
        }

        _held -= dt;
        if (_held <= 0f && World.Player is { } player)
        {
            player.Wade = 1f;
        }
    }

    protected override void OnHurt(in HitInfo hit)
    {
        var wings = Tuning.Cores;
        if (WingsBroken >= wings || Tuning.CoreHp <= 0)
        {
            return;
        }

        _wingDamage += hit.Damage;
        if (_wingDamage < Tuning.CoreHp)
        {
            return;
        }

        _wingDamage = 0;
        WingsBroken++;
        World.Feel.Shake(0.25f);
        World.Feel.Spark(HitCenter, Stone, 18, 300f * A, Vector2.Up, 80f);
        World.Feel.Toast(WingsBroken < wings
            ? $"Uma asa de pedra quebrou ({WingsBroken}/{wings}). Ele mergulha menos."
            : "As duas asas quebraram. Ele mal sai do chão.");
    }

    private void PlayIf(string anim)
    {
        if (Sprite.Animation != anim)
        {
            Play(anim);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        EndSilence();
        if (_held > 0f && World.Player is { } player && IsInstanceValid(player))
        {
            player.Wade = 1f;
        }
    }

    private void EndSilence()
    {
        if (_silenced)
        {
            _silenced = false;
            Audio.Silence.End();
        }
    }

    protected override void OnDied()
    {
        foreach (var (marker, _, _) in _ropes)
        {
            marker.Release();
        }

        _ropes.Clear();
        _tollMarker?.Release();
        _diveMarker?.Release();
        _air = -1f;
        Sprite.Position = Vector2.Zero;
        if (_held > 0f && World.Player is { } player)
        {
            player.Wade = 1f;
        }

        _held = 0f;
        EndSilence();
        World.Feel.Shake(0.8f);
        World.Feel.Toast("A pedra racha inteira, sem um som. A corda cai por último.");
    }

    /// <summary>Uma palavra gravada em pedra no alto da arena: letras cinza com sombra escura, sem som.</summary>
    private sealed partial class StoneWord(string word, float size) : Node2D
    {
        private float _t;

        public override void _Process(double delta)
        {
            _t = Mathf.Min(1f, _t + ((float)delta * 1.5f));
            QueueRedraw();
        }

        public override void _Draw()
        {
            var font = ThemeDB.FallbackFont;
            var fontSize = Mathf.RoundToInt(size);
            var width = font.GetStringSize(word, HorizontalAlignment.Left, -1, fontSize).X;
            var at = new Vector2(-width * 0.5f, 0);
            var alpha = _t;
            DrawString(font, at + new Vector2(3, 4), word, HorizontalAlignment.Left, -1, fontSize, new Color(0.08f, 0.07f, 0.12f, 0.85f * alpha));
            DrawString(font, at, word, HorizontalAlignment.Left, -1, fontSize, Stone with { A = alpha });
            DrawString(font, at - new Vector2(1, 1), word, HorizontalAlignment.Left, -1, fontSize, new Color(0.8f, 0.8f, 0.86f, 0.35f * alpha));
        }
    }
}
