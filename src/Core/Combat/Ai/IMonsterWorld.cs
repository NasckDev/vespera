namespace Vespera.Core.Combat.Ai;

/// <summary>O que a IA precisa saber do jogador neste frame.</summary>
/// <param name="X">Pé do jogador (u).</param>
/// <param name="Aim">Ângulo de mira (o Ceifador teleporta para trás da mira).</param>
/// <param name="Dead">Jogador morto: monstros não agem.</param>
public readonly record struct PlayerView(double X, double Y, double Aim, bool Dead);

public enum MonsterProjectileKind
{
    /// <summary>Bolt reto que some ao acertar o jogador.</summary>
    Bolt,

    /// <summary>Corrente do Ceifador: atravessa paredes e puxa o jogador.</summary>
    Chain,
}

public enum MonsterSound
{
    Growl,
    Magic,
    Swing,
    Boom,
}

/// <summary>Efeitos visuais pedidos pela IA (o resto é desenhado a partir do estado).</summary>
public enum MonsterFx
{
    /// <summary>Arco do golpe do Ceifador (2.2 rad, r46).</summary>
    SwipeArc,

    /// <summary>Arco da varrida dos cavaleiros (3 rad, r66).</summary>
    SweepArc,

    /// <summary>Impacto do slam (anel verde r6→40 e terra).</summary>
    SlamImpact,

    /// <summary>Clarão na boca do cuspidor.</summary>
    SpitFlash,

    /// <summary>Fumaça do teleporte do Ceifador.</summary>
    TeleportPuff,

    /// <summary>Rastro da investida dos cavaleiros.</summary>
    ChargeTrail,
}

/// <summary>
/// Pedidos da IA ao mundo (o nó Godot implementa): mover com colisão, ferir o jogador, projéteis, invocações,
/// perigos de área (isca, maré, estilhaços, lanternas orbitais), som e efeitos. Mantém a IA pura e testável.
/// </summary>
public interface IMonsterWorld
{
    /// <summary>Número de monstros vivos que não são chefe (limite de invocação dos cavaleiros: 4).</summary>
    int AliveNonBossCount { get; }

    /// <summary><c>moveEnt</c>: anda em passos de até 3 u, eixo a eixo, só onde dá para ficar.</summary>
    void Move(MonsterBody m, double dx, double dy);

    /// <summary>
    /// Direção para perseguir o jogador: recebe a linha reta (unitária) e pode trocá-la pela do campo de perseguição
    /// quando a reta está bloqueada (DECISIONS D43).
    /// </summary>
    void ChaseDirection(MonsterBody m, ref double ux, ref double uy);

    /// <summary><c>canStand(x, y, r, fly)</c> no mapa atual.</summary>
    bool CanStand(double x, double y, double radius, bool fly);

    /// <summary><c>hurtPlayer(dmg, src)</c>; true se o dano entrou (jogador não estava invulnerável).</summary>
    bool HurtPlayer(double damage, double sourceX, double sourceY);

    void FireProjectile(MonsterBody owner, MonsterProjectileKind kind, string color, double x, double y, double vx, double vy, double radius, double damage, double life);

    /// <summary>
    /// Invoca <paramref name="count"/> monstros com telegraph de 0.9 s em pontos a ≥ <paramref name="minDistance"/> u do
    /// jogador e a até <paramref name="yRange"/> u (em y) do invocador. <paramref name="chase"/>: já nascem perseguindo.
    /// </summary>
    void Summon(MonsterBody summoner, string monsterId, int count, double minDistance, double yRange, bool chase);

    void Sound(MonsterBody m, MonsterSound sound);

    void Fx(MonsterBody m, MonsterFx fx, double x, double y, double angle);

    /// <summary>Tremor de câmera (mantém o maior).</summary>
    void Shake(double amount);

    void Toast(string title, string subtitle);

    /// <summary>Isca do Abissal: 2.6 s puxando o jogador (&lt; 75 u, 38 u/s), depois explode r44.</summary>
    void SpawnLure(double x, double y, double damage);

    /// <summary>Onda da maré: anel a 150 u/s até 115 u.</summary>
    void SpawnTideWave(double x, double y, double damage);

    /// <summary>Estilhaço da chuva dos cavaleiros: cai em 0.9 s depois de <paramref name="delay"/>; acerta r16.</summary>
    void SpawnShard(double x, double y, double delay, double damage, string color);

    /// <summary>Lanterna orbital do Arauto (6 s, tiro a cada 1.3 s; o primeiro após <paramref name="firstShot"/>).</summary>
    void SpawnOrbitLantern(MonsterBody boss, double angle, double firstShot);
}
