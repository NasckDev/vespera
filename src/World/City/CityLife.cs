using System;
using Godot;
using Vespera.Autoload;
using Vespera.Core.World;
using Vespera.Testing;

namespace Vespera.World.City;

/// <summary>
/// Cidade viva (frente B2): relógio do dia, véspera e Brechas, iluminação/estações/clima, NPCs com rotina, caçadores
/// ambiente e chat simulado. Fica dentro da cena <c>World</c> (sobrevive às trocas de mapa, como o <c>G</c> da v2) e
/// fala com o resto só pelo <see cref="EventBus"/>. API para as outras frentes em <c>docs/design/api-cidade.md</c>.
/// <para>
/// Uma única passada por frame avança, nesta ordem, relógio → Brechas → moradores (<c>updWorld</c>/<c>updNpcs</c>/
/// <c>updHunters</c> da v2). Tudo para com a pausa (<c>PauseChanged</c>) e com telas que bloqueiam o jogo
/// (<see cref="GameManager.BlockGameplay"/>), menos a troca de mapa, como a v2 (só <c>G.ui</c> congelava o mundo).
/// </para>
/// </summary>
public partial class CityLife : Node
{
    private const float MaxDelta = 0.05f; // main.js:18

    private bool _paused;
    private float _timeScale = 1f;

    [Export]
    public World World { get; set; } = null!;

    /// <summary>A cidade da cena atual (null fora do World).</summary>
    public static CityLife? Instance { get; private set; }

    public DayClock Clock { get; private set; } = null!;

    public BrechaDirector Brechas { get; private set; } = null!;

    public TownsfolkDirector Townsfolk { get; private set; } = null!;

    public LightingLayer Lighting { get; private set; } = null!;

    public WorldLabels Labels { get; private set; } = null!;

    /// <summary>
    /// Relógio, Brechas e moradores ligados. No smoke test começa desligado para os cenários das outras frentes não
    /// verem portais, NPCs ou troca de música aparecendo sozinhos; os cenários da cidade ligam e desligam.
    /// </summary>
    public bool SimulationEnabled { get; private set; } = !SmokeTest.IsRequested();

    /// <summary>Pausa vinda da Interface (<c>PauseChanged</c>).</summary>
    public bool Paused => _paused;

    /// <summary>Mundo congelado neste frame: simulação desligada, pausa ou uma tela bloqueando o jogo (fora a troca de mapa).</summary>
    public bool IsFrozen => !SimulationEnabled || _paused || (GameManager.Instance.IsGameplayBlocked && !World.IsTransitioning);

    /// <summary>Acelera relógio e timers das Brechas. Só tem efeito no smoke test.</summary>
    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = SmokeTest.IsRequested() ? Math.Max(0f, value) : 1f;
    }

    /// <summary>Segundos de sessão que a cidade já simulou (<c>G.t</c> sem pausas).</summary>
    public double SessionTime { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Labels = new WorldLabels { Name = "Labels" };
        Lighting = new LightingLayer { Name = "Lighting", World = World, City = this };
        AddChild(Lighting);
        AddChild(Labels);

        Clock = new DayClock(this);
        Brechas = new BrechaDirector(this);
        Townsfolk = new TownsfolkDirector(this);

        var bus = EventBus.Instance;
        bus.MapChanged += OnMapChanged;
        bus.DayChanged += OnDayChanged;
        bus.GameLoaded += OnGameLoaded;
        bus.PauseChanged += OnPauseChanged;
        bus.InteractionRequested += OnInteractionRequested;
        Brechas.Connect(bus);

        if (World.Map is { } map)
        {
            OnMapChanged(map.Id, map.Name); // o World já carregou o primeiro mapa no _Ready dele
        }

        if (SimulationEnabled)
        {
            Clock.Refresh();
        }
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.MapChanged -= OnMapChanged;
        bus.DayChanged -= OnDayChanged;
        bus.GameLoaded -= OnGameLoaded;
        bus.PauseChanged -= OnPauseChanged;
        bus.InteractionRequested -= OnInteractionRequested;
        Brechas.Disconnect(bus);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Liga ou desliga a simulação (usado pelo smoke). Ligar emite a fase atual e povoa o mapa.</summary>
    public void SetSimulationEnabled(bool enabled)
    {
        if (enabled == SimulationEnabled)
        {
            return;
        }

        SimulationEnabled = enabled;
        if (enabled)
        {
            Clock.Reset();
            Clock.Refresh();
            Townsfolk.Populate(World.Map);
        }
        else
        {
            Townsfolk.Clear();
        }
    }

    public override void _Process(double delta)
    {
        if (IsFrozen)
        {
            return;
        }

        var dt = Math.Min(MaxDelta, delta);
        var simDt = dt * _timeScale;
        SessionTime += simDt;
        Clock.Step(simDt);
        Brechas.Step(simDt);
        Townsfolk.Step(dt);
    }

    private void OnMapChanged(string mapId, string mapName)
    {
        var map = World.Map;
        Lighting.SetMap(map);
        Brechas.OnMapChanged(map);
        if (SimulationEnabled)
        {
            Townsfolk.Populate(map);
        }
    }

    /// <summary><c>newDay(msg)</c>: toast do dia, a próxima Brecha em até 15 s e save (<c>world.js:137-141</c>).</summary>
    private void OnDayChanged(int day)
    {
        var state = GameManager.Instance.State;
        var message = Clock.RollingOver ? "A noite passou sem você perceber." : "Você dorme. Lá fora, a cidade aguenta.";
        EventBus.Instance.EmitSignal(EventBus.SignalName.ToastRequested, $"Dia {day} · {state.Season.Name}\n{message}");
        Brechas.OnNewDay();
        Clock.Refresh();
        if (SimulationEnabled)
        {
            Townsfolk.OnHourChanged(World.Map);
            GameManager.Instance.SaveGame();
        }
    }

    private void OnGameLoaded()
    {
        Clock.Reset();
        Brechas.Reset();
        if (SimulationEnabled)
        {
            Clock.Refresh();
        }
    }

    private void OnPauseChanged(bool paused) => _paused = paused;

    /// <summary>Mural de Contratos: a Interface desenha a tela "board" com os dados de <see cref="BrechaDirector"/>.</summary>
    private void OnInteractionRequested(string kind, string id)
    {
        if (kind == "openBoard")
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.ScreenRequested, "board");
        }
    }

    /// <summary>Hora do relógio mudou (usado para NPCs entrarem e saírem do mapa).</summary>
    internal void OnHourChanged()
    {
        if (SimulationEnabled)
        {
            Townsfolk.OnHourChanged(World.Map);
        }
    }

    /// <summary>Estação que pinta a tela e o clima: a da Brecha em curso dentro dela, senão a do dia (<c>render.js:50</c>).</summary>
    public string CurrentSeasonId()
    {
        if (World.Map is { Dungeon: true } && Brechas.Running is { } run)
        {
            return run.Season.Id;
        }

        return GameManager.Instance.State.Season.Id;
    }

    /// <summary>Fase do dia atual (pelo relógio, mesmo com a simulação desligada).</summary>
    public DayPhase Phase => DayPhases.Of(GameManager.Instance.State.Save.ClockMinutes, GameManager.Db.SeasonData.Clock);
}
