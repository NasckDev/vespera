# Véspera — Complemento técnico da campanha (Atos III ao Final)

**A história canônica é `docs/v2/ROTEIRO-V2.md`** (verdade §11, linha do tempo §12, falas §13). Este documento não muda
nenhum fato de lá: só detalha o que o roteiro deixa para o design — mapas, monstros, Guardiões por fase, mecânica nova,
mudança na cidade e onde cada fala entra. Vocabulário do glossário §2.2 (Ano Sem Sol, Brecha, Avesso, Fragmento,
Lanterna, Selo, Guardião, Eco, Alvor). Falas em JSON: `data/dialogue/ato3 … final` (ver `data/dialogue/LEIA-ME-ATOS-3-A-FINAL.md`).
Decisões registradas em `docs/design/CHANGELOG.md`.

Regras herdadas do roteiro (§2.3): uma mecânica nova por ato, ensinada pelo nível; um mistério pago e um plantado por
ato; uma mudança visível em Lumeira; Brecha de 3 a 5 salas + Guardião, até 12 min; objetivo sempre na tela (5 palavras).
Todo golpe forte de monstro tem aviso âmbar de 0,4–0,8 s (PLANO §2).

## Resumo

| Ato | Selo | Bairro → Brecha | Mecânica nova | Guardião (fases) | A cidade muda | Duração |
|---|---|---|---|---|---|---|
| III | C→B | Mercado das Marés → Cais Afogado | pesca na véspera; arena que inunda | Farol Afogado (3) | barco do Severo leva à ilhota com Brecha opcional | 25–35 min |
| IV | B→A | Cemitério das Lanternas → Cemitério das Lanternas (Brecha) | 1º slot de Eco; o Vigia; Ravi em perigo | Velória (3) | memorial com os nomes; lanterna do Ravi (rota B) | 25–35 min |
| V | A→S | Torre do Sino → Sino Mudo | sala do silêncio; 2º slot de Eco | Sineiro Mudo (3) | o sino vira relógio da véspera no HUD | 25–35 min |
| VI | S→SS | Ruas Baixas → Ruas Invertidas | Sopa da Véspera (2ª vida); sub-arena na casa | Inquilino (3) | Ruas Baixas reformadas; a mãe do Ravi aparece | 25–35 min |
| VII | SS→SSS | Avesso profundo (bairros misturados) | Guardiões em dupla; bênçãos de 3 famílias | 3 duplas | a praça escurece de dia; a grande lanterna pisca | 30–40 min |
| Final | Alvor | Praça → a primeira Brecha | o Reflexo | O Reflexo (aprende, sem fases) | quem toca o sino | 20–30 min |

Estágio visual sobe junto com o selo (§8.2): C–B caçador (Ato III), A–S veterano (Ato V), SS–SSS elite (Ato VI→VII), Alvor radiante.

---

## Ato III — "O Farol que Não Acendeu" (C → B)

**Hub — Mercado das Marés** (issue #29, A13): cais de madeira na névoa, bancas de peixe, o galpão e o barco do Severo,
o farol apagado na ponta de pedra (porta com corrente), o ponto de pesca no fim do píer. Muro com nomes no quebra-mar
(o marido da Nilza está nele — §12) como narrativa ambiental, sem fala.

**Brecha — Cais Afogado:** o porto na noite da Festa das Lanternas, 20 anos atrás, do lado de baixo: bandeirolas e
lanternas de papel boiando apagadas, barcos de cabeça para baixo no céu, chuva que cai para cima, o farol escuro ao fundo.
Salas (baralho §5.4):
1. **Píer das Bandeirolas** (combate, corredor) — ensina o Afogado: tábuas estreitas, dar a volta.
2. **Mercado Submerso = a sala alagada** (encruzilhada) — água até o joelho (−20% de velocidade fora das tábuas); numa pedra
   seca no centro está a **lanterna "M."** (`ato3/lanterna_m`). Aparece só na 2ª caçada em diante, garantida.
3. **Barco Virado** (santuário) — dentro de um casco virado; lanterna apagada: acender cura e dá bênção, chama elite.
4. **Quebra-mar dos Postes** (tesouro/elite) — postes de lanterna apagados, baú no fim; elite *Afogado Blindado*.
5. **Arena — Pé do Farol** — plataforma redonda na base do farol; anel externo mais baixo que **inunda**.

**Monstros** (issue #30):
- **Afogado** (C) — *avança*: lento, pesado, agarra quem fica na frente. Aviso: para de pingar, ergue os braços, capacete
  de escafandro brilha âmbar 0,7 s → pancada dupla à frente. Partes: capacete (sem ele anda em linha reta), corda (sem ela
  não agarra) → *Bronze Salgado*, *Corda Encharcada*. Costas levam dano dobrado.
- **Lanterna-Afogada** (C) — *derrama*: lanterna de papel da festa cheia de água; flutua sobre o jogador e despeja um
  círculo. Aviso: a sombra no chão escurece e a vela de dentro brilha âmbar 0,8 s. Frágil (2 golpes), foge para trás dos
  Afogados. Parte: casca → *Papel de Festa*.
- **Ancoreiro** (C) — *ancora*: caranguejo com uma âncora no lugar da garra; arremessa em linha reta e puxa o jogador.
  Aviso: corrente chacoalha, linha âmbar no chão 0,6 s (sair para o lado). Parte: garra-âncora → *Ferro de Âncora*.

**Guardião — Farol Afogado** (faroleiro de bronze, 3 m, a lanterna do farol no lugar da cabeça):
1. **Feixe** (100–66%): o feixe gira como farol; tocar = ser puxado até a pisada. Aviso: lente âmbar + linha do caminho 0,6 s.
   Partes: **lente** (quebrada, acaba o puxão), **2 ombreiras**.
2. **Maré** (66–33%): chama Afogados da água (2 por onda; uma a menos por ombreira quebrada); o anel externo começa a alagar.
3. **Escuro** (33–0%): a arena inunda (zona seca muda de lugar a cada 15 s), a luz apaga; só a lanterna do jogador
   ilumina. Fala única: *"Eu apaguei por uma noite."* (`ato3/farol`). O aviso vira som (buzina) + brilho fraco da lente.
- **Eco do Farol:** feixe que puxa inimigos para um ponto. **Conjunto Bronze Salgado** (2: imune a puxão; 4: o puxão dos
  seus golpes pesados arrasta inimigos pequenos).

**Mecânica nova — pesca na véspera:** ponto de pesca no píer; minigame de um botão no tempo (o "olho de vidro" brilha).
Peixes do Avesso são ingredientes raros da Nilza; o *peixe de olho de vidro* é o que faz a Nilza lembrar a receita (Ato VI).

**Ordem das falas (§13 Ato III):** Severo 1–2 (`ato3/severo`, chegada) → Benício 3 (`ato3/benicio`, exame C = a Brecha
observada da saída) → caçadas → item 4 (lanterna M.) → Severo 6 → Odete 7 (`ato3/odete`) → Guardião (5) → Benício 8
(selo B) → manhã: Nilza 9, Lis 10. Morte: `ato3/morte`.

---

## Ato IV — "As Perguntas do Vigia" (B → A)

**Hub — Cemitério das Lanternas** (issue #32, A19): colina de túmulos, cada um com lanterna acesa; capela no alto; o
banco do Vigia (só à noite). Depois do ato: **memorial com os nomes** dos caçadores caídos.

**Brecha — Cemitério das Lanternas (lado de baixo):** o cemitério como era antes do Ano Sem Sol, pequeno e de cabeça para
baixo — túmulos pendurados, velas que pingam para cima, nomes que o jogador conhece (Benício avisa: "Não para pra ler").
Salas: **Alameda dos Ciprestes** (combate) · **Ossuário das Velas** (arena de ondas, poças de cera que queimam) ·
**Capela Virada** (santuário) · **Fosso dos Nomes** (tesouro; na rota B, a **lanterna do Ravi** fica na Brecha B das
Ruas Baixas, sala 3) · **Arena — Altar das Mil Velas**.

**Monstros:**
- **Velinha** (B) — *cura* outros monstros e foge. Aviso: a chama cresce âmbar 0,8 s (um golpe interrompe). Parte: pavio → *Pavio Eterno*.
- **Carpideira** (B) — *lamenta*: grito em cone que empurra. Aviso: véu sobe, rosto vazio âmbar 0,7 s, cone no chão. Parte: véu → *Renda de Luto*.
- **Cão-de-Vigília** (B) — *investe* em linha após uivo (âmbar 0,6 s); bate em túmulo e fica tonto 1,5 s. Parte: coleira → *Coleira com Nome*.

**Guardiã — Velória** (colosso de cera, centenas de chamas):
1. **Vigília**: atira velas que viram Velinhas. Partes: **3 candelabros** (uma Velinha a menos por onda cada).
2. **Cera**: derrete em poça que queima e anda pela arena; ressurge onde a poça parar (círculo âmbar 0,8 s).
3. **Uma vela**: apaga a arena, restam 5 velas; fala única quando resta uma: *"Qual delas é você?"* (`ato4/velora`). A certa
   é a que não treme quando a lanterna do jogador passa.
- **Eco da Vela** (cura 6 s). **Conjunto Cera Velada**.

**Mecânica nova:** 1º slot de Eco, vendido e explicado pelo Vigia; **Ravi em perigo** (§8.1): três avisos, um por
véspera (`ato4/aviso_ruas`: Lis → Nilza → Ravi). Se o jogador não fechar a Brecha B das Ruas Baixas em 3 vésperas, ela
rompe (`ruas_rompeu`): Ravi entra e cai; ele **não morre**. Rota A/B em `ato4/ravi`. Na rota B o placar some.

**Vigia — sistema de perguntas** (§19): 5 perguntas no jogo inteiro, respostas em flags `vigia_p1..p5_*`:
p1 (IV, 1ª visita), p2 (IV, 2ª visita), p3 (fala de morte do Ato V), p4 (fala de morte do Ato VI), p5 (Ato VII). O Reflexo
pode citar as respostas.

---

## Ato V — "O Sino Sem Badalo" (A → S)

**Hub — Torre do Sino** (issue #33, A21): pátio de treino da Ordem, arquivo, a escada do campanário, o sino **sem badalo**.
**Brecha — Sino Mudo:** a Torre na noite do ritual, do lado de baixo: escada em espiral sem fim, cordas que sobem ao
escuro, papéis do arquivo flutuando. Salas: **Escada em Espiral** · **Arquivo** (encruzilhada) · **Sala das Cordas**
(santuário) · **Sala do Silêncio** (mecânica nova: sem som nenhum; só avisos visuais) · **Arena — O Campanário**.

**Monstros:** **Sineiro** (A) — *atordoa* com badalada em área (anel âmbar 0,8 s); partes: sino, 2 asas. **Casca** (A) —
*bloqueia*, imune de frente; abre o casco (âmbar) antes de investir; 3 placas. **Traça-de-Arquivo** (A) — *apaga*: enxame
que encolhe o raio da lanterna; junta numa bola âmbar 0,6 s antes de investir.

**Guardião — Sineiro Mudo** (gárgula do tamanho do sino, sem boca). Uma palavra em pedra por fase (`ato5/sineiro`):
1. **FESTA** — badaladas em área (anéis âmbar). Partes: 2 asas.
2. **CORDA** — voa e mergulha (sombra 0,8 s); cordas descem do teto e prendem quem fica embaixo.
3. **CULPA** — silêncio total: sem música, sem som de golpe; só avisos visuais.
- **Eco do Sino** (atordoa em volta, recarga longa). **Conjunto Bronze Mudo**. 2º slot de Eco.

**A cidade muda:** o sino aparece no HUD como relógio da véspera. Falas: `ato5/benicio` (1–3 antes, 5–8 depois),
Ravi A/B (9), Lis (10), Odete e Severo (perguntar mais), `ato5/morte`.

---

## Ato VI — "A Sopa da Véspera" (S → SS)

**Hub — Ruas Baixas** (issue #34, A23): palafitas, varais, a taverna da Nilza, o terreno das casas derrubadas para a
Torre; a casa da **mãe do Ravi** (issue #37) com tábuas na janela (tiradas depois do ato). Depois: lampiões novos.
**Brecha — Ruas Invertidas:** as Ruas Baixas de antes das demolições, com **casas penduradas do teto**. Salas: **Viela dos
Varais** · **Largo do Chafariz** (encruzilhada) · **Cozinha Velha** (santuário) · **Rua que Dá a Volta** (arena de ondas) ·
**Arena — O Terreno**.

**Monstros:** **Varal** (S) — *enlaça* quem passa embaixo (lençol infla, borda âmbar 0,6 s). **Despejado** (S) —
*arremessa* móveis (sombra 0,8 s); parte: carga nas costas. **Rasgo** (S) — *teleporta* para as costas do jogador (ponto de
chegada brilha 0,5 s); 2 bordas → *Fio do Avesso*.

**Guardião — Inquilino** (casa de madeira em 4 pernas, issue #38). Uma frase por fase, pela janela (`ato6/inquilino`):
1. *"Eu morava aqui."* — esmaga (sombra 0,8 s). Partes: 4 pernas (cada uma quebrada = mais lento).
2. *"Derrubaram pra Torre."* — atira móveis pela janela.
3. *"Entra."* — abre a porta e puxa o jogador para a **sub-arena** (a sala da casa); quebrar 3 vigas enquanto a casa pega fogo de fora para dentro.
- **Eco do Lar** (abrigo de 3 s). **Conjunto Madeira de Palafita**.

**Mecânica nova:** Sopa da Véspera (`ato6/nilza`): uma segunda vida por caçada. Odete forja a arma de **Duas Vésperas**
da classe (`ato6/odete`, evento `GiveLegendaryWeapon`). Celeste se revela (`ato6/celeste`).

---

## Ato VII — "A Cidade de Baixo" (SS → SSS)

**Hub:** Lumeira escurecendo às duas da tarde; a grande lanterna pisca. **Brecha:** abre em qualquer bairro e mistura
temas (Cais dentro do Pomar, Torre no Cemitério). Salas sorteadas dos baralhos anteriores + uma **sala final** com os seis
Guardiões ao fundo, onde fala **o Avesso** (`ato7/avesso`: Ficar = game over suave → `ato7/nilza_sonho`; Voltar = fecha).
**Guardiões em dupla:** Lodo-Mãe + Macieira; Farol + Velória; Sineiro + Inquilino — cada dupla com avisos encurtados (0,4 s)
e um ataque combinado (ex.: o feixe do Farol puxa para a poça de cera da Velória). **Exame SSS** = recusar a oferta do Avesso.
Monstros novos: **Ceifador de Pomar** (*vigia*: só se move quando não está no cone da lanterna) e **Sombra de Caçador**
(eco de caçador caído, usa o verbo de uma classe). Bênçãos combinadas de 3 famílias.
Falas: Lis 1 e 10 (com a escolha `lis_apresentada`/`lis_escondida`), Benício 2 (`{lanternas_perdidas}`) e 6 (escolha
`benicio_na_decisao`), Vigia 3 (pergunta cinco), Celeste 4, Odete 7, Ravi A/B 8, Severo 9 (entrega o **badalo**).

## Final — "A Primeira Brecha" (SSS → Alvor)

A Brecha da praça, do tamanho da praça; a grande lanterna apaga. Lá dentro, a praça de baixo com lampiões magenta e o sino
de baixo com badalo. **O Reflexo**: arma, bênçãos favoritas e ritmo de esquiva do jogador; uma lanterna acesa (buff) por
Lanterna perdida no jogo. Não tem fases: a cada minuto troca o padrão mais usado pelo jogador e fala uma frase
(`final/reflexo`, fichas `{lado_esquiva}`, `{familia_medo}`, `{lanternas_perdidas}`). Ao cair: `final/reflexo_queda`.
**O sino** (`final/sino`): Benício (se `benicio_na_decisao`; ele não volta), Lis (se `lis_escondida` e `lis_entrou`; as duas
Lumeiras ficam ligadas), o jogador (padrão; a primeira Brecha fecha para sempre; a cena Odete × Vigia não tem fala).
Epílogo: `final/epilogo` (ou `final/epilogo_sem_benicio`). Última fala: Lis, *"Abriu uma no mar."*

## Arte (issues `arte-v2`)

| Issue | Conteúdo |
|---|---|
| #29 | Ato III, cenários: hub + 4 salas + arena (6) |
| #30 | Ato III, monstros: Afogado, Lanterna-Afogada, Ancoreiro, Farol Afogado (21) |
| #31 | Ato III, Capitão Severo: retratos + caminhada (8) |
| #32 / #33 / #34 | Atos IV, V, VI: hub + arena (2 cada) |
| #36 | Ato IV, o Vigia: retratos + caminhada (8) |
| #37 | Ato VI, mãe do Ravi: retratos + caminhada (8) |
| #38 | Ato VI, Inquilino, a casa que anda (6) |

Gerador: `tools/art/prompts_ato3.py` → `docs/v2/prompts/ato3_*.md`, `ato4_*`, `ato5_*`, `ato6_*`. Referências novas fora do
LFS em `docs/v2/referencias/estilo_bairro_forjas.png`, `estilo_arena_esgoto.png`, `estilo_sala_esgoto.png` (links apontam
para o branch `campanha`; trocar `REPO_CAMPANHA` para `v2` depois do merge).

**Falta para os Atos IV–VII:** salas (4 por Brecha), monstros comuns e Guardiões Velória/Sineiro Mudo em tiras;
retratos de Celeste já existem (#28).
