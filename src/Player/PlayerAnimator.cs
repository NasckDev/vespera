using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Player;

/// <summary>
/// Visual do caçador (DECISIONS D43). A Lâmina usa os quadros recortados do super kit
/// (<c>assets/sprites/player/hunter_frames.tres</c>: parado frente/lado/costas, 4 quadros de caminhada de lado, golpe,
/// dano e morte, pé numa linha comum). Frente/costas andando e as outras classes (atlas <c>c_{classe}_*</c>) ganham
/// caminhada procedural (<see cref="ProceduralGait"/>: passo com squash leve, quique, balanço para o pé de apoio e
/// inclinação na direção do movimento). Poeira nos pés a cada passo e ao virar, som de passo, clarão branco de dano.
/// Roda em <c>_Process</c> (o corpo não usa interpolação de física; o nó do jogador sim).
/// </summary>
public partial class PlayerAnimator : Node
{
    public const string HunterFramesPath = "res://assets/sprites/player/hunter_frames.tres";
    public const string KitClass = "sword";

    private const float MaxDelta = 0.05f;
    private const float AttackSeconds = 0.3f;
    private const float HurtSeconds = 0.32f;
    private const float DeathKneelSeconds = 0.35f;
    private const float DeathTurnSeconds = 1f / 3f; // render.js:170 (gira até −90° em 1/3 s)
    private const float WalkThreshold = 0.12f;
    private const int DustEmitters = 4;

    private static readonly StringName FlashParam = "flash";
    private static readonly StringName AnimIdleFront = "idle_front";
    private static readonly StringName AnimIdleSide = "idle_side";
    private static readonly StringName AnimIdleBack = "idle_back";
    private static readonly StringName AnimWalkSide = "walk_side";
    private static readonly StringName AnimAttack = "attack";
    private static readonly StringName AnimHurt = "hurt";
    private static readonly StringName AnimDeath = "death";

    private readonly CpuParticles2D[] _dust = new CpuParticles2D[DustEmitters];
    private Player _player = null!;
    private SpriteFrames? _frames;
    private Vector2 _kitAnchor;
    private float _kitDensity = 1f;
    private ShaderMaterial _flashMaterial = null!;
    private float _time;
    private float _walkPhase;
    private int _lastStep;
    private int _dustNext;
    private float _attackT = -1f;
    private float _attackDuration = AttackSeconds;
    private float _hurtT = -1f;
    private float _deathT = -1f;
    private bool _wasAttackPose;
    private float _attackPoseT;
    private FacingDirection _lastDirection = FacingDirection.Down;
    private float _lastVelX;

    /// <summary>Nó que espelha e recebe a pose (em volta do pé).</summary>
    public Node2D Body { get; private set; } = null!;

    public AtlasSprite Sprite { get; private set; } = null!;

    /// <summary>Quadros do kit (só a Lâmina).</summary>
    public AnimatedSprite2D Hunter { get; private set; } = null!;

    /// <summary>A pose atual usa os quadros do kit.</summary>
    public bool UsesKitFrames => Hunter.Visible;

    /// <summary>Nome da animação do kit tocando agora ("" sem kit).</summary>
    public string CurrentAnimation => UsesKitFrames ? Hunter.Animation.ToString() : string.Empty;

    /// <summary>Passos dados desde o início (para testes: som e poeira).</summary>
    public int StepsTaken { get; private set; }

    public int DustBursts { get; private set; }

    public bool IsAttacking => _attackT >= 0f || _player.AttackPose;

    public bool IsHurt => _hurtT >= 0f;

    public bool IsDead => _deathT >= 0f;

    /// <summary>Sentado (banco do layout): corpo um pouco mais baixo e sem quique.</summary>
    public bool Sitting { get; set; }

    /// <summary>Altura do corpo em u (para oclusão e prompts).</summary>
    public float VisualHeight => WorldScale.CharacterHeight;

    public void Setup(Player player, Node2D body, AtlasSprite sprite)
    {
        _player = player;
        Body = body;
        Sprite = sprite;
        Body.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off; // pose por quadro (_Process)

        _flashMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/World/Shaders/flash.gdshader") };
        Sprite.Material = _flashMaterial;

        Hunter = new AnimatedSprite2D { Name = "Hunter", Centered = false, Visible = false, Material = _flashMaterial };
        if (ResourceLoader.Exists(HunterFramesPath))
        {
            _frames = GD.Load<SpriteFrames>(HunterFramesPath);
            _kitAnchor = _frames.GetMeta("anchor", Vector2.Zero).AsVector2();
            _kitDensity = (float)_frames.GetMeta("density", 1.0).AsDouble();
            Hunter.SpriteFrames = _frames;
            Hunter.Offset = -_kitAnchor;
        }
        else
        {
            Log.Warning($"Quadros do caçador ausentes ({HunterFramesPath}); a Lâmina usa o atlas.");
        }

        Body.AddChild(Hunter);
        CreateDust();
    }

    /// <summary>Golpe: a Lâmina toca os 3 quadros do kit; as outras classes mostram <c>c_*_atk</c> com avanço.</summary>
    public void PlayAttack(float duration)
    {
        if (IsDead)
        {
            return;
        }

        _attackDuration = MathF.Max(0.08f, duration);
        _attackT = 0f;
    }

    public void PlayHurt()
    {
        if (!IsDead)
        {
            _hurtT = 0f;
        }
    }

    public void PlayDeath()
    {
        if (!IsDead)
        {
            _deathT = 0f;
            _attackT = -1f;
            _hurtT = -1f;
        }
    }

    public void Revive()
    {
        _deathT = -1f;
        _hurtT = -1f;
        _attackT = -1f;
    }

    /// <summary>Teleporte/troca de mapa: zera a fase do passo para não sair poeira no destino.</summary>
    public void ResetMotion()
    {
        _lastStep = ProceduralGait.StepIndex(_walkPhase);
        _lastVelX = 0f;
    }

    public override void _Process(double delta)
    {
        if (_player is null)
        {
            return;
        }

        var dt = MathF.Min(MaxDelta, (float)delta);
        _time += dt;
        Advance(ref _attackT, dt, _attackDuration);
        Advance(ref _hurtT, dt, HurtSeconds);
        if (_deathT >= 0f)
        {
            _deathT += dt;
        }

        if (_player.AttackPose && !_wasAttackPose)
        {
            _attackPoseT = 0f;
        }

        _wasAttackPose = _player.AttackPose;
        _attackPoseT += dt;

        var velocity = _player.Velocity;
        var speed01 = Locomotion.SpeedFraction(new MapPoint(velocity.X, velocity.Y), Player.BaseSpeed * _player.SpeedMultiplier);
        var moving = speed01 > WalkThreshold && !IsDead;
        if (moving)
        {
            _walkPhase += dt * (velocity.Length() / Player.BaseSpeed);
        }

        UpdateSteps(moving, speed01, velocity);
        UpdatePose(speed01, moving, velocity);
        _flashMaterial.SetShaderParameter(FlashParam, (_hurtT >= 0f ? MathF.Max(0f, 1f - (_hurtT / HurtSeconds)) * 0.85f : 0f) * GameSettings.HitFlashScale); // POLI-7
    }

    private static void Advance(ref float t, float dt, float duration)
    {
        if (t < 0f)
        {
            return;
        }

        t += dt;
        if (t >= duration)
        {
            t = -1f;
        }
    }

    private void UpdatePose(float speed01, bool moving, Vector2 velocity)
    {
        var direction = _player.Direction;
        var attacking = IsAttacking;
        var view = direction switch
        {
            FacingDirection.Up => GaitView.Back,
            FacingDirection.Down => GaitView.Front,
            _ => GaitView.Side,
        };
        var flip = attacking ? MathF.Cos(_player.Aim) < 0 : direction == FacingDirection.Left;
        var useKit = _frames is not null && _player.ClassId == KitClass;
        var scale = WorldScale.Character;
        var leanX = Math.Clamp(velocity.X / Player.BaseSpeed, -1f, 1f);

        // Pose base: caminhada procedural (frente/costas e atlas) ou só quique leve (caminhada de lado do kit já tem passada).
        var gaitView = useKit && view == GaitView.Side ? GaitView.Side : view;
        var pose = ProceduralGait.Sample(gaitView, _walkPhase, _time, moving ? speed01 : 0f, leanX, scale);
        if (useKit && view == GaitView.Side && moving)
        {
            pose = pose with { OffsetY = pose.OffsetY * 0.35f, ScaleY = 1f + ((pose.ScaleY - 1f) * 0.4f), ScaleX = 1f + ((pose.ScaleX - 1f) * 0.4f) };
        }

        var rotation = pose.Rotation;
        var offset = new Vector2(pose.OffsetX, pose.OffsetY);
        if (attacking)
        {
            // avanço curto na direção da mira, como o lunge do golpe
            var k = AttackProgress();
            var lunge = MathF.Sin(k * MathF.PI) * 2.2f * scale;
            offset += new Vector2(MathF.Cos(_player.Aim), MathF.Sin(_player.Aim) * 0.5f) * lunge;
            rotation = 0f;
        }

        if (_hurtT >= 0f)
        {
            var h = 1f - (_hurtT / HurtSeconds);
            offset.X += MathF.Sin(_hurtT * 70f) * 1.2f * h * scale;
            rotation = 0f;
        }

        if (Sitting && !IsDead)
        {
            offset = new Vector2(0f, 2.5f * scale);
            rotation = 0f;
            pose = new GaitPose(0f, 0f, 1.04f, 0.9f, 0f);
        }

        if (IsDead)
        {
            offset = Vector2.Zero;
            rotation = useKit ? 0f : -MathF.PI / 2f * MathF.Min(1f, _deathT / DeathTurnSeconds);
            pose = GaitPose.Identity;
        }

        Sprite.Key = SpriteKey(direction, attacking);
        Hunter.Visible = useKit;
        Sprite.Visible = !useKit;
        if (useKit)
        {
            ApplyKitFrame(view, moving, attacking);
            Hunter.Scale = Vector2.One * (scale / _kitDensity);
        }

        Body.Position = offset;
        Body.Rotation = rotation;
        Body.Scale = new Vector2((flip ? -1f : 1f) * pose.ScaleX, pose.ScaleY);
    }

    private float AttackProgress()
    {
        if (_attackT >= 0f)
        {
            return Math.Clamp(_attackT / _attackDuration, 0f, 1f);
        }

        // AttackPose ligado por fora (combate) sem PlayAttack: repete o golpe enquanto durar
        return (_attackPoseT % AttackSeconds) / AttackSeconds;
    }

    private void ApplyKitFrame(GaitView view, bool moving, bool attacking)
    {
        if (IsDead)
        {
            SetKit(AnimDeath, _deathT < DeathKneelSeconds ? 0 : 1);
            return;
        }

        if (_hurtT >= 0f)
        {
            SetKit(AnimHurt, 0);
            return;
        }

        if (attacking)
        {
            SetKit(AnimAttack, Math.Min(2, (int)(AttackProgress() * 3f)));
            return;
        }

        if (moving && view == GaitView.Side)
        {
            SetKit(AnimWalkSide, ProceduralGait.CycleFrame(_walkPhase, 4));
            return;
        }

        SetKit(view switch { GaitView.Back => AnimIdleBack, GaitView.Side => AnimIdleSide, _ => AnimIdleFront }, 0);
    }

    private void SetKit(StringName animation, int frame)
    {
        if (Hunter.Animation != animation)
        {
            Hunter.Animation = animation;
        }

        if (Hunter.Frame != frame)
        {
            Hunter.Frame = frame;
        }
    }

    private string SpriteKey(FacingDirection direction, bool attacking)
    {
        if (IsDead)
        {
            return _player.PoseKey(0); // render.js:170: c_{arma}_front girado
        }

        if (attacking)
        {
            return _player.PoseKey(3);
        }

        return direction switch
        {
            FacingDirection.Up => _player.PoseKey(1),
            FacingDirection.Down => _player.PoseKey(0),
            _ => _player.PoseKey(2),
        };
    }

    // --- passos e poeira ---

    private void UpdateSteps(bool moving, float speed01, Vector2 velocity)
    {
        var step = ProceduralGait.StepIndex(_walkPhase);
        if (moving && step != _lastStep)
        {
            StepsTaken++;
            if (speed01 > 0.5f)
            {
                EmitDust(0.6f);
                AudioManager.Instance?.PlaySfx("step", _player.GlobalPosition, pitchJitter: 0.12f);
            }
        }

        _lastStep = step;

        // virar: inverteu a direção horizontal ou trocou de vista andando → poeira mais forte
        var turned = moving && ((_lastVelX > 20f && velocity.X < -20f) || (_lastVelX < -20f && velocity.X > 20f) || IsReversal(_lastDirection, _player.Direction));
        if (turned)
        {
            EmitDust(1f);
        }

        _lastDirection = _player.Direction;
        _lastVelX = velocity.X;
    }

    private static bool IsReversal(FacingDirection a, FacingDirection b) =>
        (a == FacingDirection.Up && b == FacingDirection.Down) || (a == FacingDirection.Down && b == FacingDirection.Up)
        || (a == FacingDirection.Left && b == FacingDirection.Right) || (a == FacingDirection.Right && b == FacingDirection.Left);

    private void CreateDust()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.86f, 0.76f, 0.62f, 0.55f));
        gradient.SetColor(1, new Color(0.86f, 0.76f, 0.62f, 0f));
        for (var i = 0; i < _dust.Length; i++)
        {
            var p = new CpuParticles2D
            {
                Name = $"Dust{i}",
                Emitting = false,
                OneShot = true,
                Amount = 6,
                Lifetime = 0.45,
                Explosiveness = 0.85f,
                LocalCoords = false,
                ShowBehindParent = true,
                Direction = new Vector2(0, -1),
                Spread = 80f,
                Gravity = new Vector2(0, 30),
                InitialVelocityMin = 6f,
                InitialVelocityMax = 18f,
                ScaleAmountMin = 1.2f,
                ScaleAmountMax = 2.4f,
                ColorRamp = gradient,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(5, 1.5f),
            };
            _dust[i] = p;
            _player.AddChild(p);
        }
    }

    private void EmitDust(float strength)
    {
        var p = _dust[_dustNext];
        _dustNext = (_dustNext + 1) % _dust.Length;
        p.GlobalPosition = _player.GlobalPosition + new Vector2(0, 1);
        p.ScaleAmountMax = 2.4f * strength * WorldScale.Character / 1.75f;
        p.Restart();
        DustBursts++;
    }
}
