using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Vespera.Game.Actors.Enemies;
using Vespera.Game.Combat;

namespace Vespera.Game.World.Rift;

/// <summary>
/// Diretor de uma sala de Brecha (camada 3): sela a saída, solta as ondas (cada monstro nasce de uma fenda no chão),
/// abre a saída quando a sala fica limpa, cuida do baú, do Guardião, da Lanterna perdida e da fenda de saída.
/// Avisa a fase por <see cref="ExitRequested"/> quando o caçador sai.
/// </summary>
public partial class RoomDirector : Node
{
    private const float FirstWaveDelay = 0.9f;
    private const float WaveGap = 0.8f;
    private const float SpawnWarning = 0.55f;
    private const float BarrierHeight = 170f;
    private const float PortalHeight = 210f;

    private readonly LevelWorld _world;
    private readonly RiftRoomData _data;
    private readonly List<Enemy> _wave = [];
    private Barrier? _barrier;
    private int _nextWave;
    private float _timer = FirstWaveDelay;
    private bool _exitSent;
    private int _pending;

    public RoomDirector(LevelWorld world, RiftRoomData data)
    {
        _world = world;
        _data = data;
        Name = "Director";
    }

    public bool Cleared { get; private set; }

    public LodoMae? Boss { get; private set; }

    public Chest? Chest { get; private set; }

    public BreachPortal? ExitPortal { get; private set; }

    public string Title => _data.Title;

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

        if (_data.Chest is { } chest)
        {
            Chest = new Chest(_world, new Vector2(chest[0], chest[1])) { Name = "Chest" };
            _world.Entities.AddChild(Chest);
            Chest.Opened += _ => OpenExitPortal();
        }

        if (_data.Boss is { } boss)
        {
            Boss = new LodoMae(_world) { Position = new Vector2(boss.At[0], boss.At[1]) };
            Boss.Died += _ => OnBossDefeated();
            _world.AddEnemy(Boss);
        }

        if (RiftSession.Rules.IsLanternHere(RiftSession.Save, RiftSession.CurrentRoom) && RiftSession.Save.LostLantern is { } lost)
        {
            _world.Entities.AddChild(new LanternPickup(_world, _world.ClampToFloor(new Vector2((float)lost.X, (float)lost.Y))));
        }

        if (_data.Waves.Count == 0 && Boss is null)
        {
            Clear(quiet: true);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (!Cleared && Boss is null)
        {
            TickWaves(dt);
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
        for (var i = 0; i < wave.Count; i++)
        {
            SpawnWithWarning(wave[i], i * 0.18f);
        }

        _timer = WaveGap;
        if (_nextWave > 1)
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

                Enemy enemy = spawn.Type == "espectro" ? new Espectro(_world) : new Lodo(_world) { StartDelay = (float)GD.RandRange(0.0, 0.6) };
                enemy.Position = at;
                _world.AddEnemy(enemy);
                _wave.Add(enemy);
                _world.Feel.Spark(at - new Vector2(0, 10f * a), new Color("e64a8e"), 14, 220f, Vector2.Up, 60f);
            };
        };
    }

    private void Clear(bool quiet)
    {
        if (Cleared)
        {
            return;
        }

        Cleared = true;
        _barrier?.Open();
        if (_barrier is not null)
        {
            _world.RemoveSolid(_barrier.Rect);
        }

        if (!quiet)
        {
            _world.Feel.Toast("Sala limpa. O selo da passagem se desfez.");
        }

        if (_data.ExitPortal is not null && Chest is null && Boss is null)
        {
            OpenExitPortal();
        }
    }

    private void OnBossDefeated()
    {
        RiftSession.Rules.AddGuardianLoot(RiftSession.Bag);
        _world.Feel.Toast("A Lodo-Mãe se desfaz. +2 Gel de Lodo. A fenda de volta se abriu.");
        Cleared = true;
        var timer = GetTree().CreateTimer(1.4);
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
