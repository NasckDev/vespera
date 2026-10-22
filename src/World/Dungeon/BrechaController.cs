using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Combat;
using Vespera.Core.Combat;
using Vespera.Core.Combat.Ai;
using Vespera.Core.Common;
using Vespera.Core.Data;
using Vespera.Core.Save;
using Vespera.Core.World;
using Vespera.Items;

namespace Vespera.World.Dungeon;

/// <summary>
/// Fluxo da Brecha (spec 01 §1.9; <c>enterBrecha</c>/<c>setupDungeon</c>/<c>updRun</c>/<c>bossDefeated</c>/<c>finishRun</c>/<c>fleeDialog</c>,
/// <c>world.js:75-134</c>): guarda os portais abertos pela Cidade (<c>PortalOpened</c>), entra no pedido da Interface
/// (<c>BrechaStartRequested</c>), monta a sala (props, baús, Lanternas), roda as ondas do <see cref="BrechaRun"/> (Core),
/// libera a porta, leva ao Núcleo, chama o chefe, abre o portal de saída e devolve a bolsa ao sair ou fugir. Também
/// coordena os bonecos da Guilda, a morte/Lanterna (<see cref="LanternKeeper"/>) e a invasão (<see cref="InvasionController"/>).
/// </summary>
public sealed class BrechaController
{
    private static readonly MapPoint DefaultBoss = new(250, 500); // world.js:113
    private static readonly MapPoint DefaultExit = new(250, 228); // world.js:120

    private readonly CombatDirector _combat;
    private readonly Dictionary<int, OpenBrecha> _open = [];
    private int _nextBrechaId = 1;
    private BrechaRun? _run;
    private bool _themeRoomReady;
    private (string Title, string Sub)? _arrivalToast;
    private Enemies.Enemy? _boss;

    public BrechaController(CombatDirector combat)
    {
        _combat = combat;
        Lantern = new LanternKeeper(combat, this);
        Invasion = new InvasionController(combat, this);
    }

    public LanternKeeper Lantern { get; }

    public InvasionController Invasion { get; }

    /// <summary>Incursão em curso (null fora de Brecha).</summary>
    public BrechaRun? Run => _run;

    /// <summary>Bolsa da Brecha em curso (null fora: itens vão direto para o save).</summary>
    public RunBag? Bag => _run?.Bag;

    /// <summary>Dentro de uma Brecha (mapa de dungeon com incursão ativa).</summary>
    public bool InBrecha => _run is not null && _combat.World.Map.Dungeon;

    public SeasonDef? SeasonDef => _run is null ? null : SeasonOf(_run.SeasonId);

    /// <summary>Segundos dentro da Brecha atual (timer da incursão), ou 0.</summary>
    public double RunElapsed => _run?.Elapsed ?? 0;

    /// <summary>Rank e estação para escalar monstros (<c>spawnMob</c>): Brecha, senão invasão, senão 0.</summary>
    public (int Rank, string? Season) ScalingContext =>
        _run is not null ? (_run.Rank, (string?)_run.SeasonId) : Invasion.Active ? (Invasion.Rank, (string?)null) : (0, (string?)null);

    /// <summary>Portais abertos conhecidos (spot → Brecha).</summary>
    public IReadOnlyDictionary<int, OpenBrecha> OpenBrechas => _open;

    private GameDatabase Db => _combat.Db;

    private World World => _combat.World;

    public void Connect(EventBus bus)
    {
        bus.PortalOpened += OnPortalOpened;
        bus.PortalClosed += OnPortalClosed;
        bus.BrechaStartRequested += OnBrechaStartRequested;
        bus.ChoiceMade += OnChoiceMade;
        bus.DungeonEscapeRequested += OnEscapeRequested;
        bus.DungeonDoorEntered += OnDoorEntered;
        bus.InvasionRequested += Invasion.OnInvasionRequested;
        bus.RespawnRequested += Lantern.OnRespawnRequested;
        bus.GameLoaded += Lantern.OnGameLoaded;
        bus.LanternRescueRequested += Lantern.ScheduleRescue; // resgate pago da Odete (Interface, api-combate)
        Lantern.OnGameLoaded();
    }

    public void Disconnect(EventBus bus)
    {
        bus.PortalOpened -= OnPortalOpened;
        bus.PortalClosed -= OnPortalClosed;
        bus.BrechaStartRequested -= OnBrechaStartRequested;
        bus.ChoiceMade -= OnChoiceMade;
        bus.DungeonEscapeRequested -= OnEscapeRequested;
        bus.DungeonDoorEntered -= OnDoorEntered;
        bus.InvasionRequested -= Invasion.OnInvasionRequested;
        bus.RespawnRequested -= Lantern.OnRespawnRequested;
        bus.GameLoaded -= Lantern.OnGameLoaded;
        bus.LanternRescueRequested -= Lantern.ScheduleRescue;
    }

    public void Step(float dt, float realDt)
    {
        Lantern.Step(realDt);
        Invasion.Step();
        if (_run is null || !World.Map.Dungeon || World.IsTransitioning || !_themeRoomReady)
        {
            return;
        }

        switch (_run.Update(dt, _combat.AliveMonsters, World.Map.BossHere is not null))
        {
            case BrechaRunEvent.SpawnWave:
                SpawnWave();
                break;
            case BrechaRunEvent.DoorOpened:
                World.DoorOpen = true;
                _combat.Toast("A passagem se abriu", "Siga até a porta marcada: o Núcleo da Brecha espera.");
                _combat.Sfx("open", _combat.PlayerFoot);
                break;
            case BrechaRunEvent.AltarAwakens:
                _combat.Toast("O altar desperta", "Algo enorme se levanta no centro da nave.");
                _combat.Sfx("portal", _combat.PlayerFoot);
                break;
            case BrechaRunEvent.SpawnBoss:
                SpawnBoss();
                break;
            case BrechaRunEvent.ExitPortal:
                SpawnExitPortal();
                break;
        }
    }

    // --- Portais (Cidade) ---

    private void OnPortalOpened(int spot, string themeId, int rank)
    {
        if (!Db.Themes.ContainsKey(themeId))
        {
            Log.Warning($"PortalOpened com tema desconhecido '{themeId}'.");
            return;
        }

        _open[spot] = new OpenBrecha(_nextBrechaId++, spot, themeId, rank, _combat.State.Season.Id);
    }

    private void OnPortalClosed(int spot)
    {
        if (_open.Remove(spot, out var brecha))
        {
            Lantern.ReturnIfHere(brecha.Id); // returnLostIfHere: a Brecha fechou com a sua Lanterna
        }
    }

    // --- Entrar ---

    private void OnBrechaStartRequested(int spot)
    {
        if (_run is not null)
        {
            return;
        }

        if (!_open.TryGetValue(spot, out var brecha))
        {
            Log.Warning($"BrechaStartRequested({spot}) sem PortalOpened para esse portal.");
            return;
        }

        var state = _combat.State;
        if (!state.Brechas.CanEnter(brecha.Rank, state.RankIndex))
        {
            Log.Warning($"Brecha Selo {brecha.Rank} acima do permitido para o Selo {state.RankIndex}.");
            return;
        }

        var theme = Db.Themes[brecha.ThemeId];
        var easy = state.Save.BrechasClosed == 0;
        var waves = state.Brechas.GenerateWaves(theme, brecha.Rank, easy, _combat.Rng);
        _run = new BrechaRun(brecha.Id, spot, theme, brecha.Rank, brecha.SeasonId, waves, easy);
        _themeRoomReady = false;
        state.CountQuest(Core.Rules.QuestCounters.Entered);
        _combat.Sfx("portal", _combat.PlayerFoot);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PortalEntered, theme.Id);
        var map = World.Catalog[theme.Map];
        _combat.PreloadTheme(theme); // sprites soltos carregam agora, não no meio da luta
        World.GoMap(theme.Map, map.Spawn.X, map.Spawn.Y);
    }

    /// <summary>Mapa novo carregado (o diretor já limpou tudo do anterior).</summary>
    public void OnMapChanged(string mapId)
    {
        _boss = null;
        var map = World.Map;
        if (mapId == "guild")
        {
            foreach (var d in map.Dummies)
            {
                _combat.AddEnemy(MonsterBody.CreateDummy(d.X, d.Y));
            }
        }

        if (_run is not null && map.Dungeon)
        {
            if (mapId == _run.Theme.Map)
            {
                SetupThemeRoom(map);
            }
            else if (mapId == World.CoreMapId)
            {
                SetupCore(map);
            }
        }

        if (mapId == World.CityMapId)
        {
            Invasion.OnForgeLoaded();
        }

        if (_arrivalToast is { } toast)
        {
            _arrivalToast = null;
            _combat.Toast(toast.Title, toast.Sub);
        }
    }

    /// <summary><c>setupDungeon</c> (sala do tema): 9–13 props, baús, Lanterna de outro caçador (45%) e a sua.</summary>
    private void SetupThemeRoom(MapDef map)
    {
        var run = _run!;
        var rng = _combat.Rng;
        var avoid = _combat.AvoidPoints;
        avoid.Clear();
        avoid.Add(new Vector3(map.Spawn.X, map.Spawn.Y, 60));
        if (map.Door is { } door)
        {
            avoid.Add(new Vector3(door.X + (door.Width / 2), door.Y + 20, 50));
        }

        if (map.BossHere is { } bh)
        {
            avoid.Add(new Vector3(bh.X, bh.Y, 90));
        }

        // Integração v0.1 (DECISIONS INT-D4): as frentes de mapa puseram quebráveis e baús à mão no layout da sala, que o
        // motor monta (WorldDressing). Com layout, a sala não ganha os props e baús sorteados da v2 por cima deles.
        var designed = World.Layout.Props.Count > 0 || World.Layout.Chests.Count > 0;
        var props = designed ? 0 : rng.RangeInt(9, 13);
        for (var i = 0; i < props; i++)
        {
            var at = _combat.RandPoint(0, 10, useAvoid: true);
            var kind = rng.Pick(run.Theme.Props);
            _combat.AddProp(DungeonProp.Create(kind, run.Theme.Id, at));
            avoid.Add(new Vector3(at.X, at.Y, 22));
        }

        var chests = designed ? 0 : _combat.Loot.ChestCount(SeasonOf(run.SeasonId), rng);
        for (var i = 0; i < chests; i++)
        {
            var at = _combat.RandPoint(0, 10, useAvoid: true);
            var purple = _combat.Loot.RollPurpleChest(rng);
            _combat.AddProp(DungeonProp.Create("chest", run.Theme.Id, at, purple));
            avoid.Add(new Vector3(at.X, at.Y, 30));
        }

        if (_combat.State.LanternRules.RollFoundLantern(rng))
        {
            var at = _combat.RandPoint(0, 8, useAvoid: true);
            var who = rng.Pick(Db.Social.Hunters).Name;
            _combat.AddPickup(Pickup.Create(PickupKind.Lantern, who, 1, "lantern", PickupMotion.Still(at.X, at.Y), $"Pegar a Lanterna de {who}"));
        }

        PlaceOwnLantern(BrechaRooms.Theme);
        if (!_themeRoomReady)
        {
            _themeRoomReady = true;
            var season = SeasonOf(run.SeasonId);
            var title = Core.Rules.BrechaRules.Title(run.Theme, run.SeasonId);
            _combat.Toast(title, $"{run.Theme.Bairro} · {season.Name} · {season.ModText}");
            EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStarted, run.Theme.Id, run.Rank);
        }
    }

    /// <summary>Câmara do Núcleo: cristais decorativos e a Lanterna, se ficou aqui.</summary>
    private void SetupCore(MapDef map)
    {
        var rng = _combat.Rng;
        var crystals = World.Layout.Props.Count > 0 ? [] : map.CrystalProps; // INT-D4: o layout do Núcleo já tem os props
        foreach (var c in crystals)
        {
            var at = new Vector2(c.X + (float)rng.Range(-15, 15), c.Y + 30);
            _combat.AddProp(DungeonProp.Create("crystal", _run!.Theme.Id, at, spriteKey: rng.NextDouble() < 0.5 ? "pr_crys1" : "pr_crys2"));
        }

        PlaceOwnLantern(BrechaRooms.Core);
    }

    private void PlaceOwnLantern(string room)
    {
        var save = _combat.State.Save;
        if (_run is not null && LanternPlacement.IsHere(save, _run.BrechaId, _run.Theme.Id, room) && save.LostLantern is { } lost)
        {
            var at = World.CorrectToWalkable(new Vector2((float)lost.X, (float)lost.Y));
            _combat.AddPickup(Pickup.Create(PickupKind.MyLantern, string.Empty, 1, "lantern", PickupMotion.Still(at.X, at.Y), "Recuperar sua Lanterna"));
        }
    }

    // --- Ondas, porta, chefe ---

    /// <summary>Onda: cada monstro nasce a ≥ 130 u do jogador, com telegraph de 0,8 s + 0,15 s por posição.</summary>
    private void SpawnWave()
    {
        var run = _run!;
        var wave = run.CurrentWave;
        var p = _combat.PlayerFoot;
        for (var i = 0; i < wave.Count; i++)
        {
            var at = _combat.RandPoint(130, 9, MathF.Max(120, p.Y - 300), MathF.Min(1000, p.Y + 120));
            _combat.SpawnTelegraphed(wave[i], at, 0.8f + (i * 0.15f), chase: true);
        }

        if (run.Wave > 1)
        {
            _combat.Note($"Onda {run.Wave} de {run.Waves.Count}", "i_alert");
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.WaveStarted, run.Wave, run.Waves.Count);
    }

    private void OnDoorEntered(string mapId)
    {
        if (_run is not null && _run.Room == BrechaRooms.Theme && _run.Cleared)
        {
            _run.EnterCore();
        }
    }

    /// <summary>Chefe no <c>bossHere</c> do mapa (ou 250, 500): já perseguindo, 1,2 s nascendo, face 1.</summary>
    private void SpawnBoss()
    {
        var run = _run!;
        var map = World.Map;
        var p = map.BossHere ?? map.BossDefault ?? DefaultBoss;
        var def = Db.Monsters[run.Theme.Boss];
        var enemy = _combat.SpawnMonsterNow(def.Id, new Vector2(p.X, p.Y), chase: true, invasion: false);
        enemy.Body.SpawnT = MonsterBody.BossSpawnSeconds;
        enemy.Body.Face = 1;
        _boss = enemy;
        World.Camera.LookAtTarget = enemy;
        EventBus.Instance.EmitSignal(EventBus.SignalName.BossSpawned, def.Name, Core.Common.JsMath.Round(enemy.Body.MaxHp));
        _combat.Shake(8);
        _combat.Sfx("portal", enemy.Position);
        _combat.Glow.Burst(enemy.Position - new Vector2(0, 20), 60, CombatPalette.Spawn, new BurstOptions { V1 = 180 });
    }

    /// <summary>Morte de um monstro (o diretor já deu XP e drops).</summary>
    public void OnMonsterKilled(MonsterBody m)
    {
        if (_run is not null)
        {
            _run.Kills++;
            if (m.IsBoss)
            {
                OnBossDefeated(m);
            }
        }
    }

    public void OnPartBroken()
    {
        if (_run is not null)
        {
            _run.PartsBroken++;
        }
    }

    /// <summary><c>bossDefeated</c>: Brecha fechada, bônus de <c>40 + 25·rk</c> Lumes na bolsa, +60 XP, portal em 1,4 s.</summary>
    private void OnBossDefeated(MonsterBody boss)
    {
        var run = _run!;
        if (run.BossDefeated)
        {
            return;
        }

        var state = _combat.State;
        run.DefeatBoss();
        state.RecordBossDefeated(run.Theme.Id);
        run.Bag.Lumes += state.Brechas.BossBonusLumes(run.Rank);
        state.GainXp(state.Brechas.BossXp);
        World.Camera.LookAtTarget = null;
        _boss = null;
        _open.Remove(run.PortalSpot);
        Lantern.ReturnIfHere(run.BrechaId);
        EventBus.Instance.EmitSignal(EventBus.SignalName.BossDefeated, boss.Def.Id);
    }

    private void SpawnExitPortal()
    {
        var run = _run!;
        var map = World.Map;
        var at = map.ExitAt ?? map.ExitDefault ?? DefaultExit;
        var portal = ExitPortal.Create(new Vector2(at.X, at.Y));
        portal.Interaction.Callback = FinishRun;
        World.Entities.AddChild(portal);
        _combat.Toast("Brecha fechada", $"Lumeira respira mais uma noite. +{_combat.State.Brechas.BossBonusLumes(run.Rank)} Lumes na sua bolsa.");
    }

    /// <summary><c>finishRun</c>: bolsa no save, volta à Rua da Forja em frente ao portal, "De volta a Lumeira".</summary>
    public void FinishRun()
    {
        if (_run is not { } run || _combat.PlayerDead)
        {
            return;
        }

        _combat.State.MergeBag(run.Bag);
        _arrivalToast = ("De volta a Lumeira", BrechaRun.Summary(run.Bag));
        EndRun(cleared: true);
        World.LeaveDungeon(run.PortalSpot);
        GameManager.Instance.SaveGame();
    }

    // --- Fuga ---

    private const string FleeRequest = "flee";

    private void OnEscapeRequested(string mapId)
    {
        if (_run is null || _combat.PlayerDead)
        {
            return;
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.ChoiceRequested, FleeRequest, "Saída da Brecha",
            "Voltar a Lumeira agora? A Brecha continua aberta, mas você leva tudo o que coletou.",
            new[] { "Fugir para Lumeira", "Continuar lutando" });
    }

    private void OnChoiceMade(string requestId, int option)
    {
        if (requestId != FleeRequest || option != 0 || _run is not { } run || _combat.PlayerDead)
        {
            return;
        }

        _combat.State.MergeBag(run.Bag);
        EndRun(cleared: false);
        World.LeaveDungeon(run.PortalSpot);
        GameManager.Instance.SaveGame();
    }

    /// <summary>Fim da incursão (saída, fuga ou morte): <c>BrechaEnded</c>.</summary>
    public void EndRun(bool cleared)
    {
        if (_run is not { } run)
        {
            return;
        }

        _run = null;
        _themeRoomReady = false;
        World.Camera.LookAtTarget = null;
        EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaEnded, run.Theme.Id, cleared);
    }

    /// <summary>Jogo novo ou save carregado: nenhuma incursão nem invasão continua (o save não guarda Brechas).</summary>
    public void OnGameLoaded()
    {
        EndRun(cleared: false);
        Invasion.Reset();
    }

    /// <summary>Mensagem para depois da próxima troca de mapa (o <c>cb</c> do <c>goMap</c> da v2).</summary>
    public void ToastOnArrival(string title, string sub) => _arrivalToast = (title, sub);

    /// <summary>Porta da Brecha pulsando em magenta até as ondas acabarem; depois, luz âmbar (<c>drawGround</c>/<c>lightsList</c>).</summary>
    public void DrawDoor(FxLayer g, float time)
    {
        if (_run is null || _run.Room != BrechaRooms.Theme || World.Map.Door is not { } d)
        {
            return;
        }

        if (!_run.Cleared)
        {
            g.DrawRect(new Rect2(d.X, d.Y, d.Width, d.Height), CombatPalette.Brecha with { A = 0.25f + (0.1f * MathF.Sin(time * 4)) });
        }
        else
        {
            g.DrawRect(new Rect2(d.X, d.Y, d.Width, d.Height), CombatPalette.Ambar with { A = 0.18f + (0.08f * MathF.Sin(time * 3)) });
        }
    }

    private SeasonDef SeasonOf(string id)
    {
        foreach (var s in Db.SeasonData.Seasons)
        {
            if (s.Id == id)
            {
                return s;
            }
        }

        return _combat.State.Season;
    }
}

/// <summary>Brecha aberta por um portal da Cidade (id da sessão, spot, tema, rank, estação na abertura).</summary>
public sealed record OpenBrecha(int Id, int Spot, string ThemeId, int Rank, string SeasonId);
