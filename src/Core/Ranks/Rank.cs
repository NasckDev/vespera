namespace Vespera.Core.Ranks;

/// <summary>
/// Selos de caçador, do mais baixo ao mais alto (DECISIONS D4: 10 selos com SSSS). O valor numérico é o
/// índice em <c>ranks.json</c> (o <c>GameDatabase</c> valida que os dois batem). A ordem importa.
/// </summary>
public enum Rank
{
    E,
    D,
    C,
    B,
    A,
    S,
    SS,
    SSS,
    SSSS,
    Alvor,
}
