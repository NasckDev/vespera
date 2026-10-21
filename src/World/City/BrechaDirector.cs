using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Common;
using Vespera.Core.Rules;
using Vespera.Core.World;

namespace Vespera.World.City;

/// <summary>
/// A véspera como evento (spec 01 §1.9, contratos da Fase B): abre as Brechas na hora certa pela
/// <see cref="BrechaSchedule"/> (<see cref="BrechaRules"/>), liga o portal da Rua da Forja, toca a abertura animada,
/// som, tremor e toast, conta os timers e rompe a Brecha ignorada (<c>InvasionRequested</c>). Escuta o Combate
/// (<c>BrechaStartRequested</c>, <c>BrechaStarted</c>, <c>BrechaEnded</c>, <c>InvasionStateChanged</c>) para parar o timer
/// da Brecha em curso e fechar portais. Dados para o Mural: <see cref="Brechas"/>, <see cref="TrackedId"/> e <see cref="BoardTexts"/>.
/// </summary>
public sealed class BrechaDirector(CityLife city)
{
    /// <summary>Tremor leve quando o portal abre na sua frente (a v2 não tremia; pedido da frente).</summary>
    public const float OpenShake = 3f;

    /// <summary>Ruptura: tremor 8 (<c>world.js:51</c>).</summary>
    public const float RuptureShake = 8f;

    private readonly List<BrechaEvent> _events = new(8);
    private readonly PortalVisual?[] _visuals = new PortalVisual?[2];
    private readonly List<BrechaInstance> _resolved = new(4);
    private BrechaSchedule? _schedule;
    private bool _invasion;

    /// <summary>Brechas da sessão (mais antiga primeiro, no máximo 5), como <c>G.brechas</c>.</summary>
    public IReadOnlyList<BrechaInstance> Brechas => Schedule.Brechas;

    /// <summary>Brecha em que o jogador está, ou null.</summary>
    public BrechaInstance? Running => Schedule.Running;

    /// <summary>Segundos até a próxima abrir.</summary>
    public double NextOpenIn => Schedule.NextOpenIn;

    /// <summary>Brecha rastreada no Mural (<c>G.track</c>), ou null.</summary>
    public int? TrackedId { get; private set; }

    /// <summary>Há invasão (Brecha rompida) na Rua da Forja.</summary>
    public bool InvasionActive => _invasion;

    public BrechaSchedule Schedule => _schedule ??= NewSchedule();

    public BrechaInstance? OpenAt(int spot) => Schedule.OpenAt(spot);

    public BrechaInstance? Find(int id) => Schedule.Find(id);

    /// <summary>Mural: rastrear uma Brecha aberta (<c>G.track = b.id</c>).</summary>
    public void Track(int brechaId)
    {
        if (Schedule.Find(brechaId) is { IsOpen: true })
        {
            TrackedId = brechaId;
        }
    }

    /// <summary>Novo jogo/save carregado: começa outra sessão de Brechas (a v2 não salva Brechas).</summary>
    public void Reset()
    {
        for (var i = 0; i < _visuals.Length; i++)
        {
            CloseVisual(i);
        }

        _schedule = NewSchedule();
        TrackedId = null;
        _invasion = false;
        RefreshInteractables();
    }

    /// <summary>Encurta a espera até a próxima Brecha (smoke e depuração).</summary>
    public void OpenSoon(double seconds = 0) => Schedule.OpenSoon(seconds);

    public void OnNewDay() => Schedule.OnNewDay();

    public void Connect(EventBus bus)
    {
        bus.BrechaStartRequested += OnBrechaStartRequested;
        bus.BrechaStarted += OnBrechaStarted;
        bus.BrechaEnded += OnBrechaEnded;
        bus.InvasionStateChanged += OnInvasionStateChanged;
    }

    public void Disconnect(EventBus bus)
    {
        bus.BrechaStartRequested -= OnBrechaStartRequested;
        bus.BrechaStarted -= OnBrechaStarted;
        bus.BrechaEnded -= OnBrechaEnded;
        bus.InvasionStateChanged -= OnInvasionStateChanged;
    }

    public void Step(double dt)
    {
        if (dt <= 0)
        {
            return;
        }

        var state = GameManager.Instance.State;
        _events.Clear();
        Schedule.Tick(dt, state.RankIndex, state.Save.Day, _events);
        for (var i = 0; i < _events.Count; i++)
        {
            Handle(_events[i]);
        }
    }

    /// <summary>Mapa novo: na Rua da Forja, liga os portais abertos e mostra cada um já em loop.</summary>
    public void OnMapChanged(MapDef map)
    {
        for (var i = 0; i < _visuals.Length; i++)
        {
            _visuals[i] = null; // o World liberou as entidades do mapa anterior
        }

        if (map.Id != World.CityMapId)
        {
            return;
        }

        foreach (var b in Schedule.Brechas)
        {
            if (b.IsOpen)
            {
                ShowPortal(b, opening: false);
            }
        }

        RefreshInteractables();
    }

    private BrechaSchedule NewSchedule() =>
        new(GameManager.Db, GameManager.Instance.State.Random, GameManager.Instance.State.Save.FirstBrechaDone);

    private void Handle(BrechaEvent e)
    {
        var b = e.Brecha;
        var bus = EventBus.Instance;
        switch (e.Kind)
        {
            case BrechaEventKind.Opened:
                OnOpened(b);
                break;
            case BrechaEventKind.ClosedByCasa:
                // world.js:149 — chat + nota; a Brecha some do mapa.
                var casa = b.ClosedBy ?? string.Empty;
                city.Townsfolk.Chat(GameManager.Instance.State.Random.Pick(GameManager.Db.Social.Hunters).Name, "B", TownChat.ClosedByCasaLine(casa, b.Bairro));
                bus.EmitSignal(EventBus.SignalName.NoteRequested, $"A {casa} fechou a Brecha do {b.Bairro}", "i_banner");
                ClosePortal(b);
                break;
            case BrechaEventKind.Ruptured:
                OnRuptured(b);
                break;
        }

        bus.EmitSignal(EventBus.SignalName.BrechaStatusChanged, b.Id, (int)b.Status);
    }

    /// <summary><c>openBrecha</c>: toast, som, chat; portal ligado e animado se o jogador estiver na Rua da Forja.</summary>
    private void OnOpened(BrechaInstance b)
    {
        var state = GameManager.Instance.State;
        state.Save.FirstBrechaDone = true; // D6: firstDone é salvo
        var rankId = GameManager.Db.Ranks[b.Rank].Id;
        Log.Info($"Brecha #{b.Id} abriu: {b.Bairro} · {b.Title} · Rank {rankId} · spot {b.Spot} · {b.MaxTime:0} s.");

        var bus = EventBus.Instance;
        bus.EmitSignal(EventBus.SignalName.ToastRequested, $"Uma Brecha se abriu — {b.Bairro}\n{b.Title} · Rank {rankId} · Portal na Rua da Forja");
        AudioManager.Instance?.PlaySfx("portal", pitchJitter: 0f);
        var hunter = state.Random.Pick(GameManager.Db.Social.Hunters);
        city.Townsfolk.Chat(hunter.Name, hunter.Rank, TownChat.OpenedLine(b.Bairro, state.Random));

        if (city.World.MapId == World.CityMapId)
        {
            ShowPortal(b, opening: true);
            bus.EmitSignal(EventBus.SignalName.CameraShakeRequested, OpenShake);
        }

        RefreshInteractables();
        bus.EmitSignal(EventBus.SignalName.PortalOpened, b.Spot, b.ThemeId, b.Rank);
    }

    /// <summary><c>rupture(b)</c>: toast, tremor 8, <c>boom</c>, chat e o pedido de invasão para o Combate.</summary>
    private void OnRuptured(BrechaInstance b)
    {
        Log.Info($"Brecha #{b.Id} ({b.Bairro}) ROMPEU.");
        _invasion = true;
        var bus = EventBus.Instance;
        ClosePortal(b);
        bus.EmitSignal(EventBus.SignalName.ToastRequested, $"A Brecha do {b.Bairro} ROMPEU\nMonstros invadem a Rua da Forja. A forja fechou e os preços subiram.");
        bus.EmitSignal(EventBus.SignalName.CameraShakeRequested, RuptureShake);
        AudioManager.Instance?.PlaySfx("boom", pitchJitter: 0f);
        city.Townsfolk.Chat("Iara", "B", TownChat.RuptureLine);
        city.Townsfolk.Chat("Lúcia", "—", TownChat.RuptureLuciaLine);
        bus.EmitSignal(EventBus.SignalName.InvasionRequested, b.ThemeId, b.Rank);
    }

    private void ClosePortal(BrechaInstance b)
    {
        CloseVisual(b.Spot);
        if (TrackedId == b.Id)
        {
            TrackedId = null;
        }

        RefreshInteractables();
        EventBus.Instance.EmitSignal(EventBus.SignalName.PortalClosed, b.Spot);
    }

    private void ShowPortal(BrechaInstance b, bool opening)
    {
        if (b.Spot < 0 || b.Spot >= _visuals.Length || _visuals[b.Spot] is not null)
        {
            return;
        }

        var spots = World.Catalog[World.CityMapId].PortalSpots;
        var spot = spots[b.Spot];
        var visual = new PortalVisual
        {
            Name = $"Portal{b.Spot}",
            Brecha = b,
            Labels = city.Labels,
            Position = new Vector2(spot.X, spot.Y),
            StartAge = opening ? 0f : PortalVisual.OpeningSeconds,
        };
        city.World.Entities.AddChild(visual);
        _visuals[b.Spot] = visual;
    }

    private void CloseVisual(int spot)
    {
        if (_visuals[spot] is { } visual && GodotObject.IsInstanceValid(visual))
        {
            visual.Close();
        }

        _visuals[spot] = null;
    }

    /// <summary>Liga <c>portal0</c>/<c>portal1</c> conforme as Brechas abertas, com o rótulo "Brecha · bairro".</summary>
    private void RefreshInteractables()
    {
        if (city.World.Map is not { Id: World.CityMapId })
        {
            return;
        }

        for (var spot = 0; spot < _visuals.Length; spot++)
        {
            var b = Schedule.OpenAt(spot);
            city.World.SetInteractableEnabled($"portal{spot}", b is not null, b is null ? null : $"Brecha · {b.Bairro}");
        }
    }

    private void OnBrechaStartRequested(int portalSpot)
    {
        if (Schedule.EnterRun(portalSpot) is { } b)
        {
            TrackedId = b.Id;
        }
    }

    private void OnBrechaStarted(string themeId, int rank)
    {
        if (Schedule.Running is not null)
        {
            return;
        }

        foreach (var b in Schedule.Brechas)
        {
            if (b.IsOpen && b.ThemeId == themeId)
            {
                Schedule.EnterRun(b.Spot);
                return;
            }
        }
    }

    private void OnBrechaEnded(string themeId, bool cleared)
    {
        var run = Schedule.EndRun(cleared);
        if (run is not null && cleared)
        {
            ClosePortal(run);
            EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStatusChanged, run.Id, (int)run.Status);
        }
    }

    private void OnInvasionStateChanged(bool active)
    {
        _invasion = active;
        _resolved.Clear();
        if (!active && Schedule.ResolveInvasion(_resolved) is not null)
        {
            foreach (var b in _resolved)
            {
                EventBus.Instance.EmitSignal(EventBus.SignalName.BrechaStatusChanged, b.Id, (int)b.Status);
            }
        }
    }
}

/// <summary>Textos do Mural de Contratos (<c>uiBoard</c>, <c>ui.js:55-79</c>), verbatim da v2.</summary>
public static class BoardTexts
{
    public const string Title = "Mural de Contratos";

    public const string Subtitle = "A Casa da Maré adotou a Rua da Forja: 10% de desconto em toda forja e oficina.";

    public const string InvasionSuffix = " Preços +50% enquanto houver invasão.";

    public const string Empty = "Nenhuma Brecha aberta. A próxima véspera não deve demorar.";

    public static string Footer(int closed) => $"Brechas fechadas por você: {closed}";

    /// <summary>"bairro — título".</summary>
    public static string Heading(BrechaInstance b) => $"{b.Bairro} — {b.Title}";

    /// <summary>"estação · modificador".</summary>
    public static string SeasonLine(BrechaInstance b) => $"{b.Season.Name} · {b.Season.ModText}";

    public static string Status(BrechaInstance b, bool tracked) => b.Status switch
    {
        BrechaStatus.Open => "Portal na Rua da Forja" + (tracked ? " · rastreada" : string.Empty),
        BrechaStatus.Ruptured => "ROMPEU — monstros na Rua da Forja",
        _ => "Fechada",
    };

    /// <summary>Timer "m:ss" (magenta abaixo de 60 s).</summary>
    public static string Timer(BrechaInstance b) => SeasonCalendar.FormatTimer(b.TimeLeft);
}
