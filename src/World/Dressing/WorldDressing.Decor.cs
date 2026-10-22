using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Art;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World;

/// <summary>
/// Decoração com interação do layout (<c>decor</c>, D45/D46): lampião (acender/apagar o halo), flor (colher e crescer
/// de novo), balsa (desliza até <c>to</c>), pesca (mini-interação de timing → peixe), banco (sentar) e placa, fonte,
/// barraca e "olhar" (texto curto no mundo). Cada uso emite <see cref="EventBus.WorldObjectUsed"/>.
/// </summary>
public partial class WorldDressing
{
    private const string ActivityBlock = "world-activity";
    private const float FerrySpeed = 34f; // u/s
    private const float LampSearchRadius = 48f;

    private Activity _activity;
    private DecorSpot? _activeSpot;
    private FishingGame? _fishing;
    private float _activityT;
    private float _activityEnd = -1f;
    private Vector2 _ferryFrom;
    private Vector2 _ferryTo;
    private float _ferryDuration;
    private float _ferryPhase;
    private uint _savedMask;
    private Node2D? _ferryHull;
    private Sprite2D? _ferryBoat;
    private ArtFrames? _ferryArt;
    private Line2D? _fishingLine;
    private Sprite2D? _fishingRod;
    private Vector2 _rodTip;

    private static readonly StringName BoatRowing = "rowing";
    private static readonly StringName BoatReflection = "reflection";
    private static AtlasTexture? _rodRight;
    private static AtlasTexture? _rodLeft;
    private static Vector2 _rodGripRight;
    private static Vector2 _rodGripLeft;
    private static Vector2 _rodTipRight;
    private static Vector2 _rodTipLeft;
    private static float _rodUnitsPerPixel = 0.07f;

    /// <summary>Barco desenhado da travessia em curso (<c>raft</c> ou <c>rowboat</c>; "" sem arte ou parado).</summary>
    public string FerryBoat { get; private set; } = string.Empty;

    /// <summary>Quadro da remada mostrado agora (−1 fora da travessia).</summary>
    public int FerryFrame { get; private set; } = -1;

    /// <summary>A vara desenhada está na mão (pescando).</summary>
    public bool RodInHand => _fishingRod is { Visible: true };
    private Vector2 _bobber;

    private enum Activity
    {
        None,
        Fishing,
        Ferry,
        Sitting,
    }

    /// <summary>Pescando, na balsa ou sentado (o jogador não anda).</summary>
    public bool InActivity => _activity != Activity.None;

    public string ActivityName => _activity.ToString();

    /// <summary>Pesca em andamento (para o smoke e a UI).</summary>
    public FishingGame? Fishing => _fishing;

    public System.Collections.Generic.IReadOnlyList<DecorSpot> Decor => _decor;

    /// <summary>Usa a decoração <paramref name="index"/> do mapa atual como se o jogador tivesse interagido (smoke).</summary>
    public void UseDecor(int index)
    {
        if (index >= 0 && index < _decor.Count)
        {
            Use(_decor[index]);
        }
    }

    private DecorSpot AddDecor(MapDef map, LayoutDecor def, int index, string key, float promptHeight = 0f)
    {
        var spot = new DecorSpot(def, index, key);
        var interact = new WorldInteractable
        {
            Name = $"Decor_{def.Kind}_{index}",
            Kind = string.Empty,
            Id = key,
            Radius = def.Radius,
            Position = new Vector2(def.X, def.Y),
            PromptHeight = promptHeight > 0f ? promptHeight : DefaultPromptHeight(def.Kind),
            Prompt = def.Label ?? DefaultLabel(def.Kind),
        };
        interact.Handler = _ => Use(spot);
        spot.Interactable = interact;
        _decorRoot.AddChild(interact);

        switch (def.Kind)
        {
            case "lamp":
                spot.Light = Lights.NearestLight(new Vector2(def.X, def.Y - 20f), LampSearchRadius);
                if (spot.Light < 0)
                {
                    spot.Light = Lights.AddLight(new Vector2(def.X, def.Y - 30f), 46f, "a");
                }

                var lit = World.Objects.GetFlag(key, true);
                Lights.SetLit(spot.Light, lit);
                interact.Prompt = def.Label ?? (lit ? "Apagar lampião" : "Acender lampião");
                break;
            case "flower":
                spot.Sparkle = CreateSparkle(new Vector2(def.X, def.Y - 4f));
                _decorRoot.AddChild(spot.Sparkle);
                break;
            case "boat" when def.To is null:
                Log.Warning($"{map.Id}: balsa em ({def.X}, {def.Y}) sem 'to'; desligada.");
                interact.Enabled = false;
                break;
        }

        _decor.Add(spot);
        return spot;
    }

    /// <summary>Flores colhidas voltam com o tempo (<c>regrowSeconds</c>) ou no dia seguinte.</summary>
    private void RefreshSpent()
    {
        var regrow = WorldScale.Rules?.Flower.RegrowSeconds ?? 90.0;
        foreach (var spot in _decor)
        {
            if (spot.Def.Kind != "flower")
            {
                continue;
            }

            var spent = World.Objects.IsSpent(spot.Key, CurrentDay, Now, regrow);
            spot.Interactable.Enabled = !spent;
            if (spot.Object is { } flower)
            {
                // arte de estados (C2-D5): colhida logo depois, broto no caminho, florida de novo quando volta
                var since = spent ? regrow - World.Objects.RemainingSeconds(spot.Key, CurrentDay, Now, regrow) : 0.0;
                flower.SetFlowerStage(Core.Art.PropArtStates.Flower(spent, since, regrow));
            }

            if (spot.Sparkle is { } s)
            {
                s.Emitting = !spent;
                s.Visible = !spent;
            }
        }
    }

    private void Use(DecorSpot spot)
    {
        if (InActivity || !_built)
        {
            return;
        }

        var def = spot.Def;
        var at = new Vector2(def.X, def.Y);
        EventBus.Instance.EmitSignal(EventBus.SignalName.WorldObjectUsed, def.Kind, _world.MapId, spot.Index);
        switch (def.Kind)
        {
            case "lamp":
                var lit = !World.Objects.GetFlag(spot.Key, true);
                World.Objects.SetFlag(spot.Key, lit);
                Lights.SetLit(spot.Light, lit);
                spot.Interactable.Prompt = def.Label ?? (lit ? "Apagar lampião" : "Acender lampião");
                AudioManager.Instance?.PlaySfx(lit ? "magic" : "ui", at, pitchJitter: 0.1f);
                break;
            case "flower":
                var rules = WorldScale.Rules?.Flower;
                WorldLoot.Grant(LootDrop.Material(rules?.Material ?? "erva", rules?.Amount ?? 1), at + new Vector2(0, -6f), Popups);
                World.Objects.MarkSpent(spot.Key, CurrentDay, Now);
                RefreshSpent();
                break;
            case "boat" when def.To is { } to:
                StartFerry(spot, at, new Vector2(to.X, to.Y));
                break;
            case "fishing":
                StartFishing(spot);
                break;
            case "bench":
                StartSitting(spot);
                break;
            default: // sign, fountain, stall, inspect
                if (spot.Object is { ArtRole: PropArtRole.Lever or PropArtRole.Door } mechanism)
                {
                    // alavanca e porta com arte de estados alternam ao usar (o texto continua)
                    var on = mechanism.Toggle();
                    AudioManager.Instance?.PlaySfx(mechanism.ArtRole == PropArtRole.Door ? "open" : "hit", at, pitchJitter: on ? 0.05f : 0.15f);
                }

                Lore.ShowText(def.Text ?? DefaultText(def.Kind), at, spot.Interactable.PromptHeight + 4f, def.Radius);
                AudioManager.Instance?.PlaySfx("ui", at, pitchJitter: 0f);
                break;
        }
    }

    // --- atividades (pesca, balsa, banco) ---

    private void StartFishing(DecorSpot spot)
    {
        var rules = WorldScale.Rules?.Fishing;
        _fishing = new FishingGame(
            rules?.BiteDelay ?? new Core.Common.NumRange(1.2, 2.8),
            rules?.ZoneWidth ?? 0.24,
            rules?.SweepSeconds ?? 1.1,
            rules?.MaxSweeps ?? 3,
            _random);
        Begin(Activity.Fishing, spot);
        var player = _world.Player;
        var facing = player.Facing == Vector2.Zero ? Vector2.Down : player.Facing.Normalized();
        _bobber = NearestWater(player.GlobalPosition) ?? player.GlobalPosition + (facing * 26f) + new Vector2(0, 4f);
        _fishingLine = new Line2D { Name = "FishingLine", Width = 0.8f, DefaultColor = new Color(0.95f, 0.92f, 0.8f, 0.8f), ZIndex = 3, Points = new Vector2[3] };
        _decorRoot.AddChild(_fishingLine);
        CreateRod(facing.X < 0f || (facing.X == 0f && _bobber.X < player.GlobalPosition.X));
        UpdateFishingLine(false);
        FishingBar.Show(_world.Player.GlobalPosition - new Vector2(0, WorldScale.CharacterHeight + 12f), _fishing.ZoneStart, _fishing.ZoneWidth);
        AudioManager.Instance?.PlaySfx("swing", _world.Player.GlobalPosition, pitchJitter: 0.1f);
    }

    private void StartFerry(DecorSpot spot, Vector2 from, Vector2 to)
    {
        var player = _world.Player;
        _ferryFrom = player.GlobalPosition;
        _ferryTo = to;
        _ferryDuration = Math.Clamp(_ferryFrom.DistanceTo(to) / FerrySpeed, 1.2f, 6f);
        _ferryPhase = 0f;
        _savedMask = player.CollisionMask;
        player.CollisionMask = 0; // atravessa a água bloqueada
        _ferryHull = CreateBoat(spot, to.X < from.X) ?? CreateHull();
        _world.Entities.AddChild(_ferryHull);
        Begin(Activity.Ferry, spot);
        AudioManager.Instance?.PlaySfx("dash", from, pitchJitter: 0.1f);
    }

    private void StartSitting(DecorSpot spot)
    {
        Begin(Activity.Sitting, spot);
        _world.Player.Animator.Sitting = true;
        if (spot.Def.Text is { } text)
        {
            Lore.ShowText(text, new Vector2(spot.Def.X, spot.Def.Y), spot.Interactable.PromptHeight + 4f, spot.Def.Radius);
        }
    }

    private void Begin(Activity activity, DecorSpot spot)
    {
        _activity = activity;
        _activeSpot = spot;
        _activityT = 0f;
        _activityEnd = -1f;
        _world.Player.MovementLocked = true;
        GameManager.Instance.BlockGameplay(ActivityBlock);
    }

    private void EndActivity()
    {
        if (_activity == Activity.None)
        {
            return;
        }

        var player = _world.Player;
        if (_activity == Activity.Ferry)
        {
            player.CollisionMask = _savedMask;
            player.GlobalPosition = _world.CorrectToWalkable(player.GlobalPosition);
            _ferryHull?.QueueFree();
            _ferryHull = null;
            _ferryBoat = null;
            _ferryArt = null;
            FerryBoat = string.Empty;
            FerryFrame = -1;
        }

        if (IsInstanceValid(player))
        {
            player.MovementLocked = false;
            player.Animator.Sitting = false;
        }

        _fishingLine?.QueueFree();
        _fishingLine = null;
        _fishingRod?.QueueFree();
        _fishingRod = null;
        _activity = Activity.None;
        _activeSpot = null;
        _fishing = null;
        FishingBar.Visible = false;
        GameManager.Instance?.UnblockGameplay(ActivityBlock);
    }

    private void ProcessActivity(float dt)
    {
        if (_activity == Activity.None)
        {
            return;
        }

        _activityT += dt;
        if (_activityEnd >= 0f && _activityT >= _activityEnd)
        {
            EndActivity();
            return;
        }

        switch (_activity)
        {
            case Activity.Fishing when _fishing is { } game && _activityEnd < 0f:
                game.Update(dt);
                UpdateFishingLine(game.State == FishingState.Biting);
                if (game.State == FishingState.Biting)
                {
                    FishingBar.SetBiting(game.Needle, game.NeedleInZone);
                }
                else if (game.State == FishingState.Escaped)
                {
                    FinishFishing(game.State);
                }

                if (Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown).LengthSquared() > 0.25f)
                {
                    FishingBar.SetCaption("Desistiu.");
                    _activityEnd = _activityT + 0.25f;
                }

                break;
            case Activity.Sitting:
                if (_activityT > 0.4f && Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown).LengthSquared() > 0.25f)
                {
                    EndActivity();
                }

                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_activity != Activity.Ferry || _world is null)
        {
            return;
        }

        // na física (antes do jogador na árvore): a interpolação desenha o deslize liso entre os ticks
        var t = Math.Clamp(_activityT / _ferryDuration, 0f, 1f);
        var k = t * t * (3f - (2f * t));
        var bob = MathF.Sin(_activityT * 5f) * 0.6f;
        var p = _ferryFrom.Lerp(_ferryTo, k) + new Vector2(0, bob);
        _world.Player.GlobalPosition = p;
        if (_ferryHull is { } hull)
        {
            hull.GlobalPosition = p + new Vector2(0, 1f);
            hull.Rotation = MathF.Sin(_activityT * 2.3f) * 0.03f;
            if (_ferryBoat is { } boat && _ferryArt is { } art)
            {
                // remos em 4 quadros enquanto atravessa (6 fps), mais devagar no começo e no fim do trajeto. A fase é a integral
                // do ritmo (contador que só soma): tempo × ritmo variável faria os quadros voltarem depois de t ≈ 0,7
                var count = art.Count(BoatRowing);
                _ferryPhase = FrameClock.Advance(_ferryPhase, (float)delta, art.Fps(BoatRowing), FrameClock.RowPace(t), count);
                var frame = FrameClock.Loop(_ferryPhase, count);
                if (frame != FerryFrame)
                {
                    FerryFrame = frame;
                    art.Apply(boat, BoatRowing, frame);
                }
            }
        }

        if (t >= 1f && _activityEnd < 0f)
        {
            _activityEnd = _activityT + 0.05f;
        }
    }

    private bool HandleActivityInput(InputEvent @event)
    {
        if (_activity == Activity.None)
        {
            return false;
        }

        if (_activity == Activity.Fishing && _fishing is { } game && _activityEnd < 0f && @event.IsActionPressed(InputActions.Interact))
        {
            FinishFishing(game.Pull());
            return true;
        }

        if (_activity == Activity.Sitting && _activityT > 0.3f && (@event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed(InputActions.Attack)))
        {
            EndActivity();
            return true;
        }

        return @event.IsActionPressed(InputActions.Interact) || @event.IsActionPressed(InputActions.Attack);
    }

    private void FinishFishing(FishingState result)
    {
        var at = _world.Player.GlobalPosition - new Vector2(0, WorldScale.CharacterHeight * 0.6f);
        if (result == FishingState.Caught)
        {
            var material = WorldScale.Rules?.Fishing.Material ?? "peixe";
            WorldLoot.Grant(LootDrop.Material(material, 1), at, Popups);
            FishingBar.SetCaption("Fisgou!");
        }
        else
        {
            Popups.Show(at, "O peixe escapou…", null, WorldUiStyle.Ink with { A = 0.85f });
            FishingBar.SetCaption("Escapou.");
            AudioManager.Instance?.PlaySfx("ui", at, pitchJitter: 0f);
        }

        _activityEnd = _activityT + 0.7f;
    }

    // --- visuais pequenos ---

    /// <summary>Ponto de água (formas <c>water</c> do layout) mais perto do jogador, um pouco para dentro, até 60 u.</summary>
    private Vector2? NearestWater(Vector2 from)
    {
        Vector2? best = null;
        var bestD = 60f * 60f;
        foreach (var w in _world.Layout.Water)
        {
            var b = w.Bounds;
            var p = new Vector2(Math.Clamp(from.X, b.X + 4f, MathF.Max(b.X + 4f, b.Right - 4f)), Math.Clamp(from.Y, b.Y + 4f, MathF.Max(b.Y + 4f, b.Bottom - 4f)));
            if (!PolygonMath.Contains(w.Polygon, p.X, p.Y))
            {
                continue;
            }

            var d = p.DistanceSquaredTo(from);
            if (d < bestD)
            {
                bestD = d;
                best = p + ((p - from).Normalized() * 8f);
            }
        }

        return best;
    }

    /// <summary>Linha da vara até a boia; a boia afunda e treme quando o peixe morde. Sem alocação (3 pontos fixos).</summary>
    private void UpdateFishingLine(bool biting)
    {
        if (_fishingLine is not { } line)
        {
            return;
        }

        var player = _world.Player;
        var hand = player.GlobalPosition + new Vector2(player.Facing.X >= 0 ? 7f : -7f, -WorldScale.CharacterHeight * 0.55f);
        if (_fishingRod is { } rod)
        {
            // a linha sai da ponta da vara desenhada; a vara treme quando o peixe morde
            rod.Rotation = biting ? MathF.Sin(_activityT * 34f) * 0.05f : MathF.Sin(_activityT * 1.7f) * 0.015f;
            hand = rod.GlobalTransform * _rodTip;
        }

        var bob = biting ? new Vector2(MathF.Sin(_activityT * 40f) * 0.8f, 1.5f) : new Vector2(0, MathF.Sin(_activityT * 3f) * 0.6f);
        var end = _bobber + bob;
        var mid = hand.Lerp(end, 0.5f) + new Vector2(0, biting ? 2f : 6f); // a linha faz barriga
        line.SetPointPosition(0, hand);
        line.SetPointPosition(1, mid);
        line.SetPointPosition(2, end);
    }

    private static CpuParticles2D CreateSparkle(Vector2 at)
    {
        var ramp = new Gradient
        {
            Offsets = [0f, 0.5f, 1f],
            Colors = [new Color(1f, 0.95f, 0.7f, 0f), new Color(1f, 0.95f, 0.7f, 0.85f), new Color(1f, 0.95f, 0.7f, 0f)],
        };
        return new CpuParticles2D
        {
            Name = "Sparkle",
            Position = at,
            Amount = 3,
            Lifetime = 1.6,
            Preprocess = 1.6,
            LocalCoords = false,
            Direction = new Vector2(0, -1),
            Spread = 60f,
            Gravity = new Vector2(0, -3),
            InitialVelocityMin = 1f,
            InitialVelocityMax = 4f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.3f,
            ColorRamp = ramp,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 5f,
            ZIndex = 4,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }

    /// <summary>
    /// Barco desenhado da travessia (DECISIONS C2-D5): balsa para os rótulos "balsa", barco a remo para os outros, por trás
    /// do caçador (z −1 dentro do y-sort) com o pé dele no meio do casco, reflexo na água e os remos em 4 quadros.
    /// </summary>
    private Node2D? CreateBoat(DecorSpot spot, bool flip)
    {
        var kind = (spot.Def.Label ?? string.Empty).Contains("balsa", StringComparison.OrdinalIgnoreCase) ? "raft" : "rowboat";
        if (PropArt.Boat(kind) is not { } path || !ArtFrames.TryLoad(path, out var art))
        {
            return null;
        }

        var root = new Node2D { Name = "FerryBoat", ZIndex = -1 };
        // altura do casco opaco (o PNG tem margem transparente em cima): o pé do caçador fica no meio do casco
        var used = art.Texture(BoatRowing, 0)?.GetImage()?.GetUsedRect() ?? new Rect2I(0, 0, 1, 1);
        var height = (art.Anchor(BoatRowing).Y - used.Position.Y) * art.UnitsPerPixel(BoatRowing);
        var drop = height * 0.5f;
        if (art.Has(BoatReflection))
        {
            var reflection = new Sprite2D { Name = "Reflection", Modulate = new Color(1f, 1f, 1f, 0.28f), Position = new Vector2(0, drop + 2f) };
            art.Apply(reflection, BoatReflection, 0, 0.9f);
            reflection.Scale = new Vector2(flip ? -reflection.Scale.X : reflection.Scale.X, reflection.Scale.Y * 0.5f);
            root.AddChild(reflection);
        }

        var boat = new Sprite2D { Name = "Boat", Position = new Vector2(0, drop) };
        if (flip)
        {
            boat.Scale = new Vector2(-1f, 1f);
        }

        art.Apply(boat, BoatRowing, 0);
        root.AddChild(boat);
        _ferryBoat = boat;
        _ferryArt = art;
        FerryBoat = kind;
        FerryFrame = 0;
        return root;
    }

    /// <summary>
    /// Vara de pesca desenhada na mão (DECISIONS C2-D5). O PNG traz a linha e a boia penduradas; mostramos só a vara (recorte
    /// até a ponta) porque a linha de verdade vai até a boia na água e afunda quando o peixe morde.
    /// </summary>
    private void CreateRod(bool left)
    {
        if (!EnsureRodTextures())
        {
            return;
        }

        var player = _world.Player;
        var texture = left ? _rodLeft! : _rodRight!;
        var grip = left ? _rodGripLeft : _rodGripRight;
        _rodTip = (left ? _rodTipLeft : _rodTipRight) - grip;
        _fishingRod = new Sprite2D
        {
            Name = "FishingRod",
            Texture = texture,
            Centered = false,
            Offset = -grip,
            Scale = Vector2.One * _rodUnitsPerPixel,
            ZIndex = 1,
            Position = new Vector2(left ? -6f : 6f, -WorldScale.CharacterHeight * 0.42f),
        };
        player.AddChild(_fishingRod);
    }

    private static bool EnsureRodTextures()
    {
        if (_rodRight is not null)
        {
            return true;
        }

        if (PropArt.FishingRod() is not { } path || !ArtFrames.TryLoad(path, out var art))
        {
            return false;
        }

        _rodUnitsPerPixel = art.UnitsPerPixel("right");
        (_rodRight, _rodGripRight, _rodTipRight) = CutRod(art.Texture("right", 0), right: true);
        (_rodLeft, _rodGripLeft, _rodTipLeft) = CutRod(art.Texture("left", 0), right: false);
        return _rodRight is not null && _rodLeft is not null;
    }

    /// <summary>Recorta a vara (do cabo até a ponta, sem a linha pendurada); devolve o recorte, o cabo e a ponta em px do recorte.</summary>
    private static (AtlasTexture?, Vector2, Vector2) CutRod(Texture2D? texture, bool right)
    {
        if (texture?.GetImage() is not { } image)
        {
            return (null, Vector2.Zero, Vector2.Zero);
        }

        var used = image.GetUsedRect();
        // ponta = pixel opaco mais alto; cabo = pixel opaco mais baixo (lado do cabo)
        var tip = new Vector2I(used.Position.X, used.Position.Y);
        for (var x = used.Position.X; x < used.End.X; x++)
        {
            if (image.GetPixel(x, used.Position.Y).A > 0.5f)
            {
                tip = new Vector2I(x, used.Position.Y);
                break;
            }
        }

        var bottom = used.End.Y - 1;
        var gripX = used.Position.X;
        for (var x = used.Position.X; x < used.End.X; x++)
        {
            if (image.GetPixel(x, bottom).A > 0.5f)
            {
                gripX = x;
                break;
            }
        }

        // a linha fica do lado de dentro da ponta: o recorte vai do cabo até 2 px depois da ponta
        var x0 = right ? used.Position.X : Math.Max(used.Position.X, tip.X - 2);
        var x1 = right ? Math.Min(used.End.X, tip.X + 3) : used.End.X;
        var region = new Rect2(x0, used.Position.Y, x1 - x0, used.Size.Y);
        var cut = new AtlasTexture { Atlas = texture, Region = region };
        var origin = region.Position;
        return (cut, new Vector2(gripX + 2, bottom) - origin, new Vector2(tip.X, tip.Y) - origin);
    }

    /// <summary>Barco de madeira sob o jogador na travessia (a balsa pintada no mapa fica no cais): casco com proa e popa, borda clara e esteira.</summary>
    private static Node2D CreateHull()
    {
        var s = WorldScale.Character;
        Vector2 P(float x, float y) => new(x * s, y * s);
        var root = new Node2D { Name = "FerryHull" };
        root.AddChild(new Polygon2D { Color = new Color(0.8f, 0.9f, 1f, 0.22f), Polygon = [P(-22, 1), P(22, 1), P(18, 5), P(-18, 5)] });
        root.AddChild(new Polygon2D { Color = new Color("#3e2716"), Polygon = [P(-19, -6), P(19, -6), P(15, 1), P(9, 3), P(-9, 3), P(-15, 1)] });
        root.AddChild(new Polygon2D { Color = new Color("#6b4426"), Polygon = [P(-16, -6), P(16, -6), P(13, -2), P(-13, -2)] });
        root.AddChild(new Polygon2D { Color = new Color("#a8743e"), Polygon = [P(-19, -7), P(19, -7), P(19, -5.8f), P(-19, -5.8f)] });
        return root;
    }

    private static float DefaultPromptHeight(string kind) => kind switch
    {
        "lamp" => 44f,
        "flower" => 18f,
        "boat" or "fishing" => WorldScale.CharacterHeight + 8f,
        _ => 28f,
    };

    private static string DefaultLabel(string kind) => kind switch
    {
        "lamp" => "Apagar lampião",
        "flower" => "Colher flor",
        "boat" => "Atravessar de balsa",
        "fishing" => "Pescar",
        "sign" => "Ler placa",
        "bench" => "Sentar",
        "fountain" => "Olhar a fonte",
        "stall" => "Ver a barraca",
        _ => "Olhar",
    };

    private static string DefaultText(string kind) => kind switch
    {
        "sign" => "As letras estão gastas demais para ler.",
        "fountain" => "A água corre fria e limpa.",
        "stall" => "A barraca está fechada por enquanto.",
        _ => "Nada de especial.",
    };
}

/// <summary>Uma decoração do layout montada no mapa atual.</summary>
public sealed class DecorSpot(LayoutDecor def, int index, string key)
{
    public LayoutDecor Def { get; } = def;

    /// <summary>Posição na lista <c>decor</c> do layout (ou depois dela, para objetos do catálogo).</summary>
    public int Index { get; } = index;

    public string Key { get; } = key;

    public WorldInteractable Interactable { get; set; } = null!;

    /// <summary>Halo controlado (lampião), ou −1.</summary>
    public int Light { get; set; } = -1;

    public CpuParticles2D? Sparkle { get; set; }

    /// <summary>Objeto do catálogo com esta interação (lampião, flor, alavanca, porta), se houver.</summary>
    public CatalogObject? Object { get; set; }
}
