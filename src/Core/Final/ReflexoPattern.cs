using System.Text.Json;

namespace Vespera.Core.Final;

/// <summary>
/// Ritmo de luta do Reflexo (Guardião do Final, ROTEIRO-V2 §13; arena "praça de baixo"). Valores em
/// <c>src/Data/brecha_final.json</c>, seção <c>"reflexo"</c>; distâncias na escala 1 do caçador. Cada lista "por fase" tem
/// três valores (fases 1, 2 e 3).
/// </summary>
public sealed class ReflexoTuning
{
    /// <summary>Aviso âmbar (s) antes de cada golpe, por fase: encurta conforme ele aprende.</summary>
    public List<double> Telegraph { get; set; } = [0.85, 0.7, 0.55];

    /// <summary>Pausa (s) entre um golpe e o próximo, por fase.</summary>
    public List<double> Cooldown { get; set; } = [1.8, 1.4, 1.05];

    /// <summary>Multiplica a chance de esquiva do perfil, por fase.</summary>
    public List<double> DodgeScale { get; set; } = [0.6, 0.85, 1.0];

    /// <summary>Mínimo entre duas esquivas (s).</summary>
    public double DodgeCooldown { get; set; } = 1.4;

    public double DodgeDistance { get; set; } = 220;

    public double DodgeSeconds { get; set; } = 0.25;

    /// <summary>Uma fala por este tempo de luta (s): "uma frase por minuto".</summary>
    public double LineSeconds { get; set; } = 60;

    /// <summary>Lâmina: investida curta e corte em leque na frente.</summary>
    public double BladeReach { get; set; } = 175;

    public double BladeLunge { get; set; } = 150;

    public double BladeDamage { get; set; } = 1.0;

    /// <summary>Arco: flechas por fase (uma faixa âmbar por flecha), abertura entre elas (rad) e distância que mantém.</summary>
    public List<int> BowArrows { get; set; } = [1, 2, 3];

    public double BowSpread { get; set; } = 0.28;

    public double BowRange { get; set; } = 900;

    public double BowWidth { get; set; } = 48;

    public double BowKeep { get; set; } = 380;

    public double BowDamage { get; set; } = 0.75;

    /// <summary>Martelo: salta até onde o caçador estava e afunda o chão (círculo âmbar).</summary>
    public double HammerRadius { get; set; } = 190;

    public double HammerLeap { get; set; } = 320;

    public double HammerDamage { get; set; } = 1.35;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Lê a seção <c>"reflexo"</c> do JSON da Brecha final e confere as listas por fase.</summary>
    public static ReflexoTuning FromBrechaJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        if (!doc.RootElement.TryGetProperty("reflexo", out var section))
        {
            throw new FormatException("brecha final sem a seção 'reflexo'");
        }

        var tuning = section.Deserialize<ReflexoTuning>(Options) ?? throw new FormatException("seção 'reflexo' vazia");
        var problems = tuning.Validate();
        return problems.Count == 0 ? tuning : throw new FormatException($"reflexo: {string.Join("; ", problems)}");
    }

    public List<string> Validate()
    {
        var problems = new List<string>();
        void ThreePositive(string name, IReadOnlyCollection<double> values)
        {
            if (values.Count != 3 || values.Any(v => v <= 0))
            {
                problems.Add($"{name} precisa de 3 valores positivos (um por fase)");
            }
        }

        ThreePositive("telegraph", Telegraph);
        ThreePositive("cooldown", Cooldown);
        ThreePositive("dodgeScale", DodgeScale);
        if (BowArrows.Count != 3 || BowArrows.Any(n => n < 1))
        {
            problems.Add("bowArrows precisa de 3 valores >= 1");
        }

        if (LineSeconds <= 0 || DodgeCooldown < 0)
        {
            problems.Add("lineSeconds/dodgeCooldown inválidos");
        }

        return problems;
    }
}

/// <summary>
/// Escolhas do Reflexo durante a luta, sem engine (testável): fase pela vida, qual arma vem no próximo golpe, quanto dura o
/// aviso âmbar, se esquiva e para que lado, e qual fala toca em cada minuto.
/// <para>
/// O roteiro diz que ele "não tem fases: aprende"; a luta tem três fases de intensidade (limiares de vida em
/// <c>boss.phases</c>) que mudam o ritmo, e o que ele aprende muda o golpe: a arma que lidera é a do minuto
/// (<see cref="ReflexoProfile.AttackForMinute"/>) e, a cada fase, ele soma a arma seguinte do ranking do jogador.
/// Fase 1: só a do minuto. Fase 2: alterna duas. Fase 3: as três, na ordem de uso do jogador.
/// </para>
/// </summary>
public sealed class ReflexoPattern(ReflexoProfile profile, ReflexoTuning tuning, IReadOnlyList<double> phases)
{
    private int _turn;

    public ReflexoProfile Profile => profile;

    public ReflexoTuning Tuning => tuning;

    /// <summary>Fase (1, 2 ou 3) pela fração de vida restante.</summary>
    public int Phase(double hpFraction)
    {
        if (phases.Count < 2 || hpFraction > phases[0])
        {
            return 1;
        }

        return hpFraction > phases[1] ? 2 : 3;
    }

    /// <summary>Armas que ele usa nesta fase e neste minuto, começando pela do minuto.</summary>
    public IReadOnlyList<ReflexoAttack> Pool(int phase, int minute)
    {
        var count = Math.Clamp(phase, 1, 3);
        var pool = new List<ReflexoAttack>(count);
        for (var i = 0; i < count; i++)
        {
            pool.Add(profile.AttackForMinute(Math.Max(0, minute) + i));
        }

        return pool;
    }

    /// <summary>Golpe que vem a seguir, sem gastar a vez (para escolher a distância).</summary>
    public ReflexoAttack Peek(int phase, int minute)
    {
        var pool = Pool(phase, minute);
        return pool[_turn % pool.Count];
    }

    /// <summary>Próximo golpe (alterna dentro do <see cref="Pool"/>).</summary>
    public ReflexoAttack Next(int phase, int minute)
    {
        var attack = Peek(phase, minute);
        _turn++;
        return attack;
    }

    public double Telegraph(int phase) => tuning.Telegraph[Index(phase)];

    public double Cooldown(int phase) => tuning.Cooldown[Index(phase)];

    public int Arrows(int phase) => tuning.BowArrows[Index(phase)];

    /// <summary>Chance de esquivar um golpe do caçador nesta fase (perfil × fase, até o máximo do perfil).</summary>
    public double DodgeChance(int phase) =>
        Math.Clamp(profile.DodgeChance * tuning.DodgeScale[Index(phase)], 0, ReflexoProfile.MaxDodgeChance);

    /// <summary>Esquiva se o sorteio (0..1) cair abaixo da chance.</summary>
    public bool ShouldDodge(int phase, double roll) => roll < DodgeChance(phase);

    /// <summary>Para onde ele esquiva no eixo X: o lado em que o jogador mais esquiva (-1 esquerda, +1 direita).</summary>
    public int DodgeDirection => profile.DodgeSide == DodgeSide.Left ? -1 : 1;

    /// <summary>Minuto de luta (0 = primeiro).</summary>
    public int MinuteAt(double seconds) => seconds <= 0 ? 0 : (int)(seconds / tuning.LineSeconds);

    private static int Index(int phase) => Math.Clamp(phase, 1, 3) - 1;
}
