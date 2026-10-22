using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Art;
using Vespera.Core.World;
using Vespera.World;

namespace Vespera.Player;

/// <summary>
/// Visual do caçador. Três camadas sob o <see cref="Body"/>, uma visível por vez:
/// <list type="bullet">
/// <item><see cref="Walker"/> — a arte aprovada do designer (DECISIONS C2-D2/C2-D3, <c>assets/sprites/player/&lt;classe&gt;/frames.tres</c>):
/// andar de 6 quadros por vista nas 5 classes, a 9 fps na velocidade cheia e proporcional à velocidade real (o pé não
/// escorrega), e a esquiva da Lâmina (lado v4; frente/costas v3 provisórias) sincronizada com o dash do combate
/// (<see cref="PlayDodge"/>: o sprite não tem translação, quem anda é a entidade).</item>
/// <item><see cref="Hunter"/> — os quadros do super kit da Lâmina (D43): parado frente/lado/costas, golpe, dano e morte.</item>
/// <item><see cref="Sprite"/> — o atlas da v2 (<c>c_{classe}_*</c>): parado e golpe das outras classes, e o que não tiver arte nova.</item>
/// </list>
/// Parado, o corpo respira de leve (<see cref="ProceduralGait"/>); andando com quadros desenhados não há quique procedural
/// (o andar já tem passada; somar o quique era o "uma imagem em cima da outra"). Poeira e som de passo nos contatos do pé
/// (quadros 0 e 3 do ciclo). Roda em <c>_Process</c>; nada é alocado por quadro.
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
    private const float WalkFps = 9f;
    private const float DodgeWalkFps = 18f; // classes sem esquiva desenhada: a passada acelera durante o dash
    private const int DustEmitters = 4;

    private static readonly StringName FlashParam = "flash";
    private static readonly StringName AnimIdleFront = "idle_front";
    private static readonly StringName AnimIdleSide = "idle_side";
    private static readonly StringName AnimIdleBack = "idle_back";
    private static readonly StringName AnimWalkSide = "walk_side";
    private static readonly StringName AnimWalkFront = "walk_front";
    private static readonly StringName AnimWalkBack = "walk_back";
    private static readonly StringName AnimDodgeSide = "dodge_side";
    private static readonly StringName AnimDodgeFront = "dodge_front";
    private static readonly StringName AnimDodgeBack = "dodge_back";
    private static readonly StringName AnimAttack = "attack";
    private static readonly StringName AnimHurt = "hurt";
    private static readonly StringName AnimDeath = "death";

    private readonly CpuParticles2D[] _dust = new CpuParticles2D[DustEmitters];
    private Player _player = null!;
    private SpriteFrames? _frames;
    private Vector2 _kitAnchor;
    private float _kitDensity = 1f;
    private ShaderMaterial _flashMaterial = null!;
    private string _walkerClass = string.Empty;
    private float _time;
    private float _walkPhase;
    private float _walkCounter;
    private int _lastWalkFrame = -1;
    private int _lastStep;
    private int _dustNext;
    private float _attackT = -1f;
    private float _attackDuration = AttackSeconds;
    private float _hurtT = -1f;
    private float _deathT = -1f;
    private float _dodgeT = -1f;
    private float _dodgeDuration = 0.17f;
    private Vector2 _dodgeDir = Vector2.Down;
    private bool _wasAttackPose;
    private bool _attackCancelled;
    private float _attackPoseT;
    private FacingDirection _lastDirection = FacingDirection.Down;
    private float _lastVelX;
    private Material? _sharedMaterial;

    /// <summary>Nó que espelha e recebe a pose (em volta do pé).</summary>
    public Node2D Body { get; private set; } = null!;

    public AtlasSprite Sprite { get; private set; } = null!;

    /// <summary>Quadros do super kit (só a Lâmina): parado, golpe, dano, morte.</summary>
    public AnimatedSprite2D Hunter { get; private set; } = null!;

    /// <summary>Arte aprovada do designer para a classe atual: andar (todas) e esquiva (Lâmina).</summary>
    public ArtSprite Walker { get; private set; } = null!;

    /// <summary>A pose atual usa os quadros do kit.</summary>
    public bool UsesKitFrames => Hunter.Visible;

    /// <summary>A pose atual usa a arte nova (andar/esquiva desenhados).</summary>
    public bool UsesDrawnFrames => Walker.Visible;

    /// <summary>A classe atual tem andar desenhado.</summary>
    public bool HasWalkArt => Walker.Art is not null;

    /// <summary>Nome da animação desenhada tocando agora ("" quando é o atlas).</summary>
    public string CurrentAnimation => Walker.Visible ? Walker.Shown.ToString() : UsesKitFrames ? Hunter.Animation.ToString() : string.Empty;

    /// <summary>Quadro da animação desenhada mostrada agora.</summary>
    public int CurrentFrame => Walker.Visible ? Walker.Frame : Hunter.Frame;

    /// <summary>Passos dados desde o início (para testes: som e poeira).</summary>
    public int StepsTaken { get; private set; }

    public int DustBursts { get; private set; }

    /// <summary>Trocas de quadro da caminhada desenhada (para testes de fluidez).</summary>
    public int WalkFrameChanges { get; private set; }

    /// <summary>
    /// A pose de golpe está na tela. A esquiva cancela o golpe: durante o dash (e até o combate soltar o
    /// <see cref="Player.AttackPose"/> daquele golpe) o caçador mostra a esquiva, não desliza em pose de ataque.
    /// </summary>
    public bool IsAttacking => !IsDodging && !_attackCancelled && (_attackT >= 0f || _player.AttackPose);

    public bool IsHurt => _hurtT >= 0f;

    public bool IsDead => _deathT >= 0f;

    /// <summary>Esquiva em curso (de <see cref="PlayDodge"/>).</summary>
    public bool IsDodging => _dodgeT >= 0f;

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

        Walker = new ArtSprite { Name = "Walker", Visible = false, Material = _flashMaterial };
        Body.AddChild(Hunter);
        Body.AddChild(Walker);
        CreateDust();
        LoadWalker();
    }

    /// <summary>Caminho do SpriteFrames da arte aprovada da classe.</summary>
    public static string WalkFramesPath(string classId) => $"res://assets/sprites/player/{classId}/frames.tres";

    /// <summary>Golpe: a Lâmina toca os 3 quadros do kit; as outras classes mostram <c>c_*_atk</c> com avanço.</summary>
    public void PlayAttack(float duration)
    {
        if (IsDead)
        {
            return;
        }

        _attackDuration = MathF.Max(0.08f, duration);
        _attackT = 0f;
        _attackCancelled = false;
    }

    /// <summary>
    /// Esquiva (o combate chama quando o dash começa): quadros desenhados na direção <paramref name="direction"/> por
    /// <paramref name="duration"/> s, sem mover o sprite (o deslocamento é da entidade, sem passo duplo).
    /// </summary>
    public void PlayDodge(Vector2 direction, float duration)
    {
        if (IsDead)
        {
            return;
        }

        _dodgeDir = direction == Vector2.Zero ? Vector2.Down : direction.Normalized();
        _dodgeDuration = MathF.Max(0.05f, duration);
        _dodgeT = 0f;
        _attackT = -1f; // a esquiva cancela o golpe: o combate segue com AttackPose por mais um pouco, a pose não
        _attackCancelled = _player.AttackPose;
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
            _attackCancelled = false;
            _hurtT = -1f;
            _dodgeT = -1f;
        }
    }

    public void Revive()
    {
        _deathT = -1f;
        _hurtT = -1f;
        _attackT = -1f;
        _attackCancelled = false;
        _dodgeT = -1f;
    }

    /// <summary>Teleporte/troca de mapa: zera a fase do passo para não sair poeira no destino.</summary>
    public void ResetMotion()
    {
        _lastStep = ProceduralGait.StepIndex(_walkPhase);
        _lastWalkFrame = -1;
        _lastVelX = 0f;
    }

    /// <summary>
    /// Quadro visível agora, para o rastro da esquiva: textura, pé (px) e u por px. Falso sem textura.
    /// </summary>
    public bool TryGetGhost(out Texture2D texture, out Vector2 anchor, out float unitsPerPixel)
    {
        texture = null!;
        anchor = Vector2.Zero;
        unitsPerPixel = 1f;
        if (Walker.Visible && Walker.Art is { } art && Walker.CurrentTexture is { } wt)
        {
            texture = wt;
            anchor = art.Anchor(Walker.Shown);
            unitsPerPixel = art.UnitsPerPixel(Walker.Shown);
            return true;
        }

        if (Hunter.Visible && _frames is not null && _frames.GetFrameTexture(Hunter.Animation, Hunter.Frame) is { } ht)
        {
            texture = ht;
            anchor = _kitAnchor;
            unitsPerPixel = WorldScale.Character / _kitDensity;
            return true;
        }

        if (AtlasLibrary.TryGet(Sprite.Key, out var frame))
        {
            texture = frame.Texture;
            anchor = frame.Anchor;
            unitsPerPixel = WorldScale.For(Sprite.Key, SpriteScaleClass.Auto) / frame.Density;
            return true;
        }

        return false;
    }

    public override void _Process(double delta)
    {
        if (_player is null)
        {
            return;
        }

        if (_walkerClass != _player.ClassId)
        {
            LoadWalker();
        }

        // o combate troca o material do Sprite (flash de dano); as três camadas usam o mesmo
        if (Sprite.Material != _sharedMaterial)
        {
            _sharedMaterial = Sprite.Material;
            Hunter.Material = _sharedMaterial;
            Walker.Material = _sharedMaterial;
        }

        var dt = MathF.Min(MaxDelta, (float)delta);
        _time += dt;
        Advance(ref _attackT, dt, _attackDuration);
        Advance(ref _hurtT, dt, HurtSeconds);
        Advance(ref _dodgeT, dt, _dodgeDuration);
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
        if (!_player.AttackPose && _attackT < 0f)
        {
            _attackCancelled = false; // o golpe cancelado acabou; o próximo mostra a pose
        }

        var velocity = _player.Velocity;
        var maxSpeed = Player.BaseSpeed * _player.SpeedMultiplier;
        var speed01 = Locomotion.SpeedFraction(new MapPoint(velocity.X, velocity.Y), maxSpeed);
        var moving = speed01 > WalkThreshold && !IsDead && !Sitting;
        if (moving)
        {
            _walkPhase += dt * (velocity.Length() / Player.BaseSpeed);
            // quadros desenhados: 9 fps na velocidade cheia da v2, proporcional à velocidade real
            _walkCounter = FrameClock.Advance(_walkCounter, dt, WalkFps, velocity.Length() / Player.BaseSpeed, 6);
        }
        else if (IsDodging)
        {
            _walkCounter = FrameClock.Advance(_walkCounter, dt, DodgeWalkFps, 1f, 6);
        }
        else
        {
            _walkCounter = 0f; // recomeça do contato do pé
        }

        UpdatePose(speed01, moving, velocity);
        UpdateSteps(moving, speed01, velocity);
        _flashMaterial.SetShaderParameter(FlashParam, _hurtT >= 0f ? MathF.Max(0f, 1f - (_hurtT / HurtSeconds)) * 0.85f : 0f);
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

    private void LoadWalker()
    {
        _walkerClass = _player.ClassId;
        var path = WalkFramesPath(_walkerClass);
        if (ArtFrames.Exists(path) && ArtFrames.TryLoad(path, out var art))
        {
            Walker.Setup(art);
        }
        else
        {
            Walker.Clear(); // sem arte: só o atlas e o procedural
        }

        _lastWalkFrame = -1;
    }

    private void UpdatePose(float speed01, bool moving, Vector2 velocity)
    {
        var direction = _player.Direction;
        var attacking = IsAttacking;
        var dodging = IsDodging && !IsDead;
        var view = direction switch
        {
            FacingDirection.Up => GaitView.Back,
            FacingDirection.Down => GaitView.Front,
            _ => GaitView.Side,
        };
        var kit = _frames is not null && _player.ClassId == KitClass;
        var art = Walker.Art;
        var scale = WorldScale.Character;

        // Qual camada: arte nova (andar/esquiva), kit (Lâmina parada/golpe/dano/morte) ou atlas.
        var dodgeView = PropArtStates.Dodge(_dodgeDir.X, _dodgeDir.Y);
        var dodgeAnim = dodgeView switch { DodgeView.Back => AnimDodgeBack, DodgeView.Front => AnimDodgeFront, _ => AnimDodgeSide };
        var drawnDodge = dodging && art is not null && art.Has(dodgeAnim);
        var walkAnim = view switch { GaitView.Back => AnimWalkBack, GaitView.Front => AnimWalkFront, _ => AnimWalkSide };
        if (dodging && !drawnDodge)
        {
            // sem esquiva desenhada: a passada da vista do dash, acelerada
            walkAnim = dodgeView switch { DodgeView.Back => AnimWalkBack, DodgeView.Front => AnimWalkFront, _ => AnimWalkSide };
        }

        var hurtKit = _hurtT >= 0f && kit;
        var drawnWalk = (moving || dodging) && art is not null && art.Has(walkAnim) && !attacking && !IsDead && !hurtKit && !Sitting;
        var useWalker = (drawnDodge || drawnWalk) && !attacking && !IsDead && !hurtKit;
        var useKit = !useWalker && kit;

        var flip = attacking ? MathF.Cos(_player.Aim) < 0 : direction == FacingDirection.Left;
        if (useWalker && (drawnDodge || dodging) && dodgeView == DodgeView.Side)
        {
            flip = _dodgeDir.X < 0f;
        }

        var leanX = Math.Clamp(velocity.X / Player.BaseSpeed, -1f, 1f);

        // Pose base: com quadros desenhados andando, nada de quique procedural (só a respiração parado).
        GaitPose pose;
        if (useWalker)
        {
            pose = GaitPose.Identity;
        }
        else
        {
            var gaitSpeed = moving ? speed01 : 0f;
            pose = ProceduralGait.Sample(useKit && view == GaitView.Side ? GaitView.Side : view, _walkPhase, _time, gaitSpeed, leanX, scale);
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

        if (dodging && !drawnDodge && useWalker)
        {
            // classes sem esquiva desenhada: inclina para o lado do dash e estica um pouco no meio
            var k = MathF.Sin(Math.Clamp(_dodgeT / _dodgeDuration, 0f, 1f) * MathF.PI);
            rotation = 0.12f * k * MathF.Sign(_dodgeDir.X);
            pose = new GaitPose(0f, 0f, 1f + (0.06f * k), 1f - (0.05f * k), 0f);
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
            rotation = kit ? 0f : -MathF.PI / 2f * MathF.Min(1f, _deathT / DeathTurnSeconds);
            pose = GaitPose.Identity;
        }

        Sprite.Key = SpriteKey(direction, attacking);
        Walker.Visible = useWalker;
        Hunter.Visible = useKit;
        Sprite.Visible = !useWalker && !useKit;
        if (useWalker)
        {
            if (drawnDodge)
            {
                Walker.Show(dodgeAnim, FrameClock.Once(_dodgeT, _dodgeDuration, art!.Count(dodgeAnim)));
            }
            else
            {
                var frame = FrameClock.Loop(_walkCounter, art!.Count(walkAnim));
                Walker.Show(walkAnim, frame);
                if (frame != _lastWalkFrame)
                {
                    WalkFrameChanges++;
                }
            }
        }
        else if (useKit)
        {
            ApplyKitFrame(view, attacking);
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

    private void ApplyKitFrame(GaitView view, bool attacking)
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
        // Com quadros desenhados, o passo cai no contato do pé (quadros 0 e 3 do ciclo de 6); sem eles, na fase procedural.
        var step = ProceduralGait.StepIndex(_walkPhase);
        bool contact;
        if (Walker.Visible && !IsDodging)
        {
            var frame = Walker.Frame;
            contact = moving && frame != _lastWalkFrame && (frame == 0 || frame == 3); // o primeiro apoio ao sair do parado também conta
            _lastWalkFrame = frame;
        }
        else
        {
            contact = moving && step != _lastStep;
            _lastWalkFrame = Walker.Visible ? Walker.Frame : -1;
        }

        if (contact)
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
