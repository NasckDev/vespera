using Vespera.Core.Advancement;

namespace Vespera.Core.Coop;

/// <summary>
/// Caçador convidado do co-op local (J2): progressão leve própria — rank (XP e conquistas), nível de arma, peças e
/// Fragmentos — guardada no mesmo espaço de save do J1, que é o dono da história e da progressão principal. As flags da
/// história só mudam pelo J1. Ausente (null) em saves que nunca tiveram co-op.
/// </summary>
public sealed class GuestHunter
{
    public const string DefaultName = "Convidado";

    public string Name { get; set; } = DefaultName;

    /// <summary>Classe (id da arma: sword, bow, hammer) escolhida na última entrada.</summary>
    public string ClassId { get; set; } = "bow";

    public HunterProgress Progress { get; set; } = new();

    /// <summary>Garante campos válidos depois de ler do JSON.</summary>
    public void Sanitize(IReadOnlyCollection<string> validClasses)
    {
        Name = string.IsNullOrWhiteSpace(Name) ? DefaultName : Name.Trim();
        if (validClasses.Count > 0 && !validClasses.Contains(ClassId))
        {
            ClassId = validClasses.First();
        }

        Progress ??= new HunterProgress();
    }
}
