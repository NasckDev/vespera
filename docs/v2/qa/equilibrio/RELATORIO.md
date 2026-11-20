# Frente equilibrio (19/11/2026): chefes, exames, Golpe Supremo e arena do Ato I

Branch `f/equilibrio`, base v2 = d41bd08. Issues #75, #79, #80 e #84.

## Como medir a luta de Guardião (#75)

- A "vida do chefe" do JSON não é a vida em jogo: o HP escala por rank (`1 + 0,4 x rank`, `MonsterScaling`) e o Reflexo ainda tem `HpMult`. A conta do relatório de QA (`HP / DPS`) ignorava isso e também o dano real por golpe da arma.
- Os smokes usam bot invulnerável com dano x10 e a arma inicial, o que não mede nada de jogador. Foi criada uma medição própria:
  - `--bot-duelo` (flag de smoke): na sala do Guardião o bot empunha a arma do degrau do Guardião (`DuelGear`, nível `FirstLevelOf(rank)`) e luta com dano real (x1). `--bot-dano=N` muda o multiplicador do bot.
  - Cada Guardião imprime `guardião X: luta de N s` (tempo de jogo do `RoomDirector`, sem os congelamentos de impacto). Limite de sala do bot só para salas de Guardião: 120 s -> 420 s (a luta mais longa passa de 2 min); limite da luta do Reflexo no smoke final: 180 s -> 420 s.
- Resultado (bot, Lâmina do degrau, dano x1, invulnerável; uma rodada por valor, há variância de ~20%):

| Guardião (ato) | HP antes | tempo antes | HP depois | tempo depois | meta |
|---|---|---|---|---|---|
| Lodo-Mãe (I) | 640 | 18 a 26 s | 1500 | 51 s | 45 a 60 s |
| Macieira Oca (II) | 1100 | não medido (modelo ~23 s) | 2630 | não medido (modelo ~55 s) | ~55 s |
| Farol Afogado (III) | 1500 | não medido (modelo ~23 s) | 4050 | não medido (modelo ~62 s) | ~62 s |
| Velória (IV) | 1900 | 68 s | 1900 | 51 a 69 s | ~70 s |
| Sineiro Mudo (V) | 2200 | 36 s | 6000 | 73 s | ~85 s |
| Inquilino (VI) | 2600 | 27 s | 6000 | 99 s | ~100 s |
| Dupla do Avesso (VII, cada) | 1500 a 1800 | smoke derruba em ~3 s (atalho) | 7600 a 8600 | não medido | ~115 s a dupla |
| Reflexo (final) | 2000 | 12 s | 38000 | 149 s (2,5 min) | 2 a 3 min |

- A Macieira, o Farol e a dupla não têm smoke que os enfrente com o bot de combate; os valores deles seguem o modelo calibrado nos medidos (tempo ~ HP efetivo / dano da arma). Medir com um duelo próprio é pendência.
- Teto do Lodo-Mãe: com 2000 de vida o bot do `--ato1` (sem invulnerabilidade) não vence em 5 tentativas; 1450 e 1500 ele vence. Ficou em 1500.
- Contratos "Relâmpago" (vencer o Guardião em N minutos): o tempo do Guardião só era gravado para a Lodo-Mãe (`_bossSeconds` só existe nela), então o contrato nunca fechava nas outras Brechas. Agora todo Guardião grava o tempo; metas ajustadas em `cacada_ruas`, `cacada_ruas_b`, `cacada_avesso` (4 min) e `cacada_final` (5 min).

## #79 Exames da Ordem

- Causa raiz: nenhum código lia `objectives`, `timeLimitSeconds` nem `maxHits` de `exams.json`; `RiftLoot.GuardianDefeated` aprovava o exame ao matar o Guardião.
- Correção: `ExamJudge` (Core, puro) confere objetivos, tempo e limite de golpes; `ProgressionEngine.TryPassExam(rank, run)` devolve `ExamStatus.Failed` com os motivos e não grava o selo; `ExamRun` (jogo) avisa as regras ao entrar, avisa quando o limite de golpes ou o tempo estoura e escreve o resultado no aviso do Guardião. Reprovou: o rank não sobe até repetir a Brecha do Guardião (ela continua disponível).
- O exame de um rank é a Brecha do Guardião do rank (C Farol, B Velória, S Inquilino, SSS Reflexo). "Chegar à saída" = fechar a Brecha; "matar N" não conta o Guardião; "sobreviver N s" = ter jogado N s.
- Ajustes de dados (proposta de design, o dono confirma):
  - Tempos impossíveis para uma Brecha de 5 a 7 salas com Guardião: C 150 -> 420 s, S 150 -> 600 s, SSS 240 -> 420 s (B ficou em 600 s).
  - SSS: "derrotar 40 ecos" é impossível, a Brecha final só tem o Reflexo (sem crias). Virou "fechar a Brecha" (derrotar o Reflexo) com no máximo 6 golpes.
- O bot de teste (invulnerável, dano x10) não presta exame: para ele vale só a regra de rank, como antes. A cobertura do caminho real é dos testes de unidade (`ExamJudgeTest`, 11 casos).

## #80 Golpe Supremo e bênçãos

- Golpe Supremo ligado: nova ação `supremo` (X no teclado, Kp5 no J2, R3 no controle; remapeável; aparece na Ajuda e numa dica), botão no comando do co-op (`CommandButtons.Supremo`). Recarga e nome de `ultimates.json`, números de `UltimateNumbers`, escala de alcance 2,5 (alcance da Lâmina 120 px / 48 px do protótipo). Só efeitos que já existiam:
  - Lâmina, Fenda Rubra: 3 investidas cortantes, invulnerável durante.
  - Arco, Chuva de Vésperas: chuva de flechas por 2,5 s na área marcada à frente.
  - Martelo, Terremoto da Forja: 3 ondas de choque que ferem e atordoam (chefe, metade do atordoamento).
  - Aviso "Golpe Supremo pronto (tecla)" quando recarrega. O bot de combate agora usa o Supremo e o smoke exige que ele dispare.
- Duas bênçãos viraram bênçãos com efeito (campos novos `ultDamagePct`, `ultCooldownPct`, `ultKillSeconds`): Brasa Suprema (Chama, substitui Lâmina Quente) e Sede de Véspera (Aposta, substitui Sorte do Ravi).
- Ficou de fora: Cajado e Adagas (sem classe no jogo); ícone de recarga do Supremo na HUD (hoje só o aviso); 1 verbo novo por classe além do Supremo; modificadores de véspera e sala de escolha (encruzilhada/santuário, `RoomKind`); as outras 28 bênçãos seguem numéricas.

## #84 Arena do Ato I

- Medido (grade da máscara; humano = 0,75 x doorHeight): arena do esgoto 10,2 x 9,5 humanos, 54 humanos²; outras arenas 113 a 171 humanos² (Torre 113, Pomar 116, Cemitério 121, Cais 133, Ruas 171).
- A máscara já é a plataforma de pedra inteira pintada no chão; em volta é água de lodo e entulho. Alargar a máscara faria o caçador andar sobre água/entulho pintados. Mudar a escala (`doorHeight`) quebraria a régua do PLANO-V2 (arcos gradeados). Não há ampliação segura sem arte.
- Briefing de arte: repintar `assets/v2/cenarios/definitivos/chao_esgoto_arena.png` (1672x941) com plataforma central de ~14 humanos (~1100 px) de largura por ~10,5 de altura (~830 px), mantendo a escada de entrada e o arco gradeado da régua; depois ampliar `grid` em `data/levels/esgoto_arena.json` para a nova plataforma e reposicionar `spawn`, `doorAt` e `rift.boss`. Atenuante do QA: o roteiro quer a Lodo-Mãe pequena e de 1 fase; a arena ainda não foi medida em jogo com o co-op.
