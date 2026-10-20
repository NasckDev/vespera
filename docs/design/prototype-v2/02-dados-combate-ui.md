# Véspera v2 — Dados, Núcleo, Combate e UI (spec de reimplementação)

> Fonte: protótipo HTML/JS em `scratchpad/v2/v2/`. Escopo principal: `data.js`, `core.js`, `combat.js`, `ui.js`.
> Como `extra.js` (carregado por último) **sobrescreve** funções desses quatro arquivos, e o HUD/lojas/forja moram em `render.js`/`world.js`, essas partes foram lidas e documentadas aqui quando afetam o comportamento final. Citações no formato `arquivo:linha`.
> Ordem de carga (`Vespera v2.dc.html`): data → core → combat → world → render → ui → main → extra. Em caso de conflito, **vale a última definição** (marcada como "efetivo").
> Unidades: distâncias em pixels do mundo (mapa), tempo em segundos, velocidades em px/s, ângulos em radianos. `R(a,b)` = uniforme real em [a,b); `RI(a,b)` = inteiro uniforme em [a,b] (`core.js:11-12`).

---

## 0. Convenções numéricas que a porta precisa respeitar

- **Arredondamento JS**: `Math.round(x)` = `floor(x+0.5)` (meia-unidade para cima). Em C#, `Math.Round` usa *banker's rounding* por padrão — usar `Math.Round(x, MidpointRounding.AwayFromZero)` (equivalente para positivos) ou `Math.Floor(x+0.5)`. Exemplo que quebra: preço 30 Lumes em invasão → `30*1.35 = 40.5` → JS **41**, C# padrão **40**.
- **RNG**: não há seed; tudo usa `Math.random()` (`core.js:11-13`). Na porta, injetar `IRandom` para testes determinísticos.
- **Delta time**: `dt = min(0.05, Δ)` (`main.js:18`). Hitstop: enquanto `G.hitstop>0`, jogador/monstros/projéteis/fx/picks recebem `dt*0.08` (`main.js:104-108`); NPCs/caçadores/mundo usam `dt` real.
- `G.st = stats()` é recalculado **todo frame** (`main.js:80`).

---

## 1. Tabelas de dados (`data.js`)

### 1.1 Paleta (`COL`, `data.js:3`) e cores de luz (`LIGHTC`, `data.js:4`)

| chave | hex | uso |
|---|---|---|
| indigo | `#1B1F3B` | gradientes de painel |
| roxo | `#4A2C6B` | seleção, partículas |
| ambar | `#F2A541` | destaque principal, seleção, texto de nome |
| brecha | `#E64A8E` | magenta das Brechas, perigo, telegraphs |
| creme | `#F4E7C6` | texto padrão |
| ink | `#0d0b16` | fundo |
| panel | `#120f1d` | painéis |
| gold | `#c9963e` | cantos/losangos de painel |
| goldD | `#5e4426` | bordas escuras |
| mute | `#9d93a8` | texto apagado |

`LIGHTC` (RGB): `a`=[242,165,65] âmbar, `f`=[255,120,40] fogo, `m`=[230,74,142] magenta, `c`=[95,208,230] ciano, `g`=[110,230,90] verde. Extra: `w`=[255,240,220] (`core.js:35`).

### 1.2 Ranks / Selos (`RANKS`, `data.js:100-104`)

| idx | id | XP mínimo | título | cor | HP de rank (`+12*idx`) |
|---|---|---|---|---|---|
| 0 | E | 0 | Aprendiz | `#a39fae` | +0 |
| 1 | D | 100 | Vigia | `#7cc47f` | +12 |
| 2 | C | 260 | Lanterneiro | `#6b9be6` | +24 |
| 3 | B | 520 | Guardião | `#b07ce6` | +36 |
| 4 | A | 900 | Caçador | `#e6ad4a` | +48 |
| 5 | S | 1500 | Sentinela | `#e0644f` | +60 |
| 6 | SS | 2400 | Arauto | `#dc4e66` | +72 |
| 7 | SSS | 3600 | Crepuscular | `#c8418f` | +84 |
| 8 | SSSS | 5200 | Véspero | `#E64A8E` | +96 |
| 9 | ALVOR | 7500 | Alvor | `#F2A541` | +108 |

- `rankIdx(xp)` = maior i com `xp >= RANKS[i].xp` (`core.js:221`).
- O bônus de HP **não está em data.js**; vem da fórmula `stats()` (`core.js:226`, ver §2.4).
- Ícones: `r_<id>` no atlas (ex.: `r_E`, `r_ALVOR`). Visual do personagem por rank (aba Selos): `looks = [look0,look0,look1,look2,look2,look3,look3,look4,look4,look4]` por idx (`ui.js:228`); tiers exibidos: `E–D/look0, C/look1, B–A/look2, S–SS/look3, SSS+/look4` (`ui.js:229`).

### 1.3 Nível (Nv.) — curva separada do Selo (`lvlOf`, `core.js:223`)

`need` começa em 40; a cada nível `need = round(need*1.18)`. O nível dá **+6 HP por nível acima de 1** (`core.js:226`). Tabela calculada:

| Nv | XP acumulado p/ atingir | XP para o próximo |
|---|---|---|
| 1 | 0 | 40 |
| 2 | 40 | 47 |
| 3 | 87 | 55 |
| 4 | 142 | 65 |
| 5 | 207 | 77 |
| 6 | 284 | 91 |
| 7 | 375 | 107 |
| 8 | 482 | 126 |
| 9 | 608 | 149 |
| 10 | 757 | 176 |
| 11 | 933 | 208 |
| 12 | 1141 | 245 |
| 13 | 1386 | 289 |
| 14 | 1675 | 341 |
| 15 | 2016 | 402 |
| 16 | 2418 | 474 |
| 17 | 2892 | 559 |
| 18 | 3451 | 660 |
| 19 | 4111 | 779 |
| 20 | 4890 | 919 |
| 21 | 5809 | 1084 |
| 22 | 6893 | 1279 |

HP base (sem equipamento) exatamente no limiar de cada Selo: E=100 (Nv1), D=124 (Nv3), C=148 (Nv5), B=178 (Nv8), A=202 (Nv10), S=232 (Nv13), SS=256 (Nv15), SSS=286 (Nv18), SSSS=310 (Nv20), ALVOR=334 (Nv22).

### 1.4 Estações (`SEASONS`, `SEASON_MOD`, `data.js:106, 114-116`)

`SEASONS = ['Primavera','Verão','Outono','Inverno']`. `seasonOf(day) = SEASONS[(2 + floor((day-1)/3)) % 4]` (`core.js:224`) → dias 1–3 Outono, 4–6 Inverno, 7–9 Primavera, 10–12 Verão, ciclo de 12 dias.

| estação | texto (`SEASON_MOD`) | efeito implementado |
|---|---|---|
| Primavera | "Baús extras escondidos na Brecha." | +1 baú (`world.js:94`) |
| Verão | "Monstros ardentes: +15% de dano." | dano de monstro ×1.15 em Brecha (`combat.js:246`) |
| Outono | "Espectros a mais em cada onda." | **não implementado** no gameplay; só tint visual (`render.js:52`) |
| Inverno | "Água congelada; golpes inimigos gelam." | ao ser ferido: `chill=1.6s` (vel. ×0.65) (`combat.js:20`); zonas `fast` ignoradas (`combat.js:88`) |

### 1.5 Temas de Brecha (`THEMES`, `data.js:107-113`)

| tema | bairro | mapa | chefe | minRk | mobs (tipo, peso) | extra | elite | props | pmat | stone |
|---|---|---|---|---|---|---|---|---|---|---|
| fish | Mercado de Peixes | fish2 | angler | 0 | fishm .55, wolf .45 | fishm | wolfE | barrel, crate, crate2, sack, barrel | madeira | 0 |
| crypt | Cemitério das Velas | crypt2 | reaper | 1 | cnev .4, coss .35, cvel .25 | cvel | cosr | urn, urn, skull, bones, candles, tomb | cera | 1 |
| mines | Minas da Forja | mine | colosso | 2 | mbes .4, maut .3, mesc .3 | mesc | msen | barrel, crate, crate2, barrel | parafuso | 0 |
| garden | Estufa Silvestre | garden | rainha | 3 | gbro .4, gsei .3, gmus .3 | gbro | gflo | urn, urn, candles, bones | semente | 1 |
| cathedral | Catedral do Arauto | cath | knight | 4 | kesc .35, ksin .3, ksom .35 | ksom | kcav | candles, urn, pillar, candles | cinza | 1 |

Títulos por estação (`titles`):

| tema | Primavera | Verão | Outono | Inverno |
|---|---|---|---|---|
| fish | Maré Florida | Mercado Fervente | Cais Enevoado | Maré Congelada |
| crypt | Jardim dos Ossos | Cripta Abafada | Vigília dos Espectros | Túmulos de Geada |
| mines | Veio Desperto | Fornalha Aberta | Galeria de Cinzas | Túnel Gelado |
| garden | Floração Voraz | Estufa Sufocante | Folhas Caídas | Raízes Adormecidas |
| cathedral | Vitrais em Flor | Nave Ardente | Litania das Sombras | Sinos de Geada |

`pmat` = material que cai de props quebráveis do tema; `stone` = cor de partículas de pedra vs madeira (`combat.js:516`). Ondas/rank da Brecha são gerados em `world.js` (resumo no §3.14).

### 1.6 Armas (`WORDER`, `WEAP`, `UPG`, `data.js:118-131`)

Ordem de hotbar: `sword, bow, staff, hammer, dag` (teclas 1–5).

| id | nome | short | tipo | ícone | dmg | cd (s) | kind | arc (rad) | range (px) | part (mult. dano em partes) | extra | cor | desbloqueio | material de upgrade (`UPG`) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| sword | Lâmina da Lumeira | Lâmina | Espada | i_sword | 12 | .32 | melee | 2.1 | 30 | 1 | combo 3 golpes | `#E64A8E` | 50 Lumes + 2 estilhaco | presa |
| bow | Arco do Cais | Arco | Arco | i_bow | 10 | .40 | bow | — | — | .9 | carregar | `#F2A541` | 50 Lumes + 2 estilhaco | pavio |
| staff | Cajado Fendido | Cajado | Cajado | i_staff | 16 | .72 | orb | — | — | 1 | orbe teleguiado | `#c06cf0` | 60 Lumes + 4 estilhaco | estilhaco |
| hammer | Martelo de Brás | Martelo | Martelo | i_hammer | 30 | .95 | smash | 2.9 | 36 | 2.2 | windup | `#F2A541` | 80 Lumes + 3 minerio | minerio |
| dag | Adagas Gêmeas | Adagas | Adagas | i_dag | 6 | .15 | melee | 1.4 | 24 | .7 | `backstab:2` | `#E64A8E` | 60 Lumes + 2 presa | osso |

Descrições (verbatim): sword "Equilibrada. Combo de três golpes; o último corta mais fundo."; bow "Segure para tensionar. Flechas carregadas atravessam inimigos."; staff "Orbes que buscam o alvo e explodem em área."; hammer "Lento e brutal. Quebra partes com o dobro de força."; dag "Rápidas. Golpes pelas costas causam dano dobrado."

Dano base por nível (`round(dmg*(1+0.22*(lv-1)))`, sem bônus de equipamento; máx. nível 5, `world.js:235`):

| arma | +1 | +2 | +3 | +4 | +5 |
|---|---|---|---|---|---|
| sword | 12 | 15 | 17 | 20 | 23 |
| bow | 10 | 12 | 14 | 17 | 19 |
| staff | 16 | 20 | 23 | 27 | 30 |
| hammer | 30 | 37 | 43 | 50 | 56 |
| dag | 6 | 7 | 9 | 10 | 11 |

Custo de upgrade lv→lv+1 (`world.js:236`): `lumes = 40*lv`, material `UPG[w]` × `lv` (cajado: × `2*lv`). Tabela: +1→+2: 40 L + 1 mat (cajado 2); +2→+3: 80 L + 2 (4); +3→+4: 120 L + 3 (6); +4→+5: 160 L + 4 (8). Lumes passam por `costLumes` (×0.9; ver §2.5).

Elementos por arma (`WELEM`, `data.js:95`): sword = solar, lunar, igneo, mare, silvestre, vazio; bow = solar, lunar, igneo, mare; staff/hammer/dag = solar, lunar, igneo, vazio. Ícone com elemento: `el_<arma>_<índice em WELEM>` (`core.js:217`).

**Inconsistência**: o diálogo do Brás diz "Lâmina e Adagas pedem Presas; Arco, Vidro-lume" (`world.js:239`), mas `UPG.dag='osso'` e `UPG.bow='pavio'` (`data.js:131`). O código usa `UPG`.

### 1.7 Elementos (`ELEM`, `ELEM_COST`, `data.js:94,96`)

| id | nome | cor | custo de infusão (Gaspar) |
|---|---|---|---|
| solar | Solar | `#F2C541` | 80 L + 3 estilhaco + 2 erva |
| lunar | Lunar | `#86a2ff` | 80 L + 1 frasco + 1 escama |
| igneo | Ígneo | `#ff6a2a` | 80 L + 3 brasa |
| mare | Maré | `#3fd6e0` | 80 L + 2 escama + 1 isca |
| silvestre | Silvestre | `#6fd36a` | 80 L + 3 erva + 1 nucleo |
| vazio | Vazio | `#c04cf0` | 100 L + 5 estilhaco + 1 carapaca |

Infusões compradas ficam em `s.elems[arma][]`; a ativa em `s.elem[arma]`. Só uma ativa por arma (`world.js:282-290`).

### 1.8 Materiais (`MAT`, `data.js:133-182`)

`val` = Lumes na venda de sobras (padrão 3 se ausente, `ui.js:101`, `world.js:319`). `rar` ausente → comum.

| id | nome | ícone | rar | val | descrição (verbatim) |
|---|---|---|---|---|---|
| estilhaco | Estilhaço de Brecha | i_shard | — | (3; vende a 4 no Brás) | Fragmento cristalizado de uma Brecha. Toda forja de Lumeira compra. |
| presa | Presa Rubra | m_fang | — | 3 | Arrancada da mandíbula de um Lobo da Brecha. Só cai se você quebrar a mandíbula. |
| minerio | Minério Rúnico | i_ore | — | 3 | Cresce no dorso rachado dos lobos. Quebre o dorso, não a cabeça. |
| vidro | Vidro-lume | m_glass | — | 3 | O vidro de uma lanterna-espectro. Ainda guarda um pouco de calor. |
| isca | Isca Abissal | m_lure | — | 3 | A luz que o Abissal usa para encantar. Quebrá-la é quebrar o feitiço. |
| escama | Escama Abissal | i_fish | — | 3 | Das barbatanas do Abissal. Dura como cais velho. |
| tecido | Tecido do Ceifador | m_hood | — | 3 | Retalho do capuz. Sem ele, o Ceifador não some mais nas sombras. |
| corrente | Corrente Votiva | m_chain | — | 3 | Elos que prendiam a lanterna do Ceifador. |
| madeira | Madeira de Cais | i_logs | — | 3 | Tábuas salvas da água. Boas para cabos e arcos. |
| erva | Erva-de-Véspera | i_leaf | — | 3 | Floresce só no fim da tarde, inclusive no dorso dos golems. Ivo destila em poções. |
| brasa | Brasa Viva | m_brasa | — | 3 | Arrancada das mandíbulas de um Besouro de Brasa. Nunca esfria por completo. |
| carapaca | Carapaça de Brasa | d_carapaca | comum | 4 | A casca do besouro. Só cai se você acertar pelas costas. |
| nucleo | Núcleo de Musgo | d_nucleo | incomum | 10 | O cristal que dá vida ao Golem de Musgo. |
| halo | Fio de Halo | d_halo | comum | 4 | Um fio dourado do halo de um Escudeiro. |
| estandarte | Retalho de Estandarte | i_banner | — | 3 | Do manto do Arauto. O brasão é de uma Casa que ninguém lembra. |
| cera | Cera Penitente | d_cera | comum | 4 | Derretida de uma Vela Viva. Ainda morna. |
| osso | Osso Espectral | d_osso | comum | 4 | Leve demais para ser só osso. |
| pavio | Pavio Vivo | d_pavio | comum | 4 | Se mexe sozinho quando ninguém olha. |
| frasco | Frasco de Névoa | d_frasco | incomum | 10 | A névoa dentro dele sussurra nomes. |
| medalha | Medalha Fúnebre | d_medalha | incomum | 10 | Gravada com um nome que a chuva apagou. |
| lapide | Fragmento de Lápide | d_lapide | comum | 4 | Pedra de túmulo rachada pela Brecha. |
| cinza | Cinza Consagrada | d_cinza | comum | 4 | Restos de vela de vigília. Abel usa em relíquias. |
| lamento | Essência de Lamento | d_lamento | raro | 25 | O que sobra do Ceifador quando a corrente quebra. |
| mascara | Máscara do Ceifador | d_mascara | incomum | 10 | Por baixo dela não havia rosto. |
| parafuso | Parafuso Antigo | d_parafuso | comum | 4 | Tirado das juntas de um Autômato. |
| escoria | Escória Endurecida | d_escoria | comum | 4 | Esfriou o bastante para segurar. Quase. |
| fornalha | Núcleo de Fornalha | d_fornalha | incomum | 10 | O coração de uma Sentinela. Pulsa devagar. |
| engrenagem | Engrenagem Queimada | d_engrenagem | comum | 4 | Gira mesmo solta. |
| ferro | Minério de Ferro Negro | d_ferro | comum | 4 | O ferro das minas profundas. Brás paga bem. |
| pepita | Pepita Rubra | d_pepita | incomum | 10 | Brilha como brasa, mas é pedra. |
| forja | Essência de Forja | d_forja | raro | 25 | O fogo que movia o Colosso. |
| placa | Placa Colossal | d_placa | raro | 25 | Uma lasca da armadura do Colosso. |
| semente | Semente Antiga | d_semente | comum | 4 | Germina em qualquer lugar onde houver Brecha. |
| seiva | Seiva Rara | d_seiva | comum | 4 | Doce, pegajosa e levemente venenosa. |
| vinha | Vinha Tensa | d_vinha | comum | 4 | Ainda tenta se enrolar no seu pulso. |
| casulo | Casulo Verde | d_casulo | incomum | 10 | Algo dentro dele está esperando. |
| polen | Pólen Luminoso | d_polen | comum | 4 | Brilha no escuro como pó de estrela. |
| espinho | Folha de Espinho | d_espinho | comum | 4 | Corta como lâmina. |
| essil | Essência Silvestre | d_essil | raro | 25 | A seiva da Rainha das Raízes. |
| petala | Pétala Régia | d_petala | raro | 25 | Da coroa de uma flor que comia gente. |
| vitral | Fragmento de Vitral | d_vitral | comum | 4 | Ainda projeta cores quando pega luz. |
| sino | Estilhaço de Sino | d_sino | comum | 4 | Toca sozinho quando alguém morre. |
| veu | Véu Sombrio | d_veu | incomum | 10 | Tecido feito de sombra de vitral. |
| insignia | Insígnia Consagrada | d_insignia | incomum | 10 | O selo dos cavaleiros do Nártex. |
| nucleolp | Núcleo da Lanterna Partida | d_nucleolp | raro | 25 | A chama que o Arauto carregava. |
| rosario | Rosário Partido | d_rosario | comum | 4 | Contas que ninguém mais reza. |
| catedral | Essência de Catedral | d_catedral | raro | 25 | Luz de vitral destilada. |
| brasao | Brasão do Arauto | d_brasao | raro | 25 | O brasão de uma Casa esquecida. |

Materiais definidos mas **sem fonte de drop no v2**: vidro, tecido, corrente, estandarte, brasa (nenhum `parts[].drop` os referencia). `brasa` é exigida pela infusão Ígnea → infusão Ígnea é inobtenível no v2. `frasco` só cai de cnev; `isca`/`escama` só do fish.

"Materiais em uso" (não vendíveis como sobra) = estilhaco + todos `UPG` + mats de `WEAP.unlock` + `RECIPES` + `ELEM_COST` + `RELSHOP` (`world.js:317-318`).

### 1.9 Equipamento (`GEAR`, `SLOTS`, `RAR`, `data.js:184-212`)

Slots (`SLOTS`): arma/Arma, cabeca/Cabeça, peito/Peito, lanterna/Lanterna, maos/Mãos, capa/Capa, botas/Botas, reliquia/Relíquia.
Raridades (`RAR`): comum "Comum" `#b9b0a2`; incomum "Incomum" `#7cc47f`; raro "Raro" `#E64A8E`; epico "Épico" `#F2A541`.

Atributos: `hp` (+vida), `def` (+defesa), `atk` (+ataque plano), `crit` (+% crítico), `spd` (+fração de velocidade), `light` (multiplicador de luz, **substitui**, não soma), `ess` (+fração de ganho de Essência), `regen` (HP/s).

| id | slot | nome | ícone | hp | def | atk | crit | spd | light | ess | regen | rar | classe (`extra.js:75-76`) | Selo mín. (por raridade) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| capuz | cabeca | Capuz do Vigia | eq_hood | 10 | 2 | | | | | | | comum | todas | E |
| couraca | peito | Couraça de Cais | eq_armor0 | | 6 | | | | | | | comum | todas | E |
| gibao | peito | Gibão do Selo | eq_tunic | | 5 | | 4 | | | | | incomum | dag, bow | D |
| couraca2 | peito | Couraça Rubra | eq_armor1 | 20 | 12 | | | | | | | raro | sword, hammer | C |
| capa | capa | Capa da Véspera | eq_cloak0 | | 2 | | | .05 | | | | incomum | todas | D |
| capa2 | capa | Manto do Abismo | eq_cloak1 | | | | 6 | .06 | | | | raro | dag, bow | C |
| luva | maos | Manoplas de Ferro | eq_glove0 | | | 3 | | | | | | comum | todas | E |
| luva2 | maos | Manoplas Rúnicas | eq_glove1 | | | 6 | 3 | | | | | raro | sword, hammer, dag | C |
| bota | botas | Botas de Couro | eq_boot1 | | | | | .04 | | | | comum | todas | E |
| bota2 | botas | Grevas do Alvor | eq_boot3 | | 4 | | | .08 | | | | raro | todas | C |
| lant0 | lanterna | Lanterna de Herança | eq_lant0 | | | | | | 1 | | | comum | todas | E |
| lant1 | lanterna | Lanterna do Cais | eq_lant1 | 10 | | | | | 1.2 | | | incomum | todas | D |
| lant3 | lanterna | Lanterna Fendida | eq_lant3 | | | 4 | | | 1.35 | | | raro | todas | C |
| livro | reliquia | Diário da Brecha | eq_book1 | | | | | | | .35 | | incomum | staff, bow | D |
| escudo | reliquia | Brasão da Casa | eq_shield1 | | 6 | | | | | | | incomum | sword, hammer | D |
| sh_solar | reliquia | Égide Solar | sh_solar | 15 | 10 | | | | | | | raro | sword, hammer | C |
| sh_lunar | reliquia | Escudo Lunar | sh_lunar | | 7 | | | | | .25 | | incomum | sword, hammer | D |
| sh_vazio | reliquia | Bastião do Vazio | sh_vazio | | 12 | | 3 | | | | | raro | sword, hammer | C |
| ln_solar | lanterna | Lanterna Solar | ln_solar | 15 | | | | | 1.45 | | | raro | todas | C |
| ln_lunar | lanterna | Lanterna Lunar | ln_lunar | | | | | | 1.3 | .3 | | incomum | staff, bow | D |
| ln_vazio | lanterna | Lanterna do Vazio | ln_vazio | | | 6 | 4 | | 1.2 | | | raro | todas | C |
| rl_astro | reliquia | Astrolábio do Alvor | rl_astro | | | | | .04 | | .4 | | raro | staff, bow | C |
| rl_vazio | reliquia | Cristal do Vazio | rl_vazio | | | | 9 | | | | | raro | dag, bow | C |
| rl_concha | reliquia | Concha Perolada | rl_concha | 25 | | | | | | | 1.5 | incomum | todas | D |
| rl_silva | reliquia | Lanterna Silvestre | rl_silva | 15 | | | | | | | 2.5 | raro | todas | C |

Descrições (verbatim): capuz "Lã grossa, cheiro de chuva."; couraca "Remendada mais vezes do que é possível contar."; gibao "Bordado com o selo da Guilda."; couraca2 "Forjada com escamas abissais. Pesa como uma promessa."; capa "Tingida na cor do último sol."; capa2 "Costurado com tecido de Ceifador."; luva "Simples. Fazem o trabalho."; luva2 "As runas acendem quando você acerta."; bota "Já andaram por toda Lumeira."; bota2 "Dizem que quem as calça nunca chega tarde."; lant0 "Era da sua mãe. A chama nunca apagou de verdade."; lant1 "Vê mais longe na névoa."; lant3 "Uma fenda magenta corre pelo vidro. Ilumina o que se esconde."; livro "Anotações de um caçador que não voltou. Ecos carregam mais rápido."; escudo "O brasão de uma Casa que já protegeu este bairro."; sh_solar "Reflete a última luz do dia."; sh_lunar "Fica mais leve quando a lua sobe."; sh_vazio "O olho no centro não pisca."; ln_solar "Guarda um pedaço de pôr do sol. Ilumina longe."; ln_lunar "Luz fria, que acalma os Ecos."; ln_vazio "Ilumina pouco. Mostra muito."; rl_astro "Aponta sempre para onde o sol vai nascer."; rl_vazio "Frio ao toque. Seus golpes encontram as rachaduras."; rl_concha "Encostada no ouvido, ainda se ouve o mercado."; rl_silva "Uma raiz cresceu em volta dela. Cura devagar."

Regras de bloqueio (`extra.js:77-83`): Selo mínimo = `g.rk` se existir (nenhum item define `rk`), senão `RAR_RK = {comum:0, incomum:1, raro:2, epico:3}`. Classe = `GEAR_CLS[id]` (ausente = todas). Textos de bloqueio: `'Exclusivo de '+[classes].join(' / ')+'. Sua classe é '+X+'.'` e `'Requer Selo '+id+' ('+t+'). Seu Selo é '+id+'.'`. `CLS_NAME = {sword:'Lâmina', bow:'Arco', staff:'Cajado', hammer:'Martelo', dag:'Adagas'}` (`extra.js:5`).

### 1.10 Receitas da forja do Brás (`RECIPES`, `data.js:214-220`)

| item | Lumes (antes de ×0.9) | materiais |
|---|---|---|
| luva2 Manoplas Rúnicas | 60 | 4 minerio, 2 presa |
| couraca2 Couraça Rubra | 90 | 3 escama, 3 minerio |
| lant3 Lanterna Fendida | 100 | 2 frasco, 1 isca |
| capa2 Manto do Abismo | 90 | 1 mascara, 3 cinza |
| bota2 Grevas do Alvor | 120 | 2 engrenagem, 2 ferro |

Forjar adiciona o item a `s.gear` (não equipa) (`world.js:252`). Pode-se forjar repetidamente.

### 1.11 Loja de relíquias do Abel (`RELSHOP`, `data.js:97`)

| item | Lumes | materiais |
|---|---|---|
| ln_lunar Lanterna Lunar | 110 | — |
| ln_solar Lanterna Solar | 150 | 1 halo |
| ln_vazio Lanterna do Vazio | 150 | 2 carapaca |
| sh_lunar Escudo Lunar | 90 | — |
| sh_solar Égide Solar | 140 | 1 insignia |
| sh_vazio Bastião do Vazio | 150 | 2 nucleo |
| rl_concha Concha Perolada | 90 | 2 escama |
| rl_silva Lanterna Silvestre | 120 | 4 erva, 1 nucleo |
| rl_astro Astrolábio do Alvor | 160 | 1 halo, 2 vitral |
| rl_vazio Cristal do Vazio | 150 | 1 lamento |

Item já possuído (no `gear` ou equipado) → desabilitado "Você já tem" (`world.js:311-312`).

### 1.12 Outras lojas/serviços (preços em `world.js`; todos os Lumes passam por `costLumes`)

| NPC / local | serviço | custo base | efeito | ref |
|---|---|---|---|---|
| Lúcia (Estalagem) | Comprar Poção | 30 L | +1 poção | `world.js:259` |
| Lúcia / cama | Descansar até a próxima véspera | 10 L | novo dia, HP cheio, energia 100, salva | `world.js:260,264-268` |
| Ivo (Alquimia) | Poção | 22 L | +1 | `world.js:304` |
| Ivo | Caixa com 3 Poções | 60 L | +3 | `world.js:305` |
| Ivo | Destilar Erva-de-Véspera | 6 L + 2 erva | +2 poções | `world.js:306` |
| Vesna (Feitiços) | Afinar Eco (nível lv 0→1, 1→2) | `70+60*lv` L + `4+2*lv` estilhaco | custo do Eco −10 (máx. 2 afinações) | `world.js:295` |
| Vesna | Comprar Diário da Brecha (livro) | 90 L | +livro (se não possuir) | `world.js:296` |
| Gaspar (Armaria) | Infusão elemental | `ELEM_COST` | ver §1.7 | `world.js:282-290` |
| Abel (Relíquias) | `RELSHOP` | ver §1.11 | | `world.js:309-315` |
| Brás (Forja) | Melhorar arma | `40*lv` L + mat | ver §1.6 | `world.js:232-240` |
| Brás | Forjar nova arma | `WEAP.unlock` | **efetivo: bloqueado por classe** (`extra.js:94`) | |
| Brás | Forjar equipamento | `RECIPES` | | `world.js:249-255` |
| Brás | Vender sobras de Brecha | — | +Σ(qtd×val) para mats fora de uso | `world.js:228,319` |
| Brás | Vender Estilhaços | — | +4 L por estilhaço (não passa por priceMult) | `world.js:229` |
| Odete | Pedir resgate da Lanterna | 40 L | `rescueAt = t+12` | `world.js:210` |
| Odete | Entregar lanternas encontradas | — | +35 L e +20 XP por lanterna | `world.js:207-209` |
| Inventário | Desmontar equipamento | — | +2 estilhaco | `ui.js:100` |

Durante invasão, Brás recusa atender ("Agora não! Tem monstro na minha rua. Limpa isso e eu reabro a forja.", `world.js:224`).

### 1.13 Monstros (`MON`, `data.js:222-271`)

Campos: `spr` (prefixo de sprite), `s` (escala visual, padrão 1), `hp`, `spd` (px/s), `dmg`, `xp`, `r` (raio de colisão/alvo), `hh` (altura do "centro de acerto"; ausente → `round(alturaSprite(spr+'_f')*s*0.8)` ou 20, `combat.js:247`), `ai`, `elite`, `boss`, `fly` (ignora blocos), `eco`/`ecoCh` (Eco que pode dropar / chance), `summon`, `bolt` (cor do projétil), `weak`/`res` (elementos), `glow` ([cor LIGHTC, raio]), `triple`, `orbs`, `col`, `ph2` ([título, subtítulo] do toast de fase 2).

#### Monstros comuns e elites

| id | nome | spr | s | hp | spd | dmg | xp | r | hh | ai | flags | weak | res | glow | bolt |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| wolf | Lobo da Brecha | wolf | 1 | 40 | 58 | 12 | 12 | 10 | 16 | lunger | | solar, igneo | | | |
| wolfE | Alfa da Brecha | wolf | 1.4 | 130 | 62 | 18 | 45 | 14 | 22 | lunger | elite; eco uivo .25 | solar, igneo | | | |
| fishm | Pescador Abissal | fishm | 1 | 58 | 44 | 14 | 14 | 11 | 18 | lunger | | lunar, silvestre | mare | a,28 | |
| cnev | Espectro da Névoa | cnev | 1 | 30 | 44 | 11 | 12 | 10 | — | caster | fly | solar | vazio | a,32 | `#8fb8ff` |
| coss | Ossinho Andante | coss | 1 | 38 | 56 | 12 | 12 | 10 | — | lunger | | solar, igneo | | | |
| cvel | Vela Viva | cvel | 1 | 34 | 34 | 11 | 12 | 10 | — | spitter | | mare | igneo | a,36 | `#ffb040` |
| cosr | Ossário Vigia | cosr | 1 | 160 | 30 | 20 | 45 | 16 | — | brute | elite | solar | | a,44 | |
| mbes | Besouro de Brasa | mbes | 1 | 50 | 36 | 13 | 14 | 11 | — | spitter | | mare, lunar | igneo | f,30 | `#ff7a2a` |
| maut | Autômato Ferrugem | maut | 1 | 72 | 34 | 16 | 16 | 11 | — | brute | | mare | | | |
| mesc | Escória Viva | mesc | 1 | 44 | 50 | 13 | 13 | 11 | — | lunger | | mare | igneo | f,34 | |
| msen | Sentinela da Bigorna | msen | 1 | 200 | 28 | 24 | 55 | 16 | — | brute | elite | mare | igneo | f,50 | |
| gbro | Broto Mordaz | gbro | 1 | 40 | 62 | 14 | 14 | 10 | — | lunger | | igneo | silvestre | | |
| gsei | Besouro de Seiva | gsei | 1 | 60 | 40 | 14 | 15 | 11 | — | spitter | | igneo | | | `#ffa21a` |
| gmus | Golem de Musgo | gmus | 1 | 120 | 28 | 20 | 22 | 14 | — | brute | | igneo | silvestre | g,26 | |
| gflo | Flor Carnívora Régia | gflo | 1 | 210 | 22 | 18 | 55 | 17 | — | spitter | elite, triple | igneo | silvestre | | `#ff6aa8` |
| kesc | Escudeiro do Halo | kesc | 1 | 70 | 50 | 16 | 16 | 11 | — | lunger | | lunar, vazio | solar | a,26 | |
| ksin | Sino Vivo | ksin | 1 | 62 | 30 | 14 | 16 | 11 | — | caster | | lunar | | m,32 | `#b06cff` |
| ksom | Sombra de Vitral | ksom | 1 | 54 | 62 | 15 | 16 | 10 | — | lunger | | solar | vazio | m,28 | |
| kcav | Cavaleiro do Nártex | kcav | 1 | 240 | 54 | 24 | 60 | 15 | — | lunger | elite | lunar, vazio | solar | a,40 | |

#### Chefes

| id | nome | spr | hp | spd | dmg | xp | r | hh | ai | eco (chance base) | summon | fly | weak | res | glow | bolt | col | ph2 (toast) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| angler | O Abissal do Mercado | anglerB | 520 | 34 | 20 | 120 | 24 | 30 | angler | abissal .45 | fishm | | lunar, silvestre | mare | a,60 | | | — (toast próprio "O Abissal chama a maré" / "Pescadores surgem da água.") |
| reaper | O Ceifador | cceif | 480 | 40 | 18 | 120 | 22 | — | reaper | ceifador .45 | coss | sim | solar | vazio | m,64 | `#8fb8ff` | | — (toast "O Ceifador chama os seus" / "Ossos se levantam da névoa.") |
| colosso | Colosso de Ferro | mcol | 820 | 30 | 24 | 150 | 26 | — | knight | — | mesc | | mare | igneo | f,95 | `#ff7a2a` | `#ff7a2a` | "O Colosso abre a fornalha" / "Escória viva escorre pelas placas." |
| rainha | Rainha das Raízes | grai | 900 | 30 | 24 | 160 | 26 | — | knight | — | gbro | | igneo | silvestre | g,80 | `#ff6aa8` | `#6fd36a` | "A Rainha abre as vinhas" / "Brotos mordazes rompem o chão." |
| knight | O Arauto da Lanterna Partida | karr | 950 | 30 | 24 | 160 | 24 | — | knight | arauto .6 | ksom (não usado: `orbs:1`) | | lunar, vazio | solar | a,90 | `#5fe0d0` | `#E64A8E` | "O Arauto ergue as lanternas" / "A segunda chama acendeu. Ele ficou mais rápido." |

Chefe com `eco`: chance efetiva de drop no kill é **0.75** se o jogador ainda não tem aquele Eco, 0 se já tem (`combat.js:204`); `ecoCh` só vale para não-chefes (wolfE .25).

#### Partes quebráveis (`parts`)

`side`: `front` (atingida de frente), `back` (pelas costas), `any`. `n` = quantidade de drops (padrão 1). HP da parte escala com o rank da Brecha (×`hm`, §3.9).

| monstro | parte 1 (id · nome · side · hp · drop×n) | parte 2 | parte 3 |
|---|---|---|---|
| wolf | jaw · Mandíbula · front · 16 · presa×1 | back · Dorso Rachado · back · 18 · minerio×1 | |
| wolfE | jaw · Mandíbula · front · 40 · presa×2 | back · Dorso Rachado · back · 45 · minerio×2 | |
| fishm | lure · Isca · front · 20 · isca×1 | fins · Barbatanas · back · 24 · escama×1 | |
| angler | lure · Isca · front · 110 · isca×2 | fins · Barbatanas · back · 120 · escama×3 | |
| cnev | lamp · Lanterna · front · 12 · frasco×1 | veil · Véu · back · 12 · cinza×1 | |
| coss | shield · Escudo · front · 14 · lapide×1 | bones · Costelas · back · 16 · osso×1 | |
| cvel | wick · Pavio · front · 12 · pavio×1 | wax · Cera · back · 16 · cera×1 | |
| cosr | coffin · Caixão · front · 50 · lapide×2 | candles · Velas · back · 45 · medalha×2 | |
| reaper | hood · Máscara · front · 95 · mascara×2 | chain · Corrente · back · 100 · lamento×1 | |
| mbes | horn · Mandíbulas · front · 18 · pepita×1 | shell · Carapaça · back · 24 · carapaca×1 | |
| maut | hammer · Martelo · front · 24 · engrenagem×1 | bolts · Parafusos · back · 24 · parafuso×1 | |
| mesc | maw · Boca · front · 16 · escoria×1 | core · Núcleo · back · 18 · ferro×1 | |
| msen | furnace · Fornalha · front · 60 · fornalha×2 | plates · Placas · back · 60 · ferro×2 | |
| colosso | furnace · Fornalha · front · 150 · forja×1 | plates · Placas · back · 160 · placa×2 | pipes · Chaminés · any · 130 · ferro×3 |
| gbro | maw · Boca · front · 14 · semente×1 | leaves · Folhas · back · 14 · espinho×1 | |
| gsei | horn · Chifre · front · 20 · seiva×1 | shell · Carapaça · back · 24 · casulo×1 | |
| gmus | core · Núcleo · front · 40 · nucleo×1 | moss · Musgo · back · 35 · polen×2 | |
| gflo | petals · Pétalas · front · 60 · petala×2 | vines · Vinhas · back · 60 · vinha×2 | |
| rainha | crown · Coroa · front · 160 · essil×1 | vines · Vinhas · back · 160 · vinha×3 | flowers · Flores · any · 130 · petala×2 |
| kesc | halo · Halo · front · 22 · halo×1 | armor · Armadura · back · 24 · rosario×1 | |
| ksin | clapper · Badalo · front · 20 · sino×1 | chain · Corrente · back · 20 · rosario×1 | |
| ksom | glass · Vitrais · front · 18 · vitral×1 | veil · Véu · back · 18 · veu×1 | |
| kcav | halo · Halo · front · 70 · insignia×2 | banner · Estandarte · back · 70 · veu×2 | |
| knight | halo · Halo · front · 170 · nucleolp×1 | banner · Estandarte · back · 170 · brasao×1 | lamps · Lanternas · any · 140 · catedral×1 |

Toasts específicos de parte quebrada (`combat.js:191`, verbatim): lure "A Isca se partiu. O Abissal não encanta mais."; fins "Barbatanas partidas. O Abissal perdeu a maré."; hood "O Capuz rasgou. O Ceifador não some mais."; chain "A Corrente partiu. Ele não puxa mais ninguém." (este último também dispara para a `chain` do ksin). Flutuante genérico: `'Quebrou: '+nome`.

#### Chaves de sprite

`mobSpr` (`render.js:11-18`): `<spr>_d` morto; `<spr>_s` em estados especiais (summon/rain/burst/chain/tide/lure, se existir e `t>.1`); `<spr>_a` em ataque; `<spr>_b` (costas) se jogador está acima (`p.y < m.y-14`) e existir; senão `<spr>_f`. Jogador: `c_<arma>_front|back|side|atk` (`combat.js:113-118`).
`atlas.json` contém apenas `wolf_*`, `fishm_*`, `anglerB_*` (`_a,_b,_d,_f`). Os demais (`cnev, coss, cvel, cosr, cceif, mbes, maut, mesc, msen, mcol, gbro, gsei, gmus, gflo, grai, kesc, ksin, ksom, kcav, karr`, com `_f,_a,_d` e `_s` para chefes) aparecem em `assets/src/meta.json`/`assets/src/*.png`, mas **nenhum código do v2 carrega `assets/src`** (`core.js:22-37`). Não está claro como esses sprites seriam exibidos no protótipo (`spr()` simplesmente não desenha se a chave não existir, `core.js:60`).

### 1.14 Ecos (`ECOS`, `data.js:273-278`)

| id | nome | habilidade | custo base (Essência) | descrição (verbatim) | origem |
|---|---|---|---|---|---|
| abissal | Eco do Abissal | Isca Hipnótica | 55 | Lança uma isca que atrai os inimigos próximos e explode, atordoando. | angler |
| ceifador | Eco do Ceifador | Corrente Votiva | 45 | Arremessa uma corrente que puxa o primeiro inimigo até você e o atordoa. | reaper |
| uivo | Eco do Alfa | Investida Rubra | 35 | Avança como o Alfa, atravessando e ferindo tudo no caminho. | wolfE |
| arauto | Eco do Arauto | Lanternas Votivas | 60 | Três lanternas orbitam você por alguns segundos, disparando contra os inimigos próximos. | knight |

Custo efetivo: `ecoCost = cost - 10*ecoLv` (`core.js:214`), ecoLv ∈ {0,1,2}. Rótulos "Deixado por:" na aba Ecos (`ui.js:202`): abissal "O Abissal do Mercado", ceifador "O Ceifador das Velas", uivo "Alfa da Brecha", arauto "O Arauto da Lanterna Partida".

### 1.15 Golpes Supremos (`ULT`, `extra.js:8-13`) — um por classe

| classe | nome | cd (s) | cor | descrição (verbatim) |
|---|---|---|---|---|
| sword | Fenda Rubra | 40 | `#ff4f7b` | Três investidas cortantes na direção da mira. Você fica invulnerável durante o golpe. |
| bow | Chuva de Vésperas | 45 | `#F2A541` | Uma chuva de flechas cai sobre a área marcada durante 2,5 segundos. |
| staff | Lanterna Estelar | 50 | `#c06cf0` | Uma estrela puxa os inimigos próximos e explode, atordoando todos. |
| hammer | Terremoto da Forja | 45 | `#ff8a3a` | Três ondas de choque partem de você, ferindo e atordoando. |
| dag | Dança das Sombras | 40 | `#d34cff` | Você some nas sombras e golpeia pelas costas até seis inimigos. |

Mecânica em §3.8.

### 1.16 Classes na criação (`CLASSES`, `main.js:124-128`)

| arma | nome | descrição (verbatim) | dificuldade |
|---|---|---|---|
| sword | Lâmina | Equilibrada. Combo de três golpes, o último corta mais fundo. Boa para aprender a ler os monstros. | Fácil |
| bow | Arco | Distância e paciência. Segure para tensionar; flechas carregadas atravessam fileiras. | Média |
| staff | Cajado | Orbes que buscam o alvo e explodem em área. Frágil de perto, letal de longe. | Média |
| hammer | Martelo | Lento e brutal. Quebra partes com o dobro de força: o melhor para farmar materiais. | Difícil |
| dag | Adagas | Rápidas. Golpes pelas costas causam dano dobrado. Exige esquiva constante. | Difícil |

Efetivo: classe é permanente (`extra.js:90-93,161`); `s.cls` força `s.cur`.

### 1.17 NPCs (`NPCS`, `OFICIOS`, `PORTRAIT`, `data.js:78-93`)

NPCs andantes (aparecem se o mapa bate e a hora `h ∈ [h0,h1)`; caminham em `path` com `spd`; falar cicla `lines` em ordem, `world.js:193`):

| id | nome | mapa | horário | velocidade | path |
|---|---|---|---|---|---|
| lauro | Sargento Lauro | forge | 17–27h | 30 | (600,500) (1100,500) (1150,640) (450,560) |
| marta | Marta | forge | 16–21h | 22 | (250,600) (420,640) (300,800) |
| faisca | Faísca | forge | 16–23h | 36 | (590,470) (960,520) (1060,350) (200,420) |
| vesna | Vesna | guild | 18–24h | 24 | (140,410) (350,410) (250,330) |

Falas (verbatim):
- **Sargento Lauro**: "A Guarda segura a rua. As Brechas, quem segura são vocês." / "Se ouvir o sino, é porque rompeu. Aí não tem Casa nem rank: todo mundo corre pra cá." / "Já fui caçador. Hoje só carrego a lança e a lembrança."
- **Marta**: "Peixe fresco não tem mais. O Mercado virou Brecha de novo." / "Meu avô dizia que a água do mercado lembra de tudo. Até do que nunca aconteceu." / "Quebra a barbatana do bicho, caçador. Escama abissal paga meu mês."
- **Faísca**: "Eu acendo as lanternas da rua toda véspera. Se eu esquecer uma, alguém não volta." / "O Abel diz que toda lanterna guarda um pedaço de quem a carregou." / "Quando sua lanterna apagar lá dentro, não tenha vergonha. Todo mundo já esperou alguém ir buscar."
- **Vesna**: "Ecos não são poderes. São lembranças de monstros que se recusam a ir embora." / "Na Casa dos Feitiços eu afino Ecos. Menos essência, mesmo efeito." / "Dois caçadores do mesmo rank nunca lutam igual. A diferença é o que eles absorveram."

Ofícios (Rua dos Ofícios):

| mapa | nome | dono | descrição |
|---|---|---|---|
| shop_arm | Armaria Rubra | Gaspar | Infusões elementais para suas armas. |
| shop_mag | Casa dos Feitiços | Vesna | Afinação de Ecos e grimórios. |
| shop_alq | Alquimia do Ivo | Ivo | Poções e destilação de ervas. |
| shop_rel | Oficina de Relíquias & Lanternas | Abel | Lanternas, escudos e relíquias. |

Retratos de diálogo (`PORTRAIT`): Odete→npc_odete_f, Brás→npc_bras_f, Lúcia→npc_bia_f, Vesna→npc_vesna_f, Ivo→npc_ivo_f, Sargento Lauro→npc_lauro_f, Marta→npc_marta_f, Faísca→npc_faisca_f. Gaspar e Abel **não têm retrato**. Sprites andantes: `npc_<id>_f|b` (`render.js:178`).

Falas fixas dos lojistas (verbatim, `world.js`):
- Brás (abertura, aleatória): "O fogo não pergunta de onde veio o minério. Só se é bom." / "Traga partes, não desculpas. Cada monstro dá o que você arranca dele." / "A Casa da Maré adotou esta rua. Desconto de 10% pra todo mundo, por enquanto."; entre 23h e 5h: "Devia estar dormindo. Mas o fogo pede, e eu obedeço. O que vai ser?". Upgrade: "Cada nível aumenta o dano em 22%. Lâmina e Adagas pedem Presas; Arco, Vidro-lume; Martelo, Minério; Cajado, Estilhaços." Equipamento: "Equipamento bom vem de monstro bem quebrado. Escama, corrente, vidro: tudo vira alguma coisa aqui." Nova arma (efetivo, `extra.js:94`): "Não forjo arma de outra classe pra você. Quem escolhe a {classe} vive com ela. Mas posso deixar essa arma mais afiada do que você imagina."
- Lúcia: "Senta, come alguma coisa. A cidade aguenta mais um pouco." / "Você tá com cara de quem viu um Ceifador. Uma sopa resolve metade disso." / "Os caçadores entram, os caçadores voltam. Eu só mantenho a chaleira quente."; em invasão: "Ouviu? Rompeu. Tranquei a porta dos fundos. Se for sair, leva uma poção."
- Gaspar: "Cada monstro tem uma luz que o fere e outra que ele bebe. Acerte a fraqueza e o golpe vale meio a mais." / (por arma) "Lobos e espectros odeiam o Sol. Besouros apagam na Maré. Golems queimam. O Arauto... dizem que só a Lua o alcança."
- Vesna (loja): "Deixa eu ouvir seus Ecos. Dá pra afinar: mesma lembrança, menos essência." / sem Ecos: "Você ainda não carrega nenhum Eco. Chefes às vezes deixam um quando caem. Volte quando ouvir alguém sussurrando dentro de você."
- Ivo: "Erva-de-Véspera, um pouco de brasa e paciência. É tudo o que uma poção precisa. A paciência eu cobro à parte."
- Abel: "Toda lanterna guarda um pedaço de quem a carregou. Escolha uma que mereça o seu."
- Odete: ver `world.js:205-216` e `extra.js:101-113,129-136` (falas de tutorial/missões).

### 1.18 Caçadores simulados, Casas, chat e boatos (`data.js:280-292`)

`HUNTERS` [nome, rank, arma]: Nina C bow; Bento D hammer; Iara B staff; Tomé E sword; Luzia C dag; Rafa D sword; Jurema A staff; Caio D bow.
`CASAS`: Casa do Alvorecer; Casa da Maré; Casa Ferrugem.
`CHAT` (verbatim; `{b}` ← "cemitério"|"mercado", `{c}` ← uma Casa, `world.js:154`):
1. alguém pra Brecha do {b}? levo poção
2. vi um Abissal com a Isca inteira, quebrem primeiro
3. a forja tá mais barata hoje, a {c} adotou a rua
4. perdi minha lanterna de novo, alguém me salva
5. vendo Minério Rúnico, chama no privado
6. o Ceifador some se o capuz tá inteiro, foca nele
7. boa véspera, Lumeira
8. quem tá com Eco do Alfa? queria ver
9. o pão da Lúcia hoje tá ótimo
10. dica: martelo quebra parte muito mais rápido
11. fechamos a do {b}, ufa
12. a {c} tá recrutando, só rank C+
13. alguém viu meu primo? entrou numa Brecha ontem
14. tô de arco, cubro quem for de lâmina

`RUMORS` (Lúcia, verbatim):
1. Dizem que o Abissal só encanta quem olha pra Isca. Quebre a luz e ele vira só um peixe grande e bravo.
2. O Ceifador carrega a lanterna de alguém que ele levou. Quando a corrente quebra, a lanterna fica.
3. Quando uma Brecha rompe, Brás fecha a forja e eu dobro o preço do pão. Não é ganância, é medo.
4. As Casas brigam por bairro, mas quando a cidade cai, todo brasão sangra igual.
5. Ninguém em Lumeira lembra de ter visto um Alvor. Mas todo mundo jura que um vai chegar.

### 1.19 Mapas (`MAPS`, `data.js:6-77`) — metadados

Retângulos são `[x,y,w,h]`; círculo `{c:[cx,cy,r]}`. Mapas com `mask` usam bitmap (pixel R>127 = andável, célula de 8 px, `core.js:39,241`) e luzes de `assets/lights.json` (raio 46, cor `a`, `core.js:29`). Geometria `walk/block/fast/lights` completa nas linhas citadas (documentada no doc de mundo).

| id | img | nome | tipo | mask/lightsKey | saídas `exits` (→ destino @ chegada) | door / back / outros | spawn |
|---|---|---|---|---|---|---|---|
| guild | map_guild | Guilda dos Lanterneiros | indoor | — | [200,872,100,24]→forge@(1500,250) | — | (250,560) |
| forge | map_city | Rua da Forja | outdoor, hd | mask_city/city | [250,268,40,25]→inn@(250,760); [1460,128,80,22]→guild@(250,840) | — | (1500,250) |
| inn | map_inn | Estalagem Repouso da Lanterna | indoor | — | [200,815,100,15]→forge@(268,325) | — | (250,520) |
| fish | map_fish | Mercado de Peixes | dungeon fish (legado, tem `fast`) | — | — | door [215,88,80,30]; back [200,1000,135,14] | (265,960) |
| crypt | map_crypt | Cemitério | dungeon crypt (legado) | — | — | door [195,85,70,30]; back [190,1000,75,14] | (228,960) |
| fish2 | map_fish2 | Mercado de Peixes | dungeon fish, hd | mask_fish2/fish2 | — | door [740,60,120,40]; back [790,890,90,30] | (830,870) |
| crypt2 | map_crypt2 | Cemitério das Velas | dungeon crypt, hd | mask_crypt2/crypt2 | — | door [180,110,60,40]; back [790,915,100,25] | (835,895) |
| mine | map_mine | Minas da Forja | dungeon mines, hd | mask_mine/mine | — | door [190,70,50,40]; back [300,790,80,20] | (330,770) |
| garden | map_garden | Estufa Silvestre | dungeon garden, hd | mask_garden/garden | — | door [1090,100,90,30]; back [990,915,110,25] | (1040,895) |
| cath | map_cath | Catedral do Arauto | dungeon cathedral, hd | mask_cath/cath | — | bossHere (835,380); exitAt (835,330); back [790,920,90,20] | (835,905) |
| core | map_core | Câmara do Núcleo | dungeon core | — | — | back [213,1000,74,14] | (250,960) |
| shop_arm | shop_arm | Armaria Rubra | indoor, shop | — | [300,394,105,14]→forge@(305,790) | — | (352,370) |
| shop_mag | shop_mag | Casa dos Feitiços | indoor, shop | — | [300,394,95,14]→forge@(305,790) | — | (347,370) |
| shop_alq | shop_alq | Alquimia do Ivo | indoor, shop | — | [305,420,110,14]→forge@(305,790) | — | (360,395) |
| shop_rel | shop_rel | Oficina de Relíquias & Lanternas | indoor, shop | — | [300,420,100,14]→forge@(305,790) | — | (350,395) |

`PORTAL_SPOTS` (portais de Brecha na Rua da Forja): (440,480) e (1174,534) (`data.js:98`). Máximo de 2 Brechas abertas simultâneas (`world.js:36`).

### 1.20 Moedas / recursos

- **Lumes** (`s.lumes`, ícone `i_coin`): moeda única. Inicial 60 (`core.js:210`).
- **Estilhaço de Brecha** (`s.mats.estilhaco`, `i_shard`): material-moeda exibido no HUD.
- **Essência** (`p.ess`, 0–100, não salva): recurso de combate para Ecos.
- **Energia** (`p.en`, 0–100, não salva): esquiva.
- **Poções** (`s.potions`, inicial 3).
- **XP** (`s.xp`) → Selo e Nível.

---

## 2. Núcleo (`core.js`)

### 2.1 Fontes, escala e utilidades

- Fontes: `FT = "Cinzel", Georgia, serif` (títulos; pesos 500/700) e `FU = "Pixelify Sans", monospace` (UI; 400/600) (`core.js:7`; Google Fonts no HTML).
- `U` (escala de UI) = `clamp(min(cssW/1280, cssH/760), .78, 1.6) * DPR` (`core.js:51`); todos os tamanhos de UI abaixo são `valor*U`. `DPR = min(2.5, devicePixelRatio)`.
- Escala do mundo em jogo (efetiva, `main.js:79`): `SC = max(1.2, H/vh)`, `vh = 400` (mapas hd) ou `300`; se `W/SC < 380`, `SC = max(1, W/380)`.
- Câmera: segue `(p.x, p.y-14)`; com chefe vivo, `lerp` 0.3 em x e 0.35 em y para `(boss.x, boss.y-30)`; clamp nas bordas do mapa; suavização `k=min(1,dt*7)` (`world.js:24-29`, `main.js:112`).
- `angDiff(a,b)` = |diferença angular normalizada em [-π,π]| (`core.js:16`). `fmtT(s)` = `m:ss` com `ceil` (`core.js:17`). `fmtClock(min)` = `HH:MM` mod 24h (`core.js:18`).
- `wrap(texto, maxW, sz)`: quebra por palavras e `\n` (`core.js:106-110`).
- `txt()` desenha sombra `rgba(6,4,12,.9)` deslocada `max(1, round(sz/13))` px (`core.js:101`).

### 2.2 Entrada (`core.js:176-205`)

- `K` (tecla segurada), `P` (pressionada neste frame), `typed` (caracteres digitados). `clearInput()` limpa `P`, cliques, roda e `typed` ao fim de cada frame (`main.js:24`).
- `held(...)`: tecla segurada **expira após 900 ms** sem novo `keydown` (mitiga keyup perdido) (`core.js:202`).
- Mouse: `M.down`, `M.click` (botão 0 neste frame), `M.rclick` (botão 2), `M.wheel` (sinal do delta), `M.lastClick` (tempo de jogo do último clique), `M.moved`.
- `confirmKey()` = Enter | NumpadEnter | E | Space (`core.js:205`).
- Ctrl/Meta solta todas as teclas (`core.js:195`).

### 2.3 Save (`core.js:207-218`)

Chave `localStorage['vespera_save_v1']`, JSON de `G.s`. Rejeita se `v !== 1`. Opções separadas em `localStorage['vespera_opt']` = `{shake:bool=true, hints:bool=true}` (`extra.js:2-4`).

Formato (novo jogo, `core.js:208-212`; campos `cls/qi/qc` do `extra.js`):

| campo | tipo | inicial | descrição |
|---|---|---|---|
| v | int | 1 | versão |
| name | string | 'Rui' / digitado (máx. 14 chars) | nome |
| xp | int | 0 | experiência |
| lumes | int | 60 | moeda |
| mats | dict<string,int> | {} | materiais |
| gear | string[] | ['capuz','luva'] | equipamentos na mochila (**não equipados**) |
| equip | dict<slot,string> | {lanterna:'lant0', peito:'couraca'} | equipados |
| weapons | dict<arma,int nível> | {sword:1, bow:1}; com classe: `{[cls]:1}` | armas possuídas |
| cur | string | 'sword' / classe | arma empunhada |
| cls | string | classe escolhida | classe permanente |
| ecos | string[] | [] | Ecos absorvidos |
| ecoEq | string\|null | null | Eco equipado |
| ecoLv | dict<eco,int> | {} | afinações (0–2) |
| elems | dict<arma,string[]> | {} | infusões compradas |
| elem | dict<arma,string\|null> | {} | infusão ativa |
| potions | int | 3 | poções |
| lanterns | string[] | [] | lanternas de outros caçadores carregadas |
| day | int | 1 | dia |
| clock | float (min) | 990 (16:30) | relógio do jogo; +1 min por segundo real (`world.js:143`); ≥1800 → novo dia com 960 (16:00) |
| lost | obj\|null | null | `{b:idBrecha, bairro, bag:{mats,gear,lumes,lanterns}, map, room, x, y}` |
| tut | int | 0 | não usado no v2 (não está claro) |
| closed | int | 0 | Brechas fechadas |
| qi | int | 0 | índice da missão atual |
| qc | dict | {} | contadores de missão: odete, dummy, entered, ults, bosses, broke, bt{tema:true} |

Salva em: rank-up, coletar Eco, recuperar Lanterna, equipar/desmontar, compras, morte, missão cumprida, uso de Supremo, a cada 45 s (`world.js:155`), e menu "Salvar jogo".

### 2.4 Atributos do jogador (`stats()`, `core.js:225-232`)

```
r   = rankIdx(xp); lv = lvlOf(xp).lv
hp  = 100 + 12*r + 6*(lv-1) + Σgear.hp
atkB= Σgear.atk;  def = Σgear.def;  crit = 5 + Σgear.crit (em %)
spd = 1 + Σgear.spd;  regen = Σgear.regen;  ess = 1 + Σgear.ess
light = último gear.light encontrado (padrão 1)   // ordem de iteração de s.equip
se s.lost: hp = round(hp*0.6); light *= 0.45
atk = round( WEAP[cur].dmg * (1 + 0.22*(nivelArma-1)) + atkB * (cur=='dag' ? 0.5 : 1) )
el  = s.elem[cur] ou null
```
Retorna `{hp, atk, atkB, def, crit, spd, light, ess, regen, el}`. Atributos de status iniciais: HP 100, ATK 12 (Lâmina), DEF 6, CRIT 5%, SPD 100%.
Ao mudar `hp` máximo (rank-up, equipar), o HP atual recebe o delta (`combat.js:217`, `ui.js:91`); ao desequipar, só é truncado (`ui.js:92`).

### 2.5 Preços (`core.js:233-237`)

`priceMult = (G.invasion ? 1.5 : 1) * 0.9` → 0.9 normal, 1.35 em invasão. `costLumes(n) = round(n*priceMult)`. Materiais **não** sofrem multiplicador. `costStr` = `"<L> Lumes · <n>× <Material> (<possui>)"`.

| base | normal (×0.9) | invasão (×1.35) |
|---|---|---|
| 6 | 5 | 8 |
| 10 | 9 | 14 |
| 22 | 20 | 30 |
| 30 | 27 | 41 |
| 40 | 36 | 54 |
| 50 | 45 | 68 |
| 60 | 54 | 81 |
| 70 | 63 | 95 |
| 80 | 72 | 108 |
| 90 | 81 | 122 |
| 100 | 90 | 135 |
| 110 | 99 | 149 |
| 120 | 108 | 162 |
| 130 | 117 | 176 |
| 140 | 126 | 189 |
| 150 | 135 | 203 |
| 160 | 144 | 216 |

### 2.6 Colisão (`core.js:240-264`)

- `inWalk(x,y)`: máscara (célula 8 px) ou união de `walk`. `walkable` = inWalk e (se não voador) fora de `block` e fora de props sólidos não quebrados (elipse `dx² + 2.2·dy² < r²`).
- `canStand(x,y,r)`: 4 amostras: (x±r, y) e (x, y±0.6r).
- `moveEnt(e,dx,dy)`: raio `e.cr` ou `min(6, r*0.5)`; subdivide em passos de ≤3 px; resolve X e Y separadamente (desliza em paredes). Jogador `cr=4`.
- `randPoint(minD, {y0,y1,r,avoid})`: até 400 tentativas; fallback `(p.x, p.y-60)`.

### 2.7 Áudio

Sintetizado via WebAudio (ganho mestre 0.22): `swing, hit, brk, dash, pick, coin, hurt, ui, open, boom, bow, magic, rank, die, portal, growl, kill, eco` (`core.js:161-172`). Na porta, mapear para `AudioStreamPlayer` com samples; nomes servem de IDs.

---

## 3. Combate (`combat.js` + overrides)

### 3.1 Estado do jogador (`makePlayer`, `combat.js:2-5`)

`hp, hpLag (barra atrasada), en=100, ess=0, dir, cd (recarga de ataque), atkT (anim. de ataque), combo, comboT, dashT, dashCd, dash[dx,dy], inv (i-frames), hurtT, charge (-1 = não carregando), windT (windup martelo), aim, kx/ky (knockback), chill, ecoCd, potCd, pull, uivo, orbs, ult, ultCd`.
`hpLag` → `lerp(hpLag, hp/max, min(1, dt*2.5))`, nunca abaixo do HP real (`combat.js:51`).

### 3.2 Movimento, esquiva e energia (`combat.js:60-92`)

- Bloqueado (`locked`) se fade, UI aberta ou chat digitando.
- Direção WASD/setas normalizada. Mira (`aimAngle`, `combat.js:44-48`): se mouse segurado ou clique há <0.5 s → ângulo do jogador (`p.y-12`) até o cursor; senão último ângulo de movimento (`mang`) ou direção cardinal.
- **Velocidade**: `78 * st.spd * (chill>0 ? 0.65 : 1)`; ×0.55 durante `atkT`; ×0.45 carregando arco; ×0.2 em windup do martelo; ×0.62 fora de zonas `fast` em mapas que as têm (só o legado `fish`), exceto no Inverno.
- **Esquiva**: Space | Shift | L; requer `dashT<=0`, `dashCd<=0`, `en>=22`. Custa 22 de energia; `dashT=0.17 s` a 270 px/s na direção de movimento (ou da mira se parado); `dashCd=0.32 s` (contado em paralelo); cancela carga do arco e windup. **Invulnerável durante `dashT`** (`hurtPlayer` retorna falso, `combat.js:15`). Rastro visual.
- **Energia**: regenera 30/s quando não está em dash, máx. 100.
- **Regen de HP** (equipamento): `regen` HP/s se não ferido há >4 s.
- Idle >0.45 s → vira para baixo.

### 3.3 Ataques do jogador (`combat.js:93-163`)

Entrada: `atkPressed` = clique esquerdo ou J/K pressionado; segurar J/K repete ataques (exceto martelo). Segurar mouse **não** repete melee (só carrega o arco). Ataque só se `cd<=0` e fora de dash.

**Melee (sword, dag)** — `playerAttack`:
- Lâmina — combo: `combo = comboT>0 ? (combo+1)%3 : 0`; `comboT=0.6 s`. 3º golpe (`combo==2`): dano ×1.6, arco ×1.15, alcance ×1.2, knockback 160 (senão 90), recarga ×1.5.
- `cd = WEAP.cd` (×1.5 no golpe forte); `atkT = 0.2` (adagas 0.14).
- Hitbox (`meleeHit`, `combat.js:153-163`): **setor circular** centrado em `(p.x, p.y-10)`; alvo no ponto `(m.x, m.y-0.5*hh)`; `d = distância - m.r`; acerta se `d <= range` e (`angDiff(ângulo, aim) <= arc/2` ou `d < 4`). Instantâneo (1 frame). Ignora mortos, em spawn e `alpha<0.3`.
  - Lâmina: arco 2.1 rad (~120°), alcance 30 (golpe 3: 2.415 rad, 36). Adagas: 1.4 rad (~80°), 24, `backstab ×2`.
- Props quebráveis: distância `< range+6` e ângulo `<= arc/2+0.3`.
- Se acertou algo: hitstop ≥0.035 s, shake ≥2.

**Martelo (smash)**: ao atacar inicia `windT=0.24 s` e `cd=0.95` imediatamente; anel de aviso; ao fim do windup `smash()`: `meleeHit(arco 2.9 rad ~166°, alcance 36, dano atk, kb 220)`, `atkT=0.28`; shake ≥6; hitstop 0.08 se acertou. `part 2.2` (dano em parte ×2.2).

**Cajado (orb)**: projétil do ponto `(p.x+cos·12, p.y-14+sin·10)`, 190 px/s, raio 5, vida 1.05 s, dano `atk`; `cd=0.72`, `atkT=0.26`. **Teleguiado**: procura o mob mais próximo em 90 px e gira até 4 rad/s. Explode (`orbBoom`) ao tocar mob, parede ou fim da vida: área raio `30 + m.r` (do centro do mob), dano `atk`, `part 1`, kb 80, elemento; quebra props em 30 px.

**Arco (bow)** (`combat.js:97-99,138-143`): segurar (mouse ou J/K) com `cd<=0` inicia `charge=0`; cresce 1/s até 0.9. Soltar dispara:
- velocidade `280 + 240*c`; dano `round(atk*(0.8 + 1.6*c))` (máx. ×2.24); raio 3; vida 0.9 s; `part 0.9`.
- `c >= 0.85` → flecha "cheia": **perfura** (acerta cada mob uma vez), kb 120 (senão 60).
- `cd=0.40`, `atkT=0.18`.

### 3.4 Fórmula de dano no monstro (`hitMob`, `combat.js:164-183`)

```
side  = ((src.x - m.x) * m.face >= 0) ? 'front' : 'back'
mult  = 1
se o.back (adagas=2, Dança das Sombras=1.5) e side=='back': mult *= o.back
crit  = random()*100 < st.crit  → mult *= 1.6
se o.el:  el ∈ m.weak → mult *= 1.5 ("Fraqueza!", cor do elemento)
          senão el ∈ m.res → mult *= 0.6 ("Resiste", #9d93a8)
dano  = max(1, round(dmgBase * mult * R(0.92, 1.08)))
m.hp -= dano; m.flash = 0.12
parte = primeira parte não quebrada com side==side ou side=='any' (ordem do array)
parte.hp -= dano * (o.part || 1)   → se <=0: breakPart
knockback (se o.kb e não chefe): k = o.kb * (elite ? 0.35 : 1); m.kx,ky = direção(src→m) * k
p.ess = min(100, p.ess + 3.2 * st.ess)
idle → chase; hp<=0 → killMob
```
- Dummies (bonecos da Guilda) mostram dano e "frente"/"costas", não perdem HP, **não dão Essência** (retorno antecipado, `combat.js:175`); contam para missão (`extra.js:124`).
- Texto flutuante: crítico em âmbar grande; `mult>1.5` em `#ff8ab8` grande.
- Decaimento de knockback do mob: `k *= 0.001^dt`, zera quando |kx|+|ky|<2 (`combat.js:265`).
- `o.part` por fonte: arma (`WEAP.part`), orbe do Arauto 0.8, Investida 1.2, Corrente 1, Isca 1.3, supremos (ver §3.8).
- `o.el` só é passado por melee, flechas, orbes e supremos; Ecos (exceto supremos) **não aplicam elemento**.

### 3.5 Dano no jogador (`hurtPlayer`, `combat.js:14-24`)

```
ignora se inv>0, morto, dashT>0, ou investida do Eco do Alfa ativa
dano = max(1, round(dmg * (1 - min(0.6, def/100)) * R(0.9, 1.1)))
hp -= dano; inv = 0.75; hurtT = 0.25; shake>=5; hitstop=0.06; flash vermelho redT=0.35
knockback: direção (src→p) * 170, decai 0.0005^dt, zera após 0.5 s do hit ou se <3
Inverno (em Brecha): chill = 1.6 s
hp<=0 → killPlayer
```
Defesa: redução percentual `def%`, teto 60%.

### 3.6 i-frames — resumo

| fonte | duração |
|---|---|
| após ser ferido | 0.75 s |
| esquiva | 0.17 s (`dashT`) |
| respawn | 1.5 s |
| Eco do Alfa (Investida) | `max(inv, 0.4)` + imune enquanto `uivo.t>0` (0.32 s) |
| Supremo Lâmina | `inv>=0.2` contínuo durante 0.5 s |
| Supremo Adagas | `inv>=2.4` |
Visual: jogador pisca (alpha 0.5 alternando a 18 Hz) enquanto `inv>0` (`render.js:165`).

### 3.7 Ecos (`useEco`, `combat.js:226-241`)

Tecla Q ou botão direito. Requer Eco equipado, `ecoCd<=0`, `ess >= ecoCost`. Gasta a Essência, `ecoCd=0.8 s`. Mensagens (`note`) verbatim: "Equipe um Eco no inventário (aba Ecos)." / "Nenhum Eco. Chefes às vezes deixam um ao morrer." / "Essência insuficiente (X/Y)."

| Eco | mecânica |
|---|---|
| **Lanternas Votivas** (arauto) | `p.orbs = {t:7s, cd:0.2}`. 3 lanternas orbitam (ângulo `t*3 + i*2.094`, raio 22×12 em torno de `p.y-14`). A cada 0.55 s, a próxima lanterna dispara no mob mais próximo em 110 px: bolt 260 px/s, raio 4, vida 0.8, dano `round(atk*0.6+4)`, part 0.8. |
| **Isca Hipnótica** (abissal) | Alvo 110 px na mira; se clicou há <1 s, posição do cursor limitada a 140 px. A isca voa a 320 px/s; ao pousar, por 1.4 s atrai mobs a <95 px (55 px/s; chefe 15 px/s); então explode raio `50 + m.r`, dano `atk*2.2+12`, part 1.3, kb 60, **atordoa 1.2 s** (chefe 0.6). |
| **Corrente Votiva** (ceifador) | Projétil 420 px/s, raio 6, vida 0.45 s, dano `atk*1.5+8`, part 1; atravessa paredes. No 1º mob: não-chefe é puxado para 18 px à frente do jogador (0.3 s, `lerp dt*10`) e atordoado 1.3 s; chefe só atordoado 0.5 s. |
| **Investida Rubra** (uivo) | Avanço 0.32 s a 330 px/s na mira; acerta cada mob uma vez se `dist < m.r+14`: dano `atk*1.6+14`, part 1.2, kb 140. Bloqueia demais ações durante. |

Atordoamento (`m.stun`): mob fica `idle`, sem IA (`combat.js:268`). Ao coletar um Eco: `p.ess = 100`, equipa se não houver outro (`combat.js:501`).

### 3.8 Golpe Supremo (efetivo, `extra.js:14-65,160-167`)

Tecla F ou V, fora de UI/chat/fade, vivo. `ultCd` inicia em **6 s** ao entrar no jogo (`p.ultCd==null → 6`), depois o `cd` da classe. Ativar: hitstop 0.12, shake ≥6, banner com o nome (1.4 s). Alvo `ultTarget()`: cursor se mouse mexeu há <2.5 s (limitado a 150 px), senão `(p.x+cos(aim)*90, p.y+sin(aim)*70)`. Mensagem em recarga: "{nome} recarrega em N s."

| classe | mecânica |
|---|---|
| Lâmina — Fenda Rubra | 3 investidas de 0.16 s cada (ângulo `aim ± 0.35` alternado), 420 px/s; acerta cada mob 1× por investida se `hypot(m.x-p.x, m.y-0.4hh-p.y+10) < m.r+30`: dano `atk*2.4+10`, part 1.4, kb 200, elemento. Arco visual 3.4 rad r46. Termina em 0.5 s. |
| Arco — Chuva de Vésperas | Por 2.5 s, a cada 0.07 s uma flecha em `alvo ± (52, 38)`; cada flecha impacta após 0.18 s: acerta mobs com `hypot(dx, dy*1.2) < m.r+12`, dano `atk*0.75+3`, part 0.8, elemento. Termina em 2.8 s. Elipse de área 58×42. |
| Cajado — Lanterna Estelar | Por 1.6 s puxa mobs a <140 px (110 px/s; chefe 20); então explode raio `80+m.r`: dano `atk*4+20`, part 1.6, kb 120, atordoa 1.6 s (chefe 0.8). Termina em 2 s. |
| Martelo — Terremoto da Forja | 3 ondas, iniciadas a cada 0.35 s; raio da onda i = `(t - 0.35i)/0.45 * 90` (válido 0–95); acerta se `|hypot(dx, dy*1.3) - raio| < 16`, 1× por onda: dano `atk*1.6+8`, part 2, kb 140, stun ≥1 s. Termina em 1.3 s. |
| Adagas — Dança das Sombras | Até 6 inimigos mais próximos; a cada 0.2 s (1º após 0.15 s) teleporta para as costas (`m.x - m.face*16`) e golpeia: dano `atk*2.8+6`, `back 1.5`, part 1.2, kb 60, elemento. `inv >= 2.4`. Termina ao acabar a lista ou 2.4 s. |

### 3.9 Escala de monstros por rank da Brecha (`spawnMob`, `combat.js:244-253`)

`rk` = rank da Brecha atual (ou da invasão; 0 fora). `hm = 1 + 0.4*rk` multiplica `hp` e HP das partes. `dm = (1 + 0.25*rk) * (Verão ? 1.15 : 1)` multiplica `dmg`. Sem arredondamento (HP float). Velocidade não escala.
Spawn: `face` aleatório, `st='idle'`, `cd = R(0.6, 1.6)`, `spawnT = 0.45 s` (inativo, intocável, fade-in). `spawnTele` cria um telegraph de 0.9 s (ou `delay`) antes de nascer (`combat.js:254-256`).
Separação entre mobs: se `dist < (r1+r2)*0.8`, empurra `(mind-dist)*0.4` (`combat.js:271`).

| rk | ×HP | ×dano (×1.15 no Verão) |
|---|---|---|
| 0 (E) | 1.0 | 1.00 |
| 1 (D) | 1.4 | 1.25 |
| 2 (C) | 1.8 | 1.50 |
| 3 (B) | 2.2 | 1.75 |
| 4 (A) | 2.6 | 2.00 |
| 5 (S) | 3.0 | 2.25 |

### 3.10 IA dos monstros (`AI`, `combat.js:274-405`)

Transição via `setSt(m, s)` zera `m.t` e `m.done`. Todos os mobs se viram para o jogador (`face = sign(dx)`). Se o jogador está morto (`pd`), não agem. Telegraphs (desenho no chão em magenta, `render.js:109-131`) indicados por estado. Raio de *aggro* = distância para sair de `idle`; receber dano sempre leva a `chase`.

> **Atenção (bug do protótipo):** `broken(m, id)` retorna **true quando a parte não existe** (`combat.js:257`). Como as IAs genéricas consultam ids fixos, vários monstros se comportam como se as partes estivessem quebradas desde o início. A coluna "efetivo" abaixo descreve o que o protótipo faz; a "intenção provável" está anotada. A equipe deve decidir qual reproduzir.

#### lunger (wolf, wolfE, fishm, coss, mesc, gbro, kesc, ksom, kcav)
Partes consultadas: `jaw`, `back`. `spd = m.spd * (back quebrado ? 0.75 : 1)`; `reach = m.r + 24`; aggro 220.

| estado | comportamento | saída |
|---|---|---|
| idle/chase | se `d > reach`: anda até o jogador; senão se `cd<=0`: guarda direção → `wind` (som growl); senão: *strafe* perpendicular a 0.5·spd. `cd -= dt` | — |
| wind | parado (tremor); telegraph linha de 58 px (elite 78), largura `r*1.3` | `t > 0.45` (elite 0.55) → lunge |
| lunge | avança 230 px/s (elite 250) na direção guardada; acerta 1× se `d < r+9`: dano `dmg * (jaw quebrada ? 0.55 : 1)` | `t > 0.24` → rec |
| rec | parado | `t > 0.7` → chase, `cd = R(0.4, 1.1)` |

Efetivo: só wolf/wolfE têm `jaw`/`back`. Para fishm, coss, mesc, gbro, kesc, ksom, kcav → **sempre ×0.75 velocidade e ×0.55 dano**.

#### caster (cnev, ksin)
Parte consultada: `glass`. `want = glass quebrado ? 170 : 95`. Movimento: afasta se `d < want-15`, aproxima se `d > want+25`, + strafe `0.6*sin(t*0.8+bob)`. `cd -= dt`; se `glass` intacto, `cd<=0` e `d<210` → `cast`.
`cast`: em `t>0.6` dispara `n` bolts (n = `m.triple ? 3 : 1`, abertura 0.25 rad) a 125 px/s, raio 4, vida 2.6, dano `dmg`, mirando `atan2(dy-6, dx)`; volta a `idle`, `cd = R(1.8, 2.8)`.
Aggro: `idle && d>230 && m.t<0.1` → espera (na prática `m.t` já passou de 0.1 durante o spawn, então o caster sempre age — não está claro se intencional, `combat.js:297`).
Efetivo: nem cnev nem ksin têm parte `glass` → **nunca lançam projéteis**; apenas mantêm distância 170. `m.triple` é sempre indefinido (o campo está em `m.d`). Intenção provável: usar a parte frontal (lamp/clapper) como "glass".

#### spitter (cvel, mbes, gsei, gflo)
Partes: `horn`, `shell`. `want=85`: afasta se `d<70`, aproxima se `d>115`; strafe `0.5*sin(t*0.9+bob)`; `spd × (shell quebrado ? 1.25 : 1)`. Aggro 220. `cd<=0 && d<200` → `spit`.
`spit`: em `t>0.55` dispara `n = horn quebrado ? 1 : (d.triple ? 5 : 3)` bolts (abertura 0.3 rad) a 115 px/s, raio 5, vida 2.2, dano `dmg`, origem `x+face*10`; em `t>0.9` → chase, `cd = R(1.6, 2.4)`.
Efetivo: mbes/gsei corretos. cvel e gflo (sem `horn`/`shell`) → **1 bolt e ×1.25 velocidade** sempre (gflo `triple` nunca dispara 5).

#### brute (cosr, maut, msen, gmus)
Partes: `core`, `moss`. Aggro 200. Chase se `d > r+20` (vel. ×1.3 se `moss` quebrado); senão `cd<=0` → `slam`.
`slam`: telegraph círculo verde r38 em `(x+face*8, y)` durante 0.75 s; em `t>0.75` impacto: acerta se `hypot(dx, dy*1.3) < 38`, dano `dmg * (core quebrado ? 0.6 : 1)`; em `t>1.6` → chase, `cd = R(0.8, 1.5)`.
Efetivo: só gmus tem `core`/`moss`. cosr/maut/msen → **sempre ×0.6 dano e ×1.3 velocidade**.

#### angler (O Abissal do Mercado) — chefe
Partes `lure` (front) e `fins` (back). `spd × (fins quebradas ? 0.7 : 1)`. Uma vez, ao cair abaixo de 50% HP: invoca 2 `fishm` (telegraph 0.9 s, ponto a ≥40 px do jogador, `y ± 100` do chefe) + toast.
- chase: aproxima se `d > r+22`; `cd<=0` → sorteia de `['bite','bite'] + (lure intacta ? ['lure','lure']) + (fins intactas ? ['tide']) + (d>120 ? ['bite'])`. Guarda direção e ponto `(p.x±18, p.y±18)`.
- **bite**: 0–0.65 s mira (telegraph linha 100 px, largura `r*1.2`, brilho branco); 0.65–1.0 s avança 280 px/s, acerta 1× se `d < r+12` (dano ×1); → rec.
- **lure**: em 0.5 s cria isca no ponto guardado (dura 2.6 s): jogador a <75 px (sem dash) é puxado a 38 px/s e tela fica rosada; ao fim explode r44: dano ×1.1. Telegraph círculo r44 + r75. Em 0.9 s → rec.
- **tide**: telegraph círculo crescendo até r115 em 0.9 s; em 0.9 s onda anelar expande a 150 px/s até 115 px: acerta 1× quando `|hypot(dx, dy*1.4) - r| < 7` (dano ×0.9). Em 1.3 s → rec.
- **rec**: 0.8 s → chase, `cd = R(0.5, 1.3)`.

#### reaper (O Ceifador) — chefe, voador
Partes `hood` (front) e `chain` (back). `alpha` (opacidade; <0.3 = intangível). Invoca 2 `coss` a <50% HP (≥50 px, `y±90`).
- chase: `alpha += 3·dt`; aproxima se `d>65`, recua a 0.6·spd se `d<38`. `cd<=0` → sorteia `['burst'] + (d<80 ? ['swipe','swipe']) + (chain intacta e d>55 ? ['chain','chain']) + (hood intacto ? ['tele'])`.
- **swipe**: mira até 0.45 s (telegraph setor 2.2 rad r46); golpe: acerta se `d < r+40` e `angDiff < 1.2` (dano ×1); → rec em 0.85 s.
- **chain**: mira até 0.7 s (telegraph linha 240 px, largura 4); dispara corrente 400 px/s, raio 6, vida 0.6, dano ×0.8, atravessa paredes; se acerta, puxa o jogador para 30 px do chefe por 0.3 s. → rec em 1.1 s.
- **burst**: em 0.8 s, 12 bolts radiais (`#5fe0d0`) a 105 px/s, vida 2.4, dano ×0.7. → rec em 1.15 s.
- **tele**: 0–0.4 s some (alpha 1→0); em 0.4 s teleporta para `(p.x - cos(aim)*42, p.y - sin(aim)*30)` (atrás da mira do jogador) se puder ficar; reaparece em 0.3 s; em 0.75 s entra em `swipe` com `t=0.15` (golpe 0.3 s depois).
- **rec**: 0.7 s → chase, `cd = R(0.4, 1.1)`.

#### knight (Colosso, Rainha, Arauto) — chefes
Partes por **índice**: `[0]` = halo/furnace/crown (front), `[1]` = banner/plates/vines (back), `[2]` = lamps/pipes/flowers (any).
`ph2 = hp < 50%`; `spd = m.spd * (ph2 ? 1.3 : 1) * (parte[1] quebrada ? 0.75 : 1)`. Ao entrar em ph2 (1×): invoca 2 de `d.summon` (≥50 px, `y±90`) + toast `d.ph2`.
- chase: aproxima se `d > 58`; `cd<=0` → sorteia `['summon'] + (d<90 ? ['sweep','sweep']) + (parte0 intacta ? ['rain']) + (parte1 intacta e d>70 ? ['charge'])`.
- **sweep**: mira até 0.7 s (setor 3 rad r66); golpe: acerta se `d < r+60` e `angDiff < 1.6` (dano ×1), shake 6. → rec em 1.15 s.
- **summon**: em 0.6 s, `n = parte2 intacta ? 3 : 1`. Arauto (`orbs:1`): cria `n` lanternas orbitais (`korb`, raio 40×22 em `y-30`, 1.6 rad/s, duração 6 s; cada uma atira no jogador a cada 1.3 s — 1º tiro após `1 + k*0.35` s —, bolt 130 px/s, dano `boss.dmg*0.55`, vida 2.4). Colosso/Rainha: invocam `min(n, 4 - vivos)` adds de `d.summon` já em chase. → rec em 1.1 s.
- **rain**: em 0.4 s, 5 (ph2: 7) estilhaços em `jogador ± (50, 40)` (o 1º exatamente no jogador), defasados 0.12 s; cada um cai em 0.9 s: acerta se `hypot(dx, dy*1.3) < 16` (dano ×0.8). Telegraph círculo r16. → rec em 1.2 s.
- **charge**: mira até 0.6 s (linha 130 px, largura `r*1.2`); 0.6–1.0 s avança 320 px/s, acerta 1× se `d < r+12` (dano ×1.1). → rec.
- **rec**: 0.8 s (ph2 0.5) → chase, `cd = R(0.4, 1.0)`.

Chefes **não sofrem knockback** (`combat.js:179`) e nascem com `spawnT=1.2`, `st='chase'` no `bossHere` do mapa ou (250,500); banner de intro 3.2 s: nome + "Quebre suas partes. Cada uma muda a luta." (`world.js:113`, `render.js:308`).

### 3.11 Projéteis (`updProjs`, `combat.js:408-434`)

- Todos andam em linha reta (`x += vx*dt`), exceto o orbe (teleguiado).
- Morrem ao sair da área andável (`!inWalk(x, y+10)`), exceto correntes. Orbe explode.
- Projétil do jogador vs mob: `hypot(m.x-q.x, m.y-0.5hh-q.y) < q.r + m.r`; ignora mobs já acertados (`hit` set), em spawn ou intangíveis.
- Projétil de mob vs jogador: `hypot(p.x-q.x, p.y-11-q.y) < q.r + 6`. Some ao causar dano; se o jogador estava invulnerável o projétil **continua**.

| projétil | dono | vel. | raio | vida | dano | notas |
|---|---|---|---|---|---|---|
| arrow | jogador | 280–496 | 3 | 0.9 | atk×(0.8–2.24) | perfura se cheia |
| orb | jogador | 190 | 5 | 1.05 | atk (área r30+m.r) | teleguiado 4 rad/s, alcance 90 |
| bolt (Arauto Eco) | jogador | 260 | 4 | 0.8 | atk×0.6+4 | |
| chain (Eco) | jogador | 420 | 6 | 0.45 | atk×1.5+8 | puxa/atordoa |
| bolt caster | mob | 125 | 4 | 2.6 | dmg | |
| bolt spitter | mob | 115 | 5 | 2.2 | dmg | leque 0.3 rad |
| bolt burst (Ceifador) | mob | 105 | 4 | 2.4 | dmg×0.7 | 12 radiais |
| mchain (Ceifador) | mob | 400 | 6 | 0.6 | dmg×0.8 | puxa jogador |
| bolt korb (Arauto) | mob | 130 | 4 | 2.4 | dmg×0.55 | |

### 3.12 Drops e coleta (`combat.js:194-213, 476-530`)

**Morte de mob** (`killMob`): +`d.xp` XP; Estilhaço: 100% para chefe (×4) e mobs de invasão (×1), senão 70% (×1). Lumes: chefe `RI(6,9)` montes de `RI(4,7)`; comum `RI(1,3)` montes de `RI(1,3)`. Chefe: +1 equipamento `rollGear(true)`, shake 12, hitstop 0.3, chama `bossDefeated`. Eco: ver §1.13. Corpo removido após 1.6 s (chefe 3.2 s).
**Quebra de parte**: `n` drops do material, hitstop ≥0.09, shake ≥4, conta `run.broke`.
**`rollGear(boss)`**: sorteio ponderado sobre todos os `GEAR` exceto `lant0`: raro peso 0.6 (3 se `boss`/baú roxo), incomum 2, comum 3.
**Baú** (`openChest`): `RI(3,5)` montes de `RI(2,5)` Lumes; `RI(1,2)` estilhaço; 1 de madeira/erva/madeira; equipamento 30% (roxo 85%, `rollGear(purple)`); poção 35%. (O laço `for(i<RI(3,5))` re-sorteia o limite a cada iteração — distribuição não uniforme; `combat.js:525`.)
**Prop quebrável** (`breakProp`): `r<0.45` → `RI(1,3)` Lumes; `r<0.75` → 1 material (`r<0.6` ? `pmat` do tema : erva); `r<0.82` → 1 poção; senão nada.
**Física do drop** (`dropPick`): ângulo aleatório, velocidade `R(20,60)` (vy ×0.6), altura `z=8`, `vz=R(50,90)`, gravidade 260; quique se `vz<-30` (`vz = -vz*0.35`), atrito ×0.5 ao tocar o chão.
**Ímã** (`updPicks`): após 0.45 s de vida, se `d < 48` px: move-se para `(p.x, p.y-4)` com fator `min(1, dt*(5 + (48-d)/6))`; coleta se `d < 9`. Itens `needE` (Eco, lanternas) exigem E (raio 18) e não são atraídos.
**Destino**: dentro de Brecha, Lumes/materiais/equipamentos/lanternas vão para a **bolsa da Brecha** (`run.bag`), só integrada ao save ao sair (`finishRun`/fuga) ou recuperar a Lanterna; poções e Ecos vão direto ao save.

### 3.13 Morte e mecânica da Lanterna (`combat.js:25-41`, `ui.js:252-260`, `world.js:66-72`)

1. `hp<=0` → `killPlayer`: som, shake 9, hitstop 0.15, partículas. Após 2.4 s abre a tela `death`.
2. Tela de morte → Enter/E/Space/clique após 1.2 s → `respawnAfterDeath`:
   - **Dentro de Brecha**: `s.lost = {b, bairro, bag, map, room, x, y}` (bolsa inteira fica na Lanterna); `rescueAt = t + R(55, 85)`; sai da Brecha; volta à Guilda (250,330), HP = máx. reduzido, Essência 0, inv 1.5 s. Toast "Sua Lanterna ficou na Brecha" / "Vida máxima reduzida até alguém trazê-la de volta."
   - **Fora**: Estalagem (250,520), HP 50%, −10 Lumes (mín. 0), inv 1.5 s. Toast "Lúcia te arrastou até a estalagem" / "Você perdeu 10 Lumes no tumulto."
3. Enquanto `s.lost`: HP máx. ×0.6, luz ×0.45; HUD "Sem Lanterna · vida máxima -40%".
4. Recuperação (a que ocorrer primeiro): (a) entrar na mesma Brecha e mesma sala → pick `mylantern` (E: "Recuperar sua Lanterna") na posição da morte; (b) `G.t >= rescueAt` → um caçador traz; (c) pagar Odete 40 L → `rescueAt = t+12`; (d) a Brecha ser fechada por NPC, invasão resolvida ou chefe derrotado. Recuperar mescla a bolsa no save e devolve o HP máximo.
5. Lanternas de outros caçadores: 45% de chance por Brecha (`world.js:96`); entregar a Odete = +35 L e +20 XP cada.

### 3.14 Brechas e ondas (resumo de `world.js:34-127`)

- Abertura a cada `R(80,120)` s (primeira: 16 s em jogo novo, 6 s em continuar); máx. 2 abertas; tema sorteado entre os com `minRk <= rankIdx+1`; 1ª Brecha sempre `fish`, sem o bônus aleatório de rank, timer 300 s. Rank: `clamp(max(minRk, rankIdx) + (25% ? 1 : 0), 0, 9)`; timer `R(210,270)` s. 55% delas têm `npcAt = R(25,120)`: quando o timer chega nesse valor, uma Casa a fecha.
- Timer zera → **ruptura**: invasão na Rua da Forja com `5 + min(4, rk)` mobs do tema (+1 Alfa se >4), preços ×1.5, forja fechada; limpar = +30 L, +30 XP, `closed++`.
- Entrada: efetivo só se `b.rk <= rankIdx + 1` (`extra.js:97`).
- Ondas: primeira Brecha da vida (`closed==0`): 2 ondas de 2 e 3 mobs. Demais: `2 + (rk>=1 ? 1 : 0)` ondas; onda i tem `3 + i + RI(0,1)` mobs sorteados por peso; ondas i>0 recebem +1 `extra`; última onda 50% +1 `elite`. Intervalo inicial 2.5 s (4 na fácil), entre ondas 1.6 s; mobs nascem a ≥130 px do jogador.
- Ondas limpas → porta para `core` (ou, na Catedral, chefe surge no próprio mapa). Chefe surge após 1.8–2.2 s.
- Chefe derrotado: `closed++`, bolsa += `40 + 25*rk` Lumes, +60 XP; portal de saída após 1.4 s.
- Fuga pela borda `back`: diálogo "Saída da Brecha" — leva a bolsa, Brecha continua aberta.

### 3.15 XP, Selo e rank-up (`gainXP`, `combat.js:214-219`)

Fontes de XP: `MON.xp` por kill; +60 por chefe; +30 por invasão resolvida; +20 por lanterna entregue. Missões não dão XP (só Lumes/poções/materiais).
Rank-up (só Selo; subir de **Nível** não tem efeito visual além do HUD): som `rank`, `G.rankUp = {t:0, r}` (overlay 4 s), recalcula `stats()` e soma o delta de HP máx. ao HP atual, mensagem no chat de Odete "O Selo de {nome} mudou para {id}. Lumeira agradece.", salva.

### 3.16 Poção (`usePotion`, `combat.js:220-225`)

Tecla R. `potCd=1 s`. Cura `round(hpMax*0.45)`. Bloqueia se sem poções ("Sem poções. Lúcia vende na estalagem.") ou HP cheio ("Vida já está cheia.").

### 3.17 Feedback (game feel)

| evento | hitstop (s) | shake |
|---|---|---|
| melee acertou | ≥0.035 | ≥2 |
| martelo acertou | 0.08 | ≥6 (sempre) |
| parte quebrada | ≥0.09 | ≥4 |
| jogador ferido | 0.06 | ≥5 |
| morte do jogador | 0.15 | 9 |
| chefe morto | 0.3 | 12 |
| Supremo | 0.12 | ≥6 |
Shake decai `×0.002^dt`, zera <0.2; desligável nas Opções (`extra.js:162`). Flash branco no mob 0.12 s; flash vermelho de tela `redT` 0.35 s (alpha `redT*0.5`).

---

## 4. Interfaces (`ui.js` + `render.js` HUD + `extra.js`)

Estrutura: `G.ui = {type, t, ...}`; enquanto há UI, o mundo **não atualiza** (`main.js:91`). Ao fechar, todas as teclas são liberadas. Quase todas as telas ignoram entrada nos primeiros 0.15 s (`t > .15`). Overlay de fundo `rgba(6,4,12,α)`.

Componentes de desenho comuns (`core.js:112-142`):
- **panel**: fundo `rgba(15,12,25,.95)` + gradiente `rgba(74,44,107,.18)`→transparente; contorno externo 3 px `#05040a`; borda 1 px `#8a6a35`; borda interna `rgba(201,150,62,.22)` a 4 px; losangos dourados (`#c9963e`, raio 4U) nos cantos.
- **slotBox**: `#110d1b`; borda `#3d2e22` (hover `#a07a3a`, selecionado 2 px âmbar + halo); losangos nos cantos.
- **bar**: moldura `#07050c`, fundo `#1d1626`, "lag" `rgba(244,231,198,.55)`, gradiente vertical c1→c2, brilho 25% superior, borda `#8a6a35`.
- **keyCap**: caixa `#120e1c` borda `#c9a46a`, texto 600.
- Item selecionado em listas: gradiente horizontal `rgba(74,44,107,.85→.15)` + losango âmbar.

### 4.1 HUD (`render.js:233-311` + `extra.js:139-158`)

| região | conteúdo |
|---|---|
| Topo-esq. (18U,16U) | ícone de Selo 40U; nome (Cinzel 700 17U); "Nv. N" (13U âmbar); "Selo X · Título" (12U, cor do rank); barra fina de XP do nível (150×3U âmbar). Barras: **Vida** 210×10U (`#e8506a→#8f1f3a`, com lag) + "hp / max"; **Energia** 157×7U (`#6fd39a→#1f7a55`); **Essência** 126×5U (`#f06aa8→#7a2a6b`) com marcador vertical no custo do Eco equipado (padrão 60) + rótulo "Essência". Se sem Lanterna: "Sem Lanterna · vida máxima -40%" (magenta). Dicas de teclas: Tab Inventário · Esc Menu · F Supremo · Enter Chat. |
| Abaixo (18U, 170U) | Painel de missão atual (se `OPT.hints`): ícone pergaminho, nome (Cinzel 13U âmbar), descrição 12U, progresso "n/8" à direita (`extra.js:152-155`). |
| Topo-dir. | Caixa "DIA" + número (Cinzel 22U, borda âmbar); nome do local (Cinzel 15U); "Estação · HH:MM · véspera" (véspera entre 16h e 19h). Lista de Brechas não fechadas (230×30U): ícone de rank, "Brecha · bairro", título / "ROMPEU — Rua da Forja" / "você está dentro", timer `m:ss` (pisca magenta <60 s). Em invasão: "Preços +50% · Forja fechada". |
| Centro-baixo (hotbar) | 5 slots 44U (armas 1–5; arma de outra classe com cadeado; nível "+N"); slot Eco "Q" (escurece proporcional à Essência faltante); slot Poção "R" com contagem; nome da arma acima. À esquerda: botão circular do Supremo 58U (tecla F, nome acima, varredura de recarga + segundos, brilho pulsante quando pronto, arco âmbar de progresso). |
| Baixo-dir. | Lumes (15U 600 + ícone), Estilhaços acima; dentro de Brecha: "Bolsa da Brecha: N Lumes · N materiais · N equip." (âmbar). |
| Baixo-esq. | Chat: últimas 6 linhas, "Nome:" (600, cor do autor) + texto; somem após 22–26 s. Caixa de digitação "Chat:" (máx. 80 chars, Enter envia, Esc cancela). |
| Direita (42% altura) | Notas (`note`): até 6, 4 s, painel com ícone. |
| Topo-centro | Barra do chefe: nome (Cinzel 16U), barra `min(W*0.5, 560U)`×10U (`#e64a8e→#6b1f4a`), lista de partes com losango e "— quebrada". |
| Centro (80U/84U do topo) | Toast (fila, um por vez, padrão 4.2 s): painel com borda da cor, ícone 30U, título Cinzel 18U, subtítulo 12U. |
| Centro | Nome do local ao entrar (3 s, Cinzel 30U, linha dourada); intro do chefe (3.2 s, faixa escura, nome em magenta); **Rank-up** (4 s): escurece tela, ícone 110U, "Novo Selo", "ID · Título" Cinzel 28U na cor do rank; banner do Supremo (1.4 s, desliza). |
| Mundo | Barras de HP de mobs feridos/elites (26U, elite ×1.4) com losangos de partes; nome de elites; nomes de caçadores com rank colorido e balões; nome de NPCs (âmbar); "Nome (você)" + seta; portais com bairro/timer; números flutuantes (12U; grandes 15U; sobem 16/22 px/s, duram 0.8/1.1 s); prompt de interação "[E] rótulo". Vinheta radial; flash vermelho; rosa quando encantado. |

### 4.2 Diálogo (`uiDialog`, `ui.js:17-52`)

- Caixa: largura `min(W-36U, 900U)`, altura `max(150U, bw/3.6)`, a 16U do rodapé, sprite `ui_dialog` esticado; área de texto `rgba(12,9,22,.55)` (62% da largura). Fundo `rgba(6,4,12,.35)`.
- Retrato (`PORTRAIT[who]`) com altura `bh*0.62` à esquerda; nome em Cinzel 700 17U âmbar; texto 15U, linhas a cada 20U.
- **Máquina de escrever**: 75 caracteres/s. Pronto após 0.18 s. Confirmar (Enter/E/Space/clique) durante a digitação completa o texto; depois fecha e chama `o.next`. Esc fecha. Indicador "▸" pulsante.
- **Opções**: painel acima da caixa, alinhado à direita, largura `min(520U, W*0.56)`, linhas de 34U; cada opção pode ter ícone 24U, rótulo 14U e `sub` 10.5U (`#b39fc0`); `dis` → cinza `#6f6479`, ao escolher toca `hurt`. Navegação ↑/↓ ou W/S (cíclica), hover do mouse seleciona, clique/Enter confirma, Esc fecha. Após executar `fn`, fecha se a função não abriu outra UI (permite cadeias de menus de loja).
- **Lojas e forja usam exclusivamente este componente** (não há tela de loja/forja dedicada): Brás, Lúcia, Ivo, Gaspar, Vesna, Abel, Rua dos Ofícios, portais e fuga.

### 4.3 Mural de Contratos (`uiBoard`, `ui.js:55-79`)

Painel `min(W-40U,840U)` × `min(H-50U,600U)`, fundo 0.7. Título "Mural de Contratos" (Cinzel 24U); subtítulo "A Casa da Maré adotou a Rua da Forja: 10% de desconto em toda forja e oficina." (+ " Preços +50% enquanto houver invasão."). Até 5 Brechas (mais recentes primeiro), cartões de 74U: ícone de rank 40U, "bairro — título" (Cinzel 16U), "estação · modificador", status ("Portal na Rua da Forja · rastreada" / "ROMPEU — monstros na Rua da Forja" / "Fechada"), timer 22U (magenta <60 s). Rodapé: "Brechas fechadas por você: N"; teclas Enter Rastrear, Esc Fechar. Vazio: "Nenhuma Brecha aberta. A próxima véspera não deve demorar." Controles: ↑/↓, clique/Enter/E/Space = rastrear (`G.track`), Esc/Tab/clique fora = fechar.

### 4.4 Inventário (`uiInv`, `ui.js:105-233` + overrides)

Abre com Tab/I. Painel `min(W-20U,1220U)` × `min(H-20U,780U)`, fundo 0.82. Logo à esquerda; 3 abas (170×38U, ícones eq_armor1, i_eco, r_C): **PERSONAGEM**, **ECOS**, **SELOS**; botão × (30U). Rodapé: Esc Fechar · Tab Aba · Setas Navegar · E Equipar · X Descartar; Lumes e Estilhaços à direita. Tab cicla abas; Esc/I fecha.

**Aba Personagem** (44% esquerda / resto direita):
- Esquerda: título "PERSONAGEM"; palco com sprite `c_<arma>_front` flutuando, brilho âmbar/magenta e elipses no chão; 8 slots em volta (`slotPos`: col0 = arma, lanterna, capa; col2 = peito, mãos, relíquia; centro topo = cabeça, centro baixo = botas), com rótulos (Cinzel 10U). Clicar em slot equipado (exceto arma) desequipa. Quadro de atributos: Vida, Energia (fixo 100), Ataque, Defesa | Crítico %, Velocidade %, Rank (título), Essência %.
- Direita: título "INVENTÁRIO"; filtros: Todos / Armas (i_sword) / Equipamento (eq_armor0) / Materiais (i_ore) / Consumíveis (ic_potion). Grade de 5 colunas, célula `min(64U, ...)`, gap 8U, rolagem automática; badges: quantidade, losango âmbar na arma empunhada, faixa de cor de raridade em equipamentos, cadeado + véu para itens bloqueados. Navegação setas/WASD (←/→ ±1, ↑/↓ ±5). Duplo clique (<0.35 s) = 1ª ação.
- Painel de detalhe (`min(300U, 40%)`): ícone grande com brilho, nome (Cinzel 17U), tipo, descrição 12.5U, linhas de atributos, texto de bloqueio em `#ff8a8a`, "◆ Raridade", nota "Empunhada". Ações com tecla: E Equipar / X "Desmontar (+2 Estilhaços)" / R Usar (poção). Enter = 1ª ação.
- `itemInfo` (efetivo): arma → stats "Ataque base, Nível, Elemento, Partes ×part", raridade visual por nível (≥4 raro, ≥2 incomum); armas de outra classe bloqueadas. Equipamento → `statRows` (Ataque +, Defesa +, Vida +, Crítico +%, Velocidade +%, Luz ×, Essência +%, Regeneração /s) + "Classe" + "Selo mínimo"; bloqueado remove a ação E. Material → Quantidade, Valor. Poção → "Recupera 45% da vida. Ivo destila com Erva-de-Véspera." Lanterna perdida (épico) → "Ainda está morna. Leve até Odete, na Guilda: {nome} está esperando."

**Aba Ecos**: texto introdutório (2 linhas); 4 cartões (≤270U × ≤330U): ícone do Eco (15% opaco se não possuído), nome ou "???", habilidade ou "Deixado por: …", descrição, "Custo: N de Essência (afinado)", "Equipado em [Q]" / "[E] Equipar". ←/→ seleciona; clique ou E/Enter equipa (se possuído).

**Aba Selos**: texto introdutório; fileira dos 10 ícones de Selo (atual 54U com brilho, obtidos opacos, demais 25%) com título colorido e "N XP"; barra de progresso até o próximo Selo + "xp / próximo XP até o Selo X" ou "Você chegou ao Alvor."; 5 visuais de tier (look0–4) com rótulos.

### 4.5 Pausa (efetivo `extra.js:245-258`)

Esc abre. Painel 460U de largura, título "Pausa" (Cinzel 26U) + "Dia N · local". Opções (44U cada): Continuar · Diário de missões · Como jogar · Controles · Multijogador · Opções · Salvar jogo ("Jogo salvo.") · Voltar ao menu principal. Sub-telas voltam para a pausa com a seleção preservada. ↑/↓/W/S, hover, clique, Enter/E; Esc fecha. (A pausa original de `ui.js:236-251`, com lista de controles e "História de Lumeira", é sobrescrita.)

Sub-telas (`frameBox` 980×600U, título Cinzel 26U, "Esc Voltar"; Esc/Backspace/clique voltam):
- **Diário de missões**: missões cumpridas (losango verde, "Cumprida"), atual (descrição + progresso), próxima como "???".
- **Como jogar**: 7 páginas (`HELP`, `extra.js:170-177`): História, Regras, Combate, Brechas, Classes, Selos, Cidade; ↑/↓ troca página.
- **Controles** (`CONTROLS`, `extra.js:178`): WASD/Setas Mover; Mouse Mirar; J, K ou clique Atacar (Arco: segure); Espaço/Shift Esquiva; F ou V Golpe Supremo; Q/botão direito Usar Eco; R Beber poção; E Interagir, falar, abrir baús; Tab/I Inventário; Enter Chat; Esc Menu de pausa; M Som liga/desliga. (L também esquiva; roda/1–5 trocam arma, mas a classe bloqueia.)
- **Opções**: Som; Tremor de tela; Painel de missão no HUD (persistidos em `vespera_opt`, exceto som).
- **Multijogador**: prévia sem rede (salas fictícias `MP_ROOMS`), qualquer botão mostra "O multijogador ainda está em construção."

### 4.6 Morte (`uiDeath`, `ui.js:252-260`)

Fade `a = min(1, t*1.5)` até 0.82 de opacidade. Ícone de Lanterna 90U em `H*0.34`; título (Cinzel 700 30U): "Sua Lanterna se apagou" (em Brecha) / "Você caiu na rua". Subtítulo 14U (largura `min(W*0.7, 620U)`): "Ela ficou na Brecha do {bairro}, com tudo o que você coletou lá dentro. Outro caçador pode trazê-la de volta. Ou você mesmo, se voltar antes que a Brecha feche." / "Alguém vai te carregar até a estalagem." Após 1.2 s, prompt pulsante "[Enter] Voltar à Guilda" / "[Enter] Acordar"; Enter/E/Space/clique → respawn.

### 4.7 Rank-up

Não é uma tela `G.ui`: overlay no HUD por 4 s sem bloquear o jogo (§4.1). Também: chat de Odete e som de arpejo.

### 4.8 Título, história e classe (efetivo `extra.js:261-324`, história em `main.js:150-171`)

- Título: keyart com leve deslocamento senoidal, gradiente escuro à esquerda, 60 brasas animadas; slogan "Cada véspera, uma Brecha. Cada Brecha, uma memória."; opções: Continuar ("Dia N · Selo X · Nome (Classe)") · Novo jogo · Multijogador · Como jogar · Controles · Opções; rodapé "↑↓ navegar · Enter confirmar · Esc voltar" e "v0.2 · protótipo". Novo jogo com save existente → confirmação "Começar do zero?" (Apagar e começar / Cancelar).
- História: 5 capítulos (`STORY`) com o mapa de Lumeira à esquerda; Enter/Space/E/→/D/clique avança, ←/A volta, Esc pula.
- Classe: "Escolha sua classe" + "A classe é permanente. Só a arma e os equipamentos da sua classe poderão ser usados."; campo Nome (máx. 14, letras/números/espaço/'/-); 5 cartões com sprite animado, dificuldade (Fácil verde, Média âmbar, Difícil magenta), descrição e Supremo [F]; ←/→, Enter, Esc.

### 4.9 Mapa

**Não existe tela de mapa no v2.** O único uso de `lumeira_map.png` é ilustração na tela de história (`main.js:161`).

### 4.10 Tipografia e cores de texto recorrentes

Títulos de painel: Cinzel 700 22–30U creme. Texto: Pixelify 12–15U. Secundário `#b39fc0`, `#cdbfd6`; apagado `#8d7f99`, `#6f6479`; corpo de descrição `#e4dbe9`; destaque Eco `#d99cff`; cura `#7cf0a0`; dano no jogador `#ff5d72`.

---

## 5. Inconsistências e bugs do protótipo (decidir antes de portar)

1. `broken()` retorna true para partes inexistentes (`combat.js:257`) → monstros fora de wolf/gmus/mbes/gsei/chefes já "nascem quebrados" (velocidade/dano alterados; casters nunca atiram; gflo nunca dispara 5). Ver §3.10.
2. `caster` usa `m.triple` em vez de `m.d.triple` (`combat.js:293`).
3. Aggro do caster praticamente sempre ativo (`m.t<0.1`, `combat.js:297`).
4. Outono sem efeito de gameplay (§1.4).
5. Materiais sem fonte (vidro, tecido, corrente, estandarte, brasa) → infusão Ígnea inobtenível (§1.8).
6. Texto do Brás sobre materiais de upgrade diverge de `UPG` (§1.6).
7. Laços `for(i < RI(a,b))` re-sorteiam o limite a cada iteração (`combat.js:525`, `world.js:92`).
8. Sprites de 20 monstros não estão no `atlas.json` carregado (§1.13).
9. `s.tut` salvo mas não usado; `stats().light` depende da ordem de iteração do objeto `equip` (só há um slot com `light`, então irrelevante na prática).
10. A fala do 3º toast de parte "chain" ("A Corrente partiu…") também dispara para a corrente do Sino Vivo.

---

## 6. Mapa para Godot 4.7 (C#)

### 6.1 Resources de dados (`[GlobalClass] public partial class X : Resource`)

| Resource | campos |
|---|---|
| `RankDef` | `string Id; int XpMin; string Title; Color Color; Texture2D Icon; string LookKey` |
| `LevelCurve` (constante) | `int BaseNeed = 40; float Growth = 1.18f; int HpPerLevel = 6; int HpPerRank = 12; int BaseHp = 100` |
| `ElementDef` | `string Id; string Name; Color Color; Cost InfusionCost` |
| `Cost` | `int Lumes; Dictionary<string,int> Mats` |
| `WeaponDef` | `string Id; string Name, Short, Type; Texture2D Icon; int Dmg; float Cooldown; WeaponKind Kind {Melee, Bow, Orb, Smash}; float Arc; float Range; float PartMult; float Backstab; Color Color; string Desc; Cost Unlock; string UpgradeMat; int UpgradeMatPerLevel (1; cajado 2); string[] Elements` |
| `UltimateDef` | `string ClassId; string Name; float Cooldown; Color Color; string Desc` |
| `MaterialDef` | `string Id; string Name; Texture2D Icon; Rarity Rarity; int Value (=3); string Desc` |
| `GearDef` | `string Id; GearSlot Slot; string Name; Texture2D Icon; int Hp, Def, Atk, Crit; float Spd, Light(0 = n/a), Ess, Regen; Rarity Rarity; int? MinRankOverride; string[] Classes; string Desc` |
| `RecipeDef` | `GearDef Result; Cost Cost` |
| `ShopEntry` / `ShopDef` | `string ShopId; ShopEntry[] {ItemRef, Cost, Effect}` (Abel = RELSHOP; Ivo/Lúcia como entradas de consumível) |
| `PartDef` | `string Id; string Name; PartSide Side {Front, Back, Any}; float Hp; MaterialDef Drop; int Count = 1` |
| `MonsterDef` | `string Id; string Name; string SpriteKey; float Scale; float Hp, Speed, Dmg; int Xp; float Radius; float? HitHeight; AiKind Ai; bool Elite, Boss, Fly, Triple, Orbs; EcoDef Eco; float EcoChance; MonsterDef Summon; Color? Bolt, Col; string[] Weak, Res; (string light, float radius) Glow; PartDef[] Parts; string Phase2Title, Phase2Sub` |
| `EcoDef` | `string Id; string Name; string Skill; int Cost; string Desc; string SourceLabel` |
| `ThemeDef` | `string Id; string Bairro; string MapId; MonsterDef Boss; int MinRank; Dictionary<Season,string> Titles; (MonsterDef, float weight)[] Mobs; MonsterDef Extra, Elite; string[] Props; MaterialDef PropMat; bool Stone` |
| `SeasonDef` | `Season Id; string ModText` (+ flags de efeito) |
| `NpcDef` | `string Id; string Name; string MapId; int HourFrom, HourTo; Vector2[] Path; float Speed; string[] Lines; Texture2D Portrait` |
| `OficioDef` | `string MapId; string Name; string Owner; string Desc` |
| `HunterDef`, `QuestDef` (`Name, Desc, Condition(enum+param), Reward(Cost-like), EndDialog`), `ClassDef` (`WeaponDef, Name, Desc, Difficulty, UltimateDef`) |
| `MapDef` | `string Id; Texture2D Image; Image Mask; string Name; flags (Indoor, Outdoor, Dungeon, Shop, Hd); Rect2[] Walk, Block, Fast; ExitDef[]; Rect2? Door, Back; Vector2 Spawn; Vector2? BossHere, ExitAt; LightDef[]` |

Um `GameDatabase` (Resource ou autoload) agrega dicionários por id. Textos verbatim em CSV de tradução (`tr()`), mantendo pt-BR como fonte.

### 6.2 Lógica pura (`src/Core`, sem Godot, testável com xUnit/NUnit)

- `Rng` (`IRandom`: `Range(a,b)`, `RangeInt(a,b)` inclusivo, `Pick`, `Chance`) — sem `Math.random` global.
- `JsRound` helper (`Floor(x+0.5)`).
- `Progression`: `RankIndex(xp)`, `LevelOf(xp)` → (lv, cur, need), `GainXp` → evento de rank-up.
- `PlayerStatsCalculator.Compute(SaveData, db)` → `PlayerStats` (§2.4).
- `PriceCalculator`: `PriceMult(invasion)`, `CostLumes(n)`, `CanPay`, `Pay`.
- `DamageCalculator`: `ToMonster(base, ctx{side, backstab, critRoll, element, weak/res, variance})`, `ToPlayer(dmg, def, variance)`, `PartDamage`, `KnockbackScale(elite, boss)`, `EssenceGain(ess)`.
- `HitSide(srcX, mobX, face)`, `MeleeHitTest(origin, aim, arc, range, target, radius)`, `ShockwaveRingHit`, `EllipseHit(dx, dy, yScale, r)`.
- `MonsterScaling(rk, season)`.
- `LootTables`: `RollGear(boss)`, `KillDrops(def, isBoss, isInvasion)`, `ChestDrops(purple)`, `PropDrops(theme)`, `BossEcoChance(owned)`.
- `UpgradeCosts` (arma, Eco, infusão), `SellInfo` (sobras), `UsedMaterials`.
- `SeasonCalendar`: `SeasonOf(day)`, relógio/novo dia.
- `BrechaRules`: rank da Brecha, timer, permissão de entrada (`rk <= rankIdx+1`), geração de ondas, contagem de invasão, bônus de chefe.
- `LanternRules`: estado `Lost`, penalidades (×0.6 HP, ×0.45 luz), resgate.
- `SaveData` (POCO) + `SaveSerializer` (JSON compatível com `v:1`, defaults de `loadSave`).
- **Máquinas de estado de IA** como classes puras (`LungerAi`, `CasterAi`, `SpitterAi`, `BruteAi`, `AnglerAi`, `ReaperAi`, `KnightAi`) recebendo `dt`, posição relativa ao jogador e flags de partes, e emitindo *comandos* (mover vetor X, disparar projétil Y, aplicar dano em área Z, invocar). O nó Godot só executa os comandos. Isso permite testar timings.
- `PlayerCombatState`: cooldowns, combo, carga do arco, dash/energia, i-frames, Essência, Eco/Supremo cooldowns.

Presentation (Godot): `CharacterBody2D`/movimento e colisão (substituir `moveEnt`/máscara por TileMap/NavigationPolygon ou máscara importada), `Area2D`/`ShapeCast2D` para hitboxes, `AnimatedSprite2D`, partículas `GPUParticles2D`, `Camera2D` com shake, hitstop via `Engine.TimeScale` local ou escala de `dt` nos sistemas de combate, `PointLight2D` (luz ×`light`), áudio, todas as telas em `Control`/`CanvasLayer` (tema com Cinzel/Pixelify, `StyleBoxFlat` replicando o `panel`).

### 6.3 Números/fórmulas que devem ter teste unitário

1. `RankIndex`: 0→E, 99→E, 100→D, 259→D, 260→C, 7499→SSSS, 7500→ALVOR.
2. `LevelOf`: tabela §1.3 (ex.: 87 XP → Nv3 cur 0 need 55; 6893 → Nv22).
3. HP base: E/Nv1=100; xp=100 → 124; xp=7500 → 334; com `lost` → `round(hp*0.6)` (100→60, 124→74).
4. ATK de arma por nível (§1.6) e adagas com `atkB` ×0.5 (dag +1 com luva atk 3 → `round(6+1.5)=8`).
5. Defesa: `def=6, dmg=12, var=1` → 11; teto 60% (`def=80` → ×0.4); mínimo 1.
6. Dano no mob: crit ×1.6; costas ×2 (adagas) só com `side=back`; fraqueza ×1.5; resistência ×0.6; combinação multiplicativa; `max(1, …)`.
7. Lado: `(src.x-m.x)*face >= 0` → front (incluindo igualdade).
8. Parte atingida: primeira não quebrada por `side`/`any` na ordem do array (Colosso de frente com furnace quebrada → pipes).
9. Knockback: elite ×0.35; chefe 0.
10. Essência por acerto `3.2*ess`, teto 100; dummies não contam.
11. Combo da Lâmina: sequência de 3 com janela 0.6 s; golpe 3 ×1.6 dano, ×1.15 arco, ×1.2 alcance, ×1.5 cd.
12. Arco: dano `round(atk*(0.8+1.6c))`, velocidade `280+240c`, perfuração em `c>=0.85`, carga máx. 0.9.
13. Teste de hitbox melee (setor + regra `d<4`).
14. Escala de mob por rank/estação (§3.9).
15. `priceMult`/`costLumes` com arredondamento JS (30 em invasão = 41; 22 normal = 20; 6 normal = 5).
16. Custos: upgrade de arma por nível (cajado ×2 mat), afinação de Eco (70/4, 130/6), `ecoCost` (−10 por nível).
17. `rollGear` pesos (comum 3, incomum 2, raro 0.6/3; exclui lant0) — testar com RNG fixo.
18. Drops de kill/baú/prop (probabilidades .7, .3/.85, .35, .45/.75/.82).
19. Chance de Eco: chefe 0.75 se não possuído, 0 se possuído; wolfE 0.25.
20. Poção: cura `round(max*0.45)`, cd 1 s, bloqueios.
21. Dash: custo 22, energia regen 30/s, dashT 0.17, dashCd 0.32, imunidade.
22. i-frames 0.75 s após dano; projétil persiste se jogador invulnerável.
23. `seasonOf(day)` para dias 1, 3, 4, 7, 10, 13.
24. Respawn: em Brecha (lost + bolsa preservada) vs fora (−10 Lumes, HP 50%).
25. Brecha: rank `clamp(max(minRk, rankIdx)+bônus, 0, 9)`, permissão `rk <= rankIdx+1`, bônus do chefe `40+25*rk`, invasão `5+min(4,rk)` (+1 Alfa se >4).
26. Ondas: fácil 2 ondas (2, 3); normal `2+(rk>=1)` ondas com `3+i+RI(0,1)` (+extra em i>0, +elite 50% na última).
27. Timings das IAs (por estado, §3.10) — ex.: lunger acerta exatamente uma vez por investida; Ceifador `tele` → `swipe` com `t=0.15`.
28. Supremos: dano por fórmula (§3.8), cooldown inicial 6 s, cd por classe.
29. Ímã: atração só após 0.45 s e <48 px; coleta <9 px; `needE` não atraído.
30. Bloqueio de equipamento por classe e Selo mínimo (`RAR_RK`).
