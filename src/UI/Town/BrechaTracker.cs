using System;
using System.Collections.Generic;
using Godot;
using Vespera.Autoload;
using Vespera.Core.Rules;

namespace Vespera.UI.Town;

/// <summary>Situação de uma Brecha vista pela UI (mural, HUD, diálogo do portal).</summary>
public enum BrechaStatus
{
    Open,
    Ruptured,
    Closed,
}

/// <summary>Uma Brecha anunciada pela Cidade (<c>PortalOpened</c>): portal, tema, rank e estação em que abriu.</summary>
public sealed class BrechaInfo
{
    public required int Id { get; init; }

    public required int Spot { get; init; }

    public required string ThemeId { get; init; }

    public required int Rank { get; init; }

    public required string SeasonId { get; init; }

    public BrechaStatus Status { get; set; } = BrechaStatus.Open;
}

/// <summary>
/// Lista das Brechas da sessão para a UI, montada só com os sinais da Cidade e do Combate (contratos da Fase B):
/// <c>PortalOpened</c> abre, <c>PortalClosed</c> fecha, <c>InvasionRequested</c> marca a ruptura e
/// <c>BrechaStarted</c>/<c>BrechaEnded</c> dizem em qual o jogador está. Não guarda timer (a Cidade não publica).
/// Um por World; acesse por <see cref="Current"/>.
/// </summary>
public partial class BrechaTracker : Node
{
    private const int MaxRemembered = 5; // o mural mostra até 5 (ui.js:63)

    private readonly List<BrechaInfo> _all = [];
    private int _nextId = 1;

    public static BrechaTracker? Current { get; private set; }

    /// <summary>Mais recentes primeiro (como o mural da v2).</summary>
    public IReadOnlyList<BrechaInfo> All => _all;

    /// <summary>Brecha em que o jogador está (entre BrechaStarted e BrechaEnded), ou null.</summary>
    public BrechaInfo? Inside { get; private set; }

    /// <summary>Tema da Brecha em curso (mesmo sem PortalOpened, ex.: smoke test).</summary>
    public string? InsideTheme { get; private set; }

    /// <summary>Spot do portal por onde o jogador entrou (para o diálogo de fuga).</summary>
    public int? EnteredSpot { get; private set; }

    /// <summary>Brecha rastreada no mural (<c>G.track</c>).</summary>
    public int? TrackedId { get; set; }

    public bool Invasion { get; private set; }

    /// <summary>Mudou algo (para HUD e mural redesenharem).</summary>
    public event Action? Changed;

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        var bus = EventBus.Instance;
        bus.PortalOpened += OnPortalOpened;
        bus.PortalClosed += OnPortalClosed;
        bus.InvasionRequested += OnInvasionRequested;
        bus.InvasionStateChanged += OnInvasionStateChanged;
        bus.BrechaStartRequested += OnStartRequested;
        bus.BrechaStarted += OnBrechaStarted;
        bus.BrechaEnded += OnBrechaEnded;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.PortalOpened -= OnPortalOpened;
        bus.PortalClosed -= OnPortalClosed;
        bus.InvasionRequested -= OnInvasionRequested;
        bus.InvasionStateChanged -= OnInvasionStateChanged;
        bus.BrechaStartRequested -= OnStartRequested;
        bus.BrechaStarted -= OnBrechaStarted;
        bus.BrechaEnded -= OnBrechaEnded;
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>Esquece todas as Brechas (jogo novo sem trocar de cena, smoke test).</summary>
    public void Clear()
    {
        _all.Clear();
        Inside = null;
        InsideTheme = null;
        EnteredSpot = null;
        TrackedId = null;
        Changed?.Invoke();
    }

    /// <summary>Brecha aberta no portal (spot), ou null.</summary>
    public BrechaInfo? OpenAt(int spot)
    {
        foreach (var b in _all)
        {
            if (b.Spot == spot && b.Status == BrechaStatus.Open)
            {
                return b;
            }
        }

        return null;
    }

    /// <summary>Título da Brecha na estação em que abriu (<c>THEMES.titles</c>).</summary>
    public static string Title(BrechaInfo brecha)
    {
        var db = Database.Db;
        return db.Themes.TryGetValue(brecha.ThemeId, out var theme) ? BrechaRules.Title(theme, brecha.SeasonId) : brecha.ThemeId;
    }

    public static string Bairro(string themeId) =>
        Database.Db.Themes.TryGetValue(themeId, out var theme) ? theme.Bairro : themeId;

    private void OnPortalOpened(int spot, string themeId, int rank)
    {
        // Um portal só tem uma Brecha aberta: a anterior no mesmo spot fecha.
        if (OpenAt(spot) is { } previous)
        {
            previous.Status = BrechaStatus.Closed;
        }

        var season = GameManager.Instance.State.Season.Id;
        _all.Insert(0, new BrechaInfo { Id = _nextId++, Spot = spot, ThemeId = themeId, Rank = rank, SeasonId = season });
        if (_all.Count > MaxRemembered)
        {
            _all.RemoveAt(_all.Count - 1);
        }

        Changed?.Invoke();
    }

    private void OnPortalClosed(int spot)
    {
        if (OpenAt(spot) is { } brecha)
        {
            brecha.Status = BrechaStatus.Closed;
            Changed?.Invoke();
        }
    }

    private void OnInvasionRequested(string themeId, int rank)
    {
        foreach (var b in _all)
        {
            if (b.ThemeId == themeId && b.Status == BrechaStatus.Open)
            {
                b.Status = BrechaStatus.Ruptured;
                break;
            }
        }

        Changed?.Invoke();
    }

    private void OnInvasionStateChanged(bool active)
    {
        Invasion = active;
        GameManager.Instance.State.Invasion = active; // preços ×1,35 (PriceCalculator); idempotente se o Combate também ligar
        if (!active)
        {
            foreach (var b in _all)
            {
                if (b.Status == BrechaStatus.Ruptured)
                {
                    b.Status = BrechaStatus.Closed;
                }
            }
        }

        Changed?.Invoke();
    }

    private void OnStartRequested(int spot) => EnteredSpot = spot;

    private void OnBrechaStarted(string themeId, int rank)
    {
        InsideTheme = themeId;
        Inside = EnteredSpot is { } spot && OpenAt(spot) is { } atSpot && atSpot.ThemeId == themeId ? atSpot : null;
        Changed?.Invoke();
    }

    private void OnBrechaEnded(string themeId, bool cleared)
    {
        if (cleared && Inside is { } inside)
        {
            inside.Status = BrechaStatus.Closed;
        }

        Inside = null;
        InsideTheme = null;
        Changed?.Invoke();
    }
}
