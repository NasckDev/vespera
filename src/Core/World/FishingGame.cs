using Vespera.Core.Common;

namespace Vespera.Core.World;

public enum FishingState
{
    /// <summary>Linha na água, esperando a mordida (apertar agora espanta o peixe).</summary>
    Waiting,

    /// <summary>Mordeu: a agulha corre na barra; aperte com ela dentro da faixa.</summary>
    Biting,

    Caught,

    /// <summary>Puxou cedo, fora da faixa ou demorou demais.</summary>
    Escaped,
}

/// <summary>
/// Mini-interação de pesca (DECISIONS D46): espera aleatória até a mordida, depois uma agulha vai e volta numa barra
/// (<c>sweepSeconds</c> por ida) e o jogador aperta interagir com ela dentro de uma faixa sorteada. Sem Godot: o World
/// só desenha <see cref="Needle"/>, <see cref="ZoneStart"/> e <see cref="ZoneWidth"/>.
/// </summary>
public sealed class FishingGame
{
    private readonly double _sweepSeconds;
    private readonly int _maxSweeps;
    private double _biteAt;
    private double _biteTime;

    public FishingGame(NumRange biteDelay, double zoneWidth, double sweepSeconds, int maxSweeps, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _sweepSeconds = Math.Max(0.05, sweepSeconds);
        _maxSweeps = Math.Max(1, maxSweeps);
        ZoneWidth = Math.Clamp(zoneWidth, 0.02, 1);
        _biteAt = random.Range(biteDelay);
        ZoneStart = random.Range(0.08, 0.92 - ZoneWidth);
    }

    public FishingState State { get; private set; } = FishingState.Waiting;

    /// <summary>Segundos desde o lançamento.</summary>
    public double Elapsed { get; private set; }

    /// <summary>Quando o peixe morde (s desde o lançamento).</summary>
    public double BiteAt => _biteAt;

    /// <summary>Posição da agulha na barra (0–1), onda triangular a partir da mordida.</summary>
    public double Needle
    {
        get
        {
            if (State != FishingState.Biting && State != FishingState.Caught && State != FishingState.Escaped)
            {
                return 0;
            }

            var t = _biteTime / _sweepSeconds;
            var cycle = t % 2.0;
            return cycle <= 1.0 ? cycle : 2.0 - cycle;
        }
    }

    public double ZoneStart { get; }

    public double ZoneWidth { get; }

    public bool NeedleInZone => Needle >= ZoneStart && Needle <= ZoneStart + ZoneWidth;

    public bool IsOver => State is FishingState.Caught or FishingState.Escaped;

    /// <summary>Avança o tempo. Devolve true se o estado mudou (mordeu ou escapou por demora).</summary>
    public bool Update(double dt)
    {
        if (IsOver || dt <= 0)
        {
            return false;
        }

        Elapsed += dt;
        if (State == FishingState.Waiting)
        {
            if (Elapsed < _biteAt)
            {
                return false;
            }

            State = FishingState.Biting;
            _biteTime = Elapsed - _biteAt;
            return true;
        }

        _biteTime += dt;
        if (_biteTime >= _sweepSeconds * _maxSweeps)
        {
            State = FishingState.Escaped;
            return true;
        }

        return false;
    }

    /// <summary>Jogador puxou a linha. Devolve o estado final (ou o atual, se já acabou).</summary>
    public FishingState Pull()
    {
        if (IsOver)
        {
            return State;
        }

        State = State == FishingState.Biting && NeedleInZone ? FishingState.Caught : FishingState.Escaped;
        return State;
    }
}
