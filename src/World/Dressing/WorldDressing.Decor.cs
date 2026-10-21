using System;
using Godot;
using Vespera.Autoload;
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
    private uint _savedMask;
    private Node2D? _ferryHull;
    private Line2D? _fishingLine;
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

    private void AddDecor(MapDef map, LayoutDecor def, int index, string key, float promptHeight = 0f)
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
        _savedMask = player.CollisionMask;
        player.CollisionMask = 0; // atravessa a água bloqueada
        _ferryHull = CreateHull();
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
        }

        if (IsInstanceValid(player))
        {
            player.MovementLocked = false;
            player.Animator.Sitting = false;
        }

        _fishingLine?.QueueFree();
        _fishingLine = null;
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
}
