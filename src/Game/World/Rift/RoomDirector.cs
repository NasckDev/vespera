using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Combat;
using Vespera.Game.Components;
using Vespera.Core.Common;
using Vespera.Game.Hunter;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Diretor de uma sala de Brecha (camada 3): sela a saída, solta as ondas (cada monstro nasce de uma fenda no chão),
/// abre a saída quando a sala fica limpa, cuida do baú, do Guardião e da fenda de saída.
/// Avisa a fase por <see cref="ExitRequested"/> quando o caçador sai.
/// </summary>
public partial class RoomDirector : Node
{
    private const float FirstWaveDelay = 0.9f;
    private const float WaveGap = 0.8f;
    private const float SpawnWarning = 0.55f;
    private const float BarrierHeight = 170f;
    private const float PortalHeight = 210f;

    /// <summary>Altura humana da Praça, onde o catálogo de objetos foi medido.</summary>
    private const float PracaHuman = 72f;
    private const string RestProp = "lanterna_chao";

    private readonly LevelWorld _world;
    private readonly RiftRoomData _data;
    private readonly List<Enemy> _wave = [];
    private Barrier? _barrier;
    private int _nextWave;
    private float _timer = FirstWaveDelay;
    private bool _exitSent;
    private int _pending;
    private bool _waveOut;
    private double _bossSeconds;
    private string _eliteModifier = string.Empty;
    private static readonly string[] EliteModifiers = ["blindado", "veloz", "faminto", "gemeo"];
    private Interactable? _rest;

    public RoomDirector(LevelWorld world, RiftRoomData data)
    {
        _world = world;
        _data = data;
        Name = "Director";
    }

    public bool Cleared { get; private set; }

    /// <summary>A Lodo-Mãe (Ato I), se for ela o Guardião desta sala.</summary>
    public LodoMae? Boss => Guardian as LodoMae;

    /// <summary>O Guardião desta sala (qualquer um: Lodo-Mãe, Macieira Oca).</summary>
    public Enemy? Guardian { get; private set; }

    public Chest? Chest { get; private set; }

    public BreachPortal? ExitPortal { get; private set; }

    public string Title => _data.Title;

    /// <summary>Tipo da sala na caçada (combat, elite, treasure, rest, guardian; vazio = sala fixa).</summary>
    public string Kind => _data.Kind;

    /// <summary>Ondas desta sala e quantas já saíram (HUD e bots).</summary>
    public int WaveCount => _data.Waves.Count;

    public int WavesReleased => _nextWave;

    /// <summary>A lanterna de descanso ainda não foi usada.</summary>
    public bool RestAvailable => _rest is not null;

    /// <summary>O caçador saiu da sala (passagem ou fenda).</summary>
    public event Action? ExitRequested;

    public override void _Ready()
    {
        var a = _world.Data.ActorScale;
        if (_data.Exit is { } exit)
        {
            var r = exit.Barrier;
            _barrier = new Barrier(new Rect2(r[0], r[1], r[2], r[3]), BarrierHeight * a) { Name = "Barrier" };
            _world.Entities.AddChild(_barrier);
            _world.AddSolid(_barrier.Rect);
        }

        foreach (var r in _data.Sealed)
        {
            var seal = new Barrier(new Rect2(r[0], r[1], r[2], r[3]), BarrierHeight * a * 0.8f) { Name = "Sealed" };
            _world.Entities.AddChild(seal);
            _world.AddSolid(seal.Rect);
        }

        if (_data.Chest is { } chest)
        {
            Chest = new Chest(_world, new Vector2(chest[0], chest[1])) { Name = "Chest" };
            _world.Entities.AddChild(Chest);
            Chest.Opened += _ =>
            {
                HuntService.TreasureOpened();
                OpenExitPortal();
            };
        }

        if (_data.Boss is { } boss)
        {
            Guardian = EnemyFactory.Guardian(boss.Type, _world);
            Guardian.Position = new Vector2(boss.At[0], boss.At[1]);
            Guardian.Died += _ => OnBossDefeated();
            _world.AddEnemy(Guardian);
        }

        PlaceBreakables();
        PlaceRest();
        HuntService.RoomStarted();
        if (_data.Waves.Count == 0 && Guardian is null)
        {
            Clear(quiet: true);
        }
    }

    /// <summary>Barris e caixotes da sala (quebram com um golpe e soltam Lumes, às vezes um Fragmento).</summary>
    private void PlaceBreakables()
    {
        // Os objetos do catálogo foram medidos na Praça (humano de 72 px); aqui crescem na mesma razão do caçador.
        var grow = _world.Data.HumanHeight / PracaHuman;
        for (var i = 0; i < _data.Breakables.Count; i++)
        {
            var p = _data.Breakables[i];
            var type = i % 2 == 0 ? "barril" : "caixote";
            if (_world.Catalog.ContainsKey(type))
            {
                _world.AddBreakable(type, _world.ClampToFloor(new Vector2(p[0], p[1])), string.Empty, grow);
            }
        }
    }

    /// <summary>Sala de descanso: uma lanterna acesa no chão que cura uma vez.</summary>
    private void PlaceRest()
    {
        if (_data.Rest is not { Length: 2 } at || !_world.Catalog.ContainsKey(RestProp))
        {
            return;
        }

        var feet = _world.ClampToFloor(new Vector2(at[0], at[1]));
        var lamp = _world.AddProp(RestProp, feet);
        lamp.Scale = Vector2.One * (_world.Data.HumanHeight / PracaHuman);
        var human = _world.Data.HumanHeight;
        _rest = new Interactable("rest", "Descansar na lanterna", 0.9f * human)
        {
            Name = "Rest",
            Text = "A chama é quente e calma. Você respira fundo.",
            Position = feet,
            PromptAnchor = feet - new Vector2(0, 1.1f * human),
        };
        _world.Entities.AddChild(_rest);
        _world.Feel.Toast("Sala de descanso: chegue perto da lanterna e interaja para recuperar vida.");
    }

    /// <summary>Usa a lanterna de descanso: cura parte da vida máxima (uma vez por sala).</summary>
    public bool UseRest()
    {
        if (_rest is null || _world.Player is not { } player)
        {
            return false;
        }

        var combat = player.Combat;
        combat.Heal((int)System.Math.Round(combat.MaxHp * HuntService.Config.RestHealPct));
        _rest.QueueFree();
        _rest = null;
        _world.Feel.Toast("A lanterna aqueceu você. Siga em frente.");
        return true;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (!Cleared && Guardian is null)
        {
            TickWaves(dt);
        }

        if (Boss is { Engaged: true, Dead: false })
        {
            _bossSeconds += dt;
        }

        if (Cleared && !_exitSent && _data.Exit is { } exit && _world.Player is { } player)
        {
            var t = exit.Trigger;
            if (new Rect2(t[0], t[1], t[2], t[3]).HasPoint(player.GlobalPosition))
            {
                RequestExit();
            }
        }
    }

    public void RequestExit()
    {
        if (_exitSent)
        {
            return;
        }

        _exitSent = true;
        ExitRequested?.Invoke();
    }

    /// <summary>Monstros vivos desta sala (para o bot de combate).</summary>
    public IEnumerable<Enemy> Alive => _world.Enemies.Where(e => IsInstanceValid(e) && !e.Dead);

    private void TickWaves(float dt)
    {
        _wave.RemoveAll(e => !IsInstanceValid(e) || e.Dead);
        if (_wave.Count > 0 || _pending > 0)
        {
            return;
        }

        if (_waveOut)
        {
            _waveOut = false;
            HuntService.WaveCleared();
        }

        if (_nextWave >= _data.Waves.Count)
        {
            Clear(quiet: false);
            return;
        }

        _timer -= dt;
        if (_timer > 0f)
        {
            return;
        }

        var wave = _data.Waves[_nextWave++];
        _waveOut = true;
        for (var i = 0; i < wave.Count; i++)
        {
            SpawnWithWarning(wave[i], i * 0.18f);
        }

        _timer = WaveGap;
        if (wave.Any(w => w.Elite))
        {
            _eliteModifier = EliteModifiers[RiftSession.Random.RangeInt(0, EliteModifiers.Length - 1)];
            _world.Feel.Toast($"Cuidado: ELITE {Enemy.ModifierName(_eliteModifier)}. Derrubar dá saque melhor.");
        }
        else if (_nextWave > 1)
        {
            _world.Feel.Toast($"Mais deles saem das fendas! (onda {_nextWave}/{_data.Waves.Count})");
        }
    }

    /// <summary>Uma fenda magenta se abre no chão e o monstro sai dela depois do aviso.</summary>
    private void SpawnWithWarning(SpawnData spawn, float delay)
    {
        _pending++;
        var at = _world.ClampToFloor(new Vector2(spawn.At[0], spawn.At[1]));
        var a = _world.Data.ActorScale;
        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this))
            {
                return;
            }

            var crack = TelegraphMarker.Circle(at, 36f * a, SpawnWarning);
            crack.Modulate = new Color(1.4f, 0.5f, 1.1f);
            _world.AddGroundFx(crack);
            CombatSfx.Play(_world, CombatSound.Portal, at, -10f);
            var appear = GetTree().CreateTimer(SpawnWarning);
            appear.Timeout += () =>
            {
                if (!IsInstanceValid(this))
                {
                    return;
                }

                crack.Release();
                _pending--;

                var enemy = EnemyFactory.Create(spawn.Type, _world);
                if (spawn.Elite)
                {
                    enemy.MakeElite(HuntService.Config.Elite);
                    enemy.ApplyModifier(_eliteModifier);
                    if (_eliteModifier == "gemeo")
                    {
                        SpawnTwin(spawn, at);
                    }
                }

                enemy.Position = at;
                _world.AddEnemy(enemy);
                _wave.Add(enemy);
                _world.Feel.Spark(at - new Vector2(0, 10f * a), new Color("e64a8e"), 14, 220f, Vector2.Up, 60f);
            };
        };
    }

    /// <summary>Elite Gêmeo: um segundo elite igual nasce ao lado.</summary>
    private void SpawnTwin(SpawnData spawn, Vector2 at)
    {
        Enemy twin = spawn.Type == "espectro" ? new Espectro(_world) : new Lodo(_world);
        twin.MakeElite(HuntService.Config.Elite);
        twin.ApplyModifier("gemeo");
        twin.Position = _world.ClampToFloor(at + new Vector2(0.8f * _world.Data.HumanHeight, 0));
        _world.AddEnemy(twin);
        _wave.Add(twin);
    }

    private void Clear(bool quiet)
    {
        if (Cleared)
        {
            return;
        }

        Cleared = true;
        HuntService.RoomCleared(combat: _data.Waves.Count > 0);
        _barrier?.Open();
        if (_barrier is not null)
        {
            _world.RemoveSolid(_barrier.Rect);
        }

        if (!quiet)
        {
            _world.Feel.Toast("Sala limpa. O selo da passagem se desfez.");
            HealOnClear();
            OfferBlessing();
        }

        if (_data.ExitPortal is not null && Chest is null && Guardian is null)
        {
            OpenExitPortal();
        }
    }

    /// <summary>Bênção da Sopa "Prato Cheio": cura ao limpar a sala.</summary>
    private void HealOnClear()
    {
        var pct = HuntService.Bless.HealOnClearPct;
        if (pct > 0 && _world.Player is { } player)
        {
            player.Combat.Heal((int)System.Math.Round(player.Combat.MaxHp * pct / 100));
        }
    }

    /// <summary>A lanterna oferece 3 bênçãos (ROTEIRO-V2 §5.1); escolher aplica na hora (vida máxima inclusive).</summary>
    private void OfferBlessing()
    {
        if (!RiftSession.InRift || !Hunter.Progression.IsActive || !IsInsideTree())
        {
            return;
        }

        var offer = HuntService.OfferBlessings();
        Hunt.BlessingScreen.Show(offer, id =>
        {
            var player = _world.Player;
            var before = player.Combat.MaxHp;
            HuntService.AddBlessing(id);
            player.Combat.RefreshStats(fullHeal: false);
            var gained = player.Combat.MaxHp - before;
            if (gained > 0)
            {
                player.Combat.Heal(gained);
            }

            var name = HuntService.BlessingCatalog.Find(id)?.Name ?? id;
            _world.Feel.Toast($"Bênção: {name}");
        });
    }

    private void OnBossDefeated()
    {
        HuntService.GuardianFight(_bossSeconds, RiftSession.Brecha.Boss.Cores, Boss?.CoresBroken ?? 0);
        RiftLoot.GuardianDefeated(_world, Guardian?.GlobalPosition ?? _world.Player.GlobalPosition);
        Cleared = true;
        var timer = GetTree().CreateTimer(1.4, processAlways: false);
        timer.Timeout += OpenExitPortal;
    }

    private void OpenExitPortal()
    {
        if (ExitPortal is not null || _data.ExitPortal is not { } at || !IsInstanceValid(this))
        {
            return;
        }

        var a = _world.Data.ActorScale;
        var last = RiftSession.IsLastRoom;
        ExitPortal = new BreachPortal(PortalHeight * a, last ? "saida" : "descer", last ? "Voltar para a Praça" : "Descer pela fenda", 46f * a)
        {
            Name = "ExitPortal",
            Position = new Vector2(at[0], at[1]),
        };
        _world.Entities.AddChild(ExitPortal);
        _world.AddSolid(ExitPortal.Footprint);
        CombatSfx.Play(_world, CombatSound.Portal, ExitPortal.GlobalPosition);
    }
}
