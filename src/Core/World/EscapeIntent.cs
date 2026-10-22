namespace Vespera.Core.World;

/// <summary>
/// Intenção de sair da Brecha pela borda de fuga (<c>back</c>, DECISIONS C1-D3). Na v2 qualquer passo dentro da
/// borda abria "Saída da Brecha" e travava a luta; aqui o pedido só sai depois de o jogador **insistir**: segurar o
/// movimento para dentro da borda por <see cref="HoldSeconds"/> (o tempo zera ao sair dela) ou interagir com a saída.
/// Uma vez disparado, só arma de novo depois que o pé sai da borda. Sem alocação.
/// </summary>
public sealed class EscapeIntent
{
    /// <summary>Segundos empurrando para dentro da borda até perguntar.</summary>
    public const double HoldSeconds = 0.5;

    /// <summary>Distância mínima (u) entre uma chegada e o topo da borda de fuga.</summary>
    public const float SpawnGap = 56f;

    /// <summary>Componente mínima da direção apontando para fora da sala (a borda fica no pé do mapa).</summary>
    public const float PushThreshold = 0.3f;

    private double _held;
    private bool _armed = true;

    /// <summary>0–1: quanto falta para perguntar (para um aviso na tela).</summary>
    public double Progress => Math.Clamp(_held / HoldSeconds, 0, 1);

    /// <summary>
    /// Um passo. <paramref name="inside"/>: pé dentro de <c>back</c>; <paramref name="pushing"/>: o jogador está
    /// andando para fora da sala. Devolve true no passo em que o pedido de fuga deve sair.
    /// </summary>
    public bool Update(bool inside, bool pushing, double dt)
    {
        if (!inside)
        {
            _held = 0;
            _armed = true;
            return false;
        }

        if (!pushing)
        {
            _held = 0;
            return false;
        }

        _held += dt;
        if (_armed && _held >= HoldSeconds)
        {
            _armed = false;
            return true;
        }

        return false;
    }

    /// <summary>Esquece o que estava acontecendo (troca de mapa, diálogo respondido).</summary>
    public void Reset()
    {
        _held = 0;
        _armed = true;
    }

    /// <summary>
    /// Pé na zona de fuga: dentro de <c>back</c> ou abaixo dela na mesma faixa (a borda fica no pé da sala e em vários
    /// mapas ainda dá para andar alguns u além dela; quem insiste para baixo atravessaria o retângulo).
    /// </summary>
    public static bool InZone(MapRect back, float x, float y) => x >= back.X && x <= back.Right && y >= back.Y;

    /// <summary>Direção de movimento conta como "para fora" (para baixo, onde ficam as bordas de fuga da v2).</summary>
    public static bool IsPushingOut(float directionY) => directionY > PushThreshold;

    /// <summary>
    /// Chegada longe da borda de fuga: se <paramref name="at"/> está a menos de <paramref name="gap"/> u acima de
    /// <paramref name="back"/> (na faixa horizontal dela, com folga de <paramref name="gap"/>), sobe até a distância
    /// mínima. Na v2 o Mercado de Peixes nascia 20 u acima da borda e um passo para baixo abria a fuga.
    /// </summary>
    public static MapPoint KeepAway(MapPoint at, MapRect back, float gap = SpawnGap)
    {
        var overlapsX = at.X >= back.X - gap && at.X <= back.Right + gap;
        if (!overlapsX || at.Y < back.Y - gap)
        {
            return at;
        }

        return new MapPoint(at.X, back.Y - gap);
    }
}
