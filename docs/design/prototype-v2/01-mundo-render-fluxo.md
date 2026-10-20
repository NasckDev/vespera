# Protótipo Véspera v2 — Mundo, renderização e fluxo

> Spec de engenharia reversa do protótipo HTML/JS "Véspera v2" para reimplementação fiel em Godot 4.7 C#.
> Escopo: `v2/world.js`, `v2/render.js`, `v2/main.js`, `v2/extra.js` (com `v2/data.js` e trechos de `v2/core.js`/`v2/combat.js` onde necessário).
> Citações no formato `arquivo:linha` referem-se à pasta `v2/` do protótipo. O protótipo **não foi executado**; tudo vem da leitura do código e da inspeção dos PNG/JSON.

Convenções:

- **Unidade de mundo (u)** = 1 pixel da imagem do mapa (o mapa é desenhado em `(0,0)` com seu tamanho natural, `render.js:27`).
- Retângulo `[x, y, w, h]` (canto superior esquerdo + tamanho); círculo `{c:[cx, cy, r]}` (`core.js:240`).
- Tempo em segundos reais; `dt` limitado a 0,05 s (`main.js:18`).
- Valores aleatórios: `R(a,b)` = float uniforme em [a,b); `RI(a,b)` = inteiro uniforme em [a,b] (`core.js:11-12`).

---

## 0. Ordem de carga e overrides

1. `Vespera v2.dc.html` carrega `v2/data.js, core.js, combat.js, world.js, render.js, ui.js, main.js, extra.js` em sequência (`async=false`) e chama `window.Vespera.start(canvas)`.
2. `extra.js` é carregado por último e **sobrescreve** várias funções (declarações globais redefinidas ou wrappers `f = (function(orig){...})(f)`). O comportamento efetivo é sempre o de `extra.js`:

| Função | Original | Override em `extra.js` | Tipo |
|---|---|---|---|
| `drawAir` | `render.js:189` | `extra.js:67-72` | wrapper (desenha flechas/alvos do Supremo) |
| `itemInfo`, `equipGear` | `ui.js` | `extra.js:84-88` | wrapper (travas de classe/Selo) |
| `switchWeapon` | `main.js:173` | `extra.js:90-93` | substitui (só a arma da classe) |
| `smithUnlock` | `world.js:241` | `extra.js:94` | substitui (Brás não forja arma de outra classe) |
| `portalDialog` | `world.js:218` | `extra.js:97` | wrapper (regra de Selo +1) |
| `hitMob`, `breakPart`, `enterBrecha`, `bossDefeated`, `talkClerk` | `combat.js`/`world.js` | `extra.js:124-128` | wrappers (contadores de missão) |
| `tutorial` | `main.js:177` | `extra.js:129-136` | substitui |
| `drawHUD` | `render.js:233` | `extra.js:139-158` | wrapper |
| `update` | `main.js:78` | `extra.js:160-167` | wrapper |
| `uiFrame` | `ui.js:3` | `extra.js:242` | wrapper (telas novas) |
| `uiPause` | `ui.js:236` | `extra.js:245-258` | substitui |
| `toTitle`, `titleFrame`, `classFrame` | `main.js:29,30,129` | `extra.js:261,262,300` | substitui |

**Diferenças v1 (arquivos na raiz) → v2:** `data.js`, `core.js`, `combat.js`, `world.js`, `main.js` são byte-a-byte idênticos; `render.js` difere só na linha 253 (dicas do HUD: v1 `Tab/Esc Controles/Enter`; v2 `Tab/Esc Menu/F Supremo/Enter`); `ui.js` difere a partir da linha 174 (não analisado, fora do escopo); v1 não tem `extra.js` (sem classes fixas, Supremo, missões, menus novos).

---

## 1. Game loop e fluxo de cenas/estados

### 1.1 Loop

- `startV(canvas)` (`main.js:5-12`): pega contexto 2D, `bindInput()` uma vez, `resize()`, desenha "Acendendo as lanternas…" e aguarda `loadAll()` (`core.js:22-37`). Depois `toTitle()` se não houver estado, e inicia `requestAnimationFrame`.
- `frame(now)` (`main.js:16-26`):
  1. `dt = min(0.05, (now-lastT)/1000)`.
  2. Se o tamanho CSS do canvas ou o `devicePixelRatio` mudou → `resize()`.
  3. `if(!G) titleFrame(dt); else { update(dt); render(dt); }` (erros são capturados e logados no máximo 5 vezes).
  4. `clearInput()` (zera teclas "pressionadas neste frame", clique, roda, texto digitado — `core.js:204`).
- **Estado global**: `T` = estado do título (quando `G===null`); `G` = estado do jogo. Não há "cenas"; tudo é estado + `G.ui` (modal ativo).

### 1.2 Ordem do `update(dt)` (efetivo = `main.js:78-113` + wrapper `extra.js:160-167`)

1. Recalcula escala `SC` (ver §3) — `main.js:79`.
2. `G.t += dt`; `G.st = stats()`.
3. Fade: se `fadeTo > fade` → `fade += dt*3.5` (≈0,29 s até preto); se `fadeTo < fade` → `fade -= dt*3` (≈0,33 s). Ao atingir `fade>=1` chama `fadeCb` uma vez (`main.js:81-82`).
4. Toasts (fila, 1 por vez), notas (vida 4 s), idade do chat, `redT`, `charmed`, `locT`, `bossIntro.t`, `rankUp.t`.
5. Shake: `shake *= 0.002^dt`, zera abaixo de 0,2 (`main.js:88`).
6. Tutorial agendado (`tutT`) dispara quando não há UI.
7. **Se `G.ui` existe → retorna** (mundo pausado durante qualquer modal) (`main.js:91`). No primeiro frame após fechar UI, `releaseKeys()`.
8. Chat: digitando → `chatInput()`; senão `Enter` abre o chat (só sem fade).
9. Entradas globais (sem fade, vivo, sem chat): `Tab/I` inventário, `Esc` pausa, `M` mudo, `1-5` arma, roda do mouse, `E` interage com `nearestInteract()` (`main.js:96-103`).
10. `wdt = hitstop>0 ? dt*0.08 : dt` (câmera lenta de impacto) — `main.js:104`.
11. `updPlayer(wdt)`, mobs, remoção de mortos (`deadT` < 1,6 s; chefe < 3,2 s), `updProjs/updFx/updPicks(wdt)`, `updHunters(dt)`, `updNpcs(dt)`, falas aleatórias de caçadores (prob. `dt*0.04` por caçador, `main.js:109`).
12. **Saídas de mapa**: se `map.exits` e sem fade e vivo → para cada saída, se o ponto do jogador está dentro de `e.r` → `goMap(e.to, e.at)` (`main.js:110`).
13. `updRun(wdt)`, `updWorld(dt)`.
14. Câmera: `cam += (alvo-cam) * min(1, dt*7)` (`main.js:112`).
15. Wrapper `extra.js`: força `s.cur = s.cls`; avança `ultBan`; se `OPT.shake=false` zera shake; se UI/chat → retorna; `updUlt`, `updUf`; `F`/`V` → `tryUlt()`; `updQuests()`.

### 1.3 Ordem do `render(dt)` (`main.js:185-193`)

`drawWorld()` (espaço de mundo + iluminação) → `drawOverlayWorld()` (textos/barras sobre entidades em espaço de tela) → `drawHUD()` (+ extra) → `uiFrame(dt)` (modais) → fade `rgba(8,6,14,fade)` em tela cheia.

### 1.4 Máquina de estados de alto nível

```
[Carregando] -> [Título T]
  Título (extra.js:262-299): menu
    Continuar (se save) ----------------------------> startGame(save,false) -> Guilda (250,330)
    Novo jogo -> (se save: confirmação "Começar do zero?") -> História (5 páginas) -> Escolha de classe -> startGame(newSave,true) -> Guilda (250,560)
    Multijogador / Como jogar / Controles / Opções -> sub-tela (T.sub) -> volta
  Jogo G:
    Guilda <-> Rua da Forja <-> Estalagem
    Rua da Forja -> (placa "Rua dos Ofícios") -> 4 lojas -> Rua da Forja (305,790)
    Rua da Forja -> (portal de Brecha) -> Dungeon do tema (sala "theme") -> porta -> Câmara do Núcleo (sala "core") -> chefe -> portal de saída -> Rua da Forja
                    (Catedral: chefe na própria sala, sem Núcleo)
    Morte -> tela de morte -> Guilda (se na Brecha) ou Estalagem (se na cidade)
    Pausa (Esc) -> Diário / Como jogar / Controles / Multijogador / Opções / Salvar / Voltar ao menu (toTitle)
```

### 1.5 Título (efetivo: `extra.js:261-299`)

- `toTitle()`: `G=null`, lê save, `T={sel:0,t:0,name:save?.name||'Rui',save,embers:[],sub:null}`.
- Fundo: `keyart.png` em "cover" (`k=max(W/iw,H/ih)`), deslocamento horizontal `sin(t*0.15)*6u`; smoothing só se `k<1`; gradiente horizontal escuro `rgba(10,8,18,.92)→0` em 55% da largura; 60 brasas aditivas (cores `#F2A541`, `#E64A8E`, `#ffcf8a`, sobem `R(10,35)*u` px/s, alpha `0.5+0.5 sin(ph*3)`).
- Subtítulo "Cada véspera, uma Brecha. Cada Brecha, uma memória." em `(60u, max(40u,0.3H)-26u)`; rodapé "v0.2 · protótipo". **`logo.png` não é desenhado no título v2** (só no cabeçalho do inventário, `ui.js:110`).
- Itens: `Continuar` (se save; mostra Dia/Selo/nome/classe), `Novo jogo`, `Multijogador`, `Como jogar`, `Controles`, `Opções`. Navegação `↑/↓/W/S`, confirmar `Enter/NumpadEnter/E` ou clique.
- História (`main.js:150-171`): 5 páginas (`STORY`), painel com `lumeira_map.png` (452×392) à esquerda; avança com `Enter/Space/E/→/D`/clique (após 0,15 s), volta `←/A`, `Esc` pula.
- Classe (`extra.js:300-324`): campo "Nome" (até 14 caracteres, letras/números/espaço/`'`/`-`), 5 cartões (Lâmina, Arco, Cajado, Martelo, Adagas), sprite alterna `_front`/`_atk` a 1,2 Hz no selecionado; `←/→`, `Enter`, `Esc` volta. Cria `newSave(nome, classe)` com `qi=0, qc={}`.

### 1.6 Início de jogo (`main.js:64-77`)

- Se novo: apaga `localStorage['vespera_save_v1']`.
- `G` inicial: `fade=1, fadeTo=0` (abre do preto), `nextBrecha = novo?16:6` s, `chatT=7`.
- `goMap('guild', 250, novo?560:330, instant=true)`.
- Se `s.lost` (lanterna perdida no save) → `rescueAt = 20` (resgate automático 20 s após carregar).
- 3 mensagens de chat fixas; se novo, `tutT=0.9` → `tutorial()` (3 diálogos de Odete, `extra.js:129-136`).

### 1.7 Transição de mapa — `goMap(id, x, y, instant, cb)` (`world.js:2-10`)

`doIt`: troca `mapId/map`, posiciona jogador, zera knockback/puxão/carga, **limpa** `mobs, projs, fx, floats, picks, props, hunters, exitPortal, weather`, chama `setupMap()`, `cb()`, `snapCam()` (câmera pula direto), `locT=0` (mostra nome do lugar por 3 s, `render.js:307`), `releaseKeys()`.

- `instant`: executa já e faz só fade-in (`fade=1 → 0`).
- normal: `fadeTo=1`; quando `fade>=1` executa `doIt` e `fadeTo=0` (fade-out ≈0,29 s + fade-in ≈0,33 s). Durante `fadeTo>0` o input de movimento é bloqueado (`combat.js:60`) e saídas não são testadas.
- Não há cooldown de saída: os pontos de chegada ficam fora dos retângulos de saída do mapa de destino (verificado na §2).

### 1.8 `setupMap()` (`world.js:11-22`)

1. Guilda: cria 4 bonecos de treino (`dummy:true`, `r:8`, `hh:22`) em `(155,455) (335,455) (170,545) (320,545)` — invisíveis (a arte está pintada no `map_guild.png`; `drawWorld` pula `dummy`, `render.js:36`).
2. Mapas não-dungeon: caçadores ambiente (`G.hunters`): lojas 0; guilda 1 se `s.closed>0` senão 0; demais `RI(1,2)`. Nomes de `D.HUNTERS` excluindo quem usa a mesma arma do jogador; posição `randPoint(70)` (≥70 u do jogador).
3. NPCs com horário: `hh = s.clock/60` (horas, **sem** módulo 24); entra se `n.map===id && hh>=h[0] && hh<h[1]`. Posição inicial = ponto aleatório do `path`; índice alvo `i` sorteado **independentemente** (`world.js:19`). Avaliado só ao entrar no mapa.
4. Rua da Forja com invasão ativa → `spawnInvasion()`.
5. Dungeon com `G.run` → `setupDungeon()`.

### 1.9 Brechas (dungeons) — fluxo completo

- **Abertura** (`world.js:34-48`, chamada por `updWorld` quando `nextBrecha<=0`; depois `nextBrecha=R(80,120)`):
  - máx. 2 abertas; `spot` = 0 ou 1 (`PORTAL_SPOTS = [[440,480],[1174,534]]`, `data.js:98`).
  - tema: a 1ª da sessão (`G.firstDone` não é salvo) é sempre `fish` com `t=300 s`; demais: tema com `minRk <= rankIdx()+1`, evitando temas já abertos; `t=R(210,270)`.
  - rank `rk = clamp(max(minRk, rankIdx) + (25%: +1), 0, 9)` (1ª: +0).
  - `npcAt`: 55% (não-1ª) → `R(25,120)`: quando `b.t <= npcAt` uma Casa NPC fecha a Brecha.
  - toast + `sfx('portal')` + chat.
- **Timer** (`world.js:146-151`): `b.t -= dt` só para Brechas abertas em que o jogador não está. `t<=0` → `rupture(b)`.
- **Ruptura** (`world.js:49-55`): `G.invasion={b, left: 5+min(4,rk)}`; shake 8; preços ×1,5 (`core.js:233`); se na Rua da Forja, spawna já. `spawnInvasion` (`world.js:56-60`): `left` mobs do tema em `randPoint(110)` com teleporte `R(0.4,1.4)` s; se `n>4` + 1 `wolfE` (Alfa). Resolve quando todos os `inv` morrem (`world.js:156`): +30 Lumes, +30 XP, `closed++`, save.
- **Entrada** (`world.js:75-82`): `G.run={b, room:'theme', bag:{mats,gear,lumes,lanterns}, ...}`; `goMap(THEMES[b.theme].map, spawn)`.
  - Temas → mapa (`data.js:107-113`): `fish→fish2`, `crypt→crypt2`, `mines→mine`, `garden→garden`, `cathedral→cath`.
- **Sala "theme"** (`world.js:85-96`):
  - Ondas: fácil (`s.closed===0`): 2 ondas de 2 e 3 mobs; normal: `2 + (rk>=1?1:0)` ondas, onda `i` com `3+i+RI(0,1)` mobs sorteados por peso de `th.mobs`; ondas `i>0` + `th.extra`; última onda 50% + `th.elite`.
  - 1ª onda após `waveT = fácil?4:2.5` s; entre ondas 1,6 s. Spawn por `randPoint(130,{r:9, y0:max(120,p.y-300), y1:min(1000,p.y+120)})` com teleporte `0.8+i*0.15` s (`world.js:108`).
  - Props: `RI(9,13)` de `th.props`, evitando spawn (60 u), porta (50 u, centro `door.x+w/2, door.y+20`), `bossHere` (90 u) e entre si (22 u). Quebráveis/sólidos (`r=7`): `barrel, crate, crate2, sack, urn`.
  - Baús: `1 + (Primavera?1:0) + (30%?1:0)`; 25% roxo (`pr_chest3`, aberto `pr_chest4`), senão `pr_chest1` (aberto: alpha 0,55); `r=8`, sólidos; distância 30 u entre si.
  - 45%: lanterna perdida de outro caçador (pick `needE`, "Pegar a Lanterna de X").
- **Fim das ondas** (`world.js:110`): se o mapa tem `bossHere` (só `cath`) → `room='core'` na própria sala, chefe em 2,2 s; senão "A passagem se abriu" e a **porta** (`map.door`) fica brilhando (luz âmbar 70 em `door.x+w/2, door.y+10`, `render.js:63`). Antes de limpar, a porta é um retângulo magenta pulsante com partículas (`render.js:113`).
- **Porta** (`world.js:114`): jogador dentro de `map.door`, sala theme limpa → `room='core'`, `goMap('core',250,960)`.
- **Sala "core"** (`world.js:98-99,113`): `bossT=1.8` s; na `core`, 4 cristais decorativos (`pr_crys1/2`) em `(105,350) (395,360) (130,705) (380,685)` + `R(-15,15)` em x e +30 em y. Chefe em `map.bossHere || [250,500]`; `bossIntro` 3,2 s; shake 8; câmera puxa para o chefe (§3).
- **Chefe derrotado** (`world.js:117-122`): `closed++`, bônus `40+25*rk` Lumes na bolsa, +60 XP; após 1400 ms cria `exitPortal` em `map.exitAt || [250,228]` (interação r 26 "Voltar a Lumeira").
- **Saída normal** `finishRun()` (`world.js:123-128`): junta bolsa ao save, `nextBrecha=min(next,R(30,50))`, `goMap('forge', spot.x, spot.y+24)`.
- **Fuga** (`world.js:115,130-134`): jogador **em movimento** dentro de `map.back` → empurra 14 u para cima (`p.y-=14` e ainda `moveEnt(p,0,-14)`) e abre diálogo "Fugir para Lumeira" (leva a bolsa; Brecha continua aberta) / "Continuar lutando".
- **Regra v2** (`extra.js:97`): se `b.rk > rankIdx()+1` o portal recusa a entrada.

### 1.10 Morte

- `killPlayer` (`combat.js:25-28`): `dead=true`, shake 9, hitstop 0,15. Após `deadT>2.4` s → `G.ui={type:'death'}` (`combat.js:55`).
- Tela (`ui.js:252-260`): após 1,2 s, `Enter/E/Space`/clique → `respawnAfterDeath()` (`combat.js:29-41`):
  - **Na Brecha**: `s.lost={b:id, bairro, bag:run.bag, map, room, x, y}`; `rescueAt = t + R(55,85)`; HP cheio (máx. reduzido a 60% e luz ×0,45 enquanto `lost`, `core.js:228`); `goMap('guild',250,330)`.
  - **Na cidade**: HP 50%, −10 Lumes, `goMap('inn',250,520)`.
- Recuperar a lanterna: pegar `mylantern` no mesmo lugar (mesma Brecha e sala, `world.js:101`), resgate automático em `rescueAt`, resgate pago com Odete (40 Lumes → `rescueAt=t+12`), ou fechamento da Brecha por NPC/jogador (`returnLostIfHere`).

### 1.11 Persistência

- Save: `localStorage['vespera_save_v1']` = `JSON.stringify(G.s)` (`core.js:9,218`); `loadSave` exige `v===1` (`core.js:213`).
- Conteúdo inicial (`core.js:209-212`): `name, xp:0, lumes:60, mats:{}, gear:['capuz','luva'], equip:{lanterna:'lant0', peito:'couraca'}, weapons, cur, ecos, ecoEq, ecoLv, elems, elem, potions:3, lanterns:[], day:1, clock:16*60+30, lost:null, tut:0, closed:0` + v2 `cls, qi, qc`.
- **Não** salvo: mapa atual (Continuar sempre começa na Guilda em (250,330)), Brechas abertas, invasão, `firstDone`, posição.
- Quando salva: início de jogo, `newDay`, invasão resolvida, resgate, `finishRun`, fuga, compras/forja, `tryUlt` (`extra.js:29`), missão cumprida, autosave a cada 45 s (`world.js:155`), pausa → "Salvar jogo"/"Voltar ao menu".
- Opções: `localStorage['vespera_opt']` = `{shake:true, hints:true}` (`extra.js:2-4`). `muted` (som) **não** é persistido.

---

## 2. Mapas

### 2.1 Tabela geral

| id | imagem (px = u) | máscara | flags | nome exibido | spawn | `data.js` |
|---|---|---|---|---|---|---|
| `guild` | `map_guild.png` 500×896 | — (retângulos) | indoor | Guilda dos Lanterneiros | (250,560) | 7-12 |
| `forge` | `map_city.png` 1672×941 | `mask_city.png` | hd, outdoor* | Rua da Forja | (1500,250) | 13-17 |
| `inn` | `map_inn.png` 498×896 | — | indoor | Estalagem Repouso da Lanterna | (250,520) | 18-23 |
| `fish2` | `map_fish2.png` 1672×941 | `mask_fish2.png` | hd, dungeon, theme `fish` | Mercado de Peixes | (830,870) | 37-38 |
| `crypt2` | `map_crypt2.png` 1672×941 | `mask_crypt2.png` | hd, dungeon, theme `crypt` | Cemitério das Velas | (835,895) | 39-40 |
| `mine` | `map_mine.png` 1672×941 | `mask_mine.png` | hd, dungeon, theme `mines` | Minas da Forja | (330,770) | 41-42 |
| `garden` | `map_garden.png` 1672×941 | `mask_garden.png` | hd, dungeon, theme `garden` | Estufa Silvestre | (1040,895) | 43-44 |
| `cath` | `map_cath.png` 1672×941 | `mask_cath.png` | hd, dungeon, theme `cathedral` | Catedral do Arauto | (835,905) | 45-46 |
| `core` | `map_core.png` 496×1014 | — | dungeon, theme `core` | Câmara do Núcleo | (250,960) | 47-52 |
| `shop_arm` | `shop_arm.png` 696×408 | — | indoor, shop | Armaria Rubra | (352,370) | 53-58 |
| `shop_mag` | `shop_mag.png` 694×408 | — | indoor, shop | Casa dos Feitiços | (347,370) | 59-64 |
| `shop_alq` | `shop_alq.png` 696×434 | — | indoor, shop | Alquimia do Ivo | (360,395) | 65-70 |
| `shop_rel` | `shop_rel.png` 694×434 | — | indoor, shop | Oficina de Relíquias & Lanternas | (350,395) | 71-76 |
| `fish` (legado) | `map_fish.png` 496×1014 | — | dungeon | Mercado de Peixes | (265,960) | 24-30 |
| `crypt` (legado) | `map_crypt.png` 456×1014 | — | dungeon | Cemitério | (228,960) | 31-36 |

\* `outdoor` não é lido por nenhum código v2. `hd` afeta escala da câmera (§3) e escuridão (§4.6).

- **Legados `fish`/`crypt`**: definidos em `data.js` mas `map_fish`/`map_crypt` **não são carregados** em `loadAll` (`core.js:25`) e nenhum tema aponta para eles. Só eles usam `fast` (zonas sem lentidão de "água", `combat.js:88`). Não portar (ou portar só como referência).
- **`map_forge.png` (450×896)** não é referenciado no v2 (a Rua da Forja usa `map_city`). Resto da v1.
- **Rótulo do mapa** (`render.js:313`): dentro de Brecha na sala theme mostra `THEMES[theme].bairro`; senão `map.name`.

### 2.2 Variantes `*_orig` vs. sem sufixo

`map_core, map_crypt, map_fish, map_forge, map_guild, map_inn, shop_alq, shop_arm, shop_mag, shop_rel` têm `_orig`. Comparação por pixel (Pillow): mesmas dimensões, alpha 255 em tudo, **média de cor idêntica** (±0,3), desvio-padrão 3–5% maior na versão sem sufixo, diferença média ≈3/255 por canal e <0,1% dos pixels com diferença >30. Conclusão: a versão sem sufixo é a `_orig` com **nitidez/contraste local aumentado** (unsharp). Nenhum `_orig` é referenciado pelo código; usar a versão sem sufixo.

### 2.3 Máscaras de colisão (mapas `hd`)

- Formato: PNG 209×117 RGBA, só 2 cores: `#000000` e `#FFFFFF` (verificado nas 6 máscaras). **Branco = andável**, preto = bloqueado. Sem cores especiais.
- Amostragem (`core.js:39,241`): `MASK.d[i] = R > 127 ? 1 : 0`; `cs = 8` (fixo). Célula = `(floor(x/8), floor(y/8))`; fora da grade = bloqueado.
- Cobertura: 209×8 = 1672 u (largura exata); 117×8 = **936** u → as últimas 5 linhas da imagem (y 936–940) ficam fora da máscara = bloqueadas.
- Células andáveis: city 5130, fish2 5044, crypt2 7598, mine 7676, garden 6329, cath 9235 (de 24453).
- Nos mapas com máscara, `walk` e `block` estão vazios (`data.js:13-14,37-46`); props sólidos ainda bloqueiam (§5.4).

### 2.4 Colisão em mapas sem máscara (guild, inn, core, lojas)

`inWalk(x,y)` = ponto dentro de **algum** `walk` (retângulo ou círculo); `walkable` = `inWalk` **e** fora de todos os `block` **e** fora de props sólidos (`core.js:241-247`). Os arrays completos estão no JSON da §9.3. Observações:

- Os `block` `[149,446,12,14] [329,446,12,14] [164,536,12,14] [314,536,12,14]` da Guilda são os bonecos de treino (pintados na arte).
- `core` usa um círculo `{c:[250,505,142]}` (arena central) + corredores.
- Lojas: o corredor de saída é o retângulo inferior de `walk` (ex. `[300,300,105,108]` na Armaria); a saída fica no seu fim.

### 2.5 Saídas e chegadas

| Mapa | Retângulo de saída | Destino | Chegada | Linha |
|---|---|---|---|---|
| guild | `[200,872,100,24]` | forge | (1500,250) | `data.js:10` |
| forge | `[250,268,40,25]` (porta da estalagem) | inn | (250,760) | `data.js:15` |
| forge | `[1460,128,80,22]` (porta da guilda) | guild | (250,840) | `data.js:15` |
| inn | `[200,815,100,15]` | forge | (268,325) | `data.js:21` |
| shop_arm | `[300,394,105,14]` | forge | (305,790) | `data.js:56` |
| shop_mag | `[300,394,95,14]` | forge | (305,790) | `data.js:62` |
| shop_alq | `[305,420,110,14]` | forge | (305,790) | `data.js:68` |
| shop_rel | `[300,420,100,14]` | forge | (305,790) | `data.js:74` |

Entradas por interação/script (sem retângulo):

| Origem | Gatilho | Destino |
|---|---|---|
| forge | placa "Rua dos Ofícios" (305,765) r34 → escolher loja | spawn da loja (`world.js:274`) |
| forge | portal de Brecha (spot) | spawn do mapa do tema (`world.js:79`) |
| dungeon theme | dentro de `door` após limpar | core (250,960) (`world.js:114`) |
| dungeon | dentro de `back`, movendo-se | diálogo de fuga → forge `spot+(0,24)` (`world.js:132`) |
| core/cath | portal de saída `exitAt` r26 | forge `spot+(0,24)` (`world.js:126`) |
| qualquer | morte | guild (250,330) ou inn (250,520) |
| inn | dormir | não troca de mapa; fade + `newDay` (`world.js:267`) |

Portas/fugas das dungeons:

| Mapa | `door` | `back` | outros |
|---|---|---|---|
| fish2 | `[740,60,120,40]` | `[790,890,90,30]` | — |
| crypt2 | `[180,110,60,40]` | `[790,915,100,25]` | — |
| mine | `[190,70,50,40]` | `[300,790,80,20]` | — |
| garden | `[1090,100,90,30]` | `[990,915,110,25]` | — |
| cath | — | `[790,920,90,20]` | `bossHere (835,380)`, `exitAt (835,330)` |
| core | — | `[213,1000,74,14]` | chefe padrão (250,500), saída padrão (250,228) |

**Verificação contra as máscaras** (script próprio, mesma regra `canStand` r=4): todos os spawns, chegadas, `bossHere`, `exitAt`, Forja do Brás, placa dos Ofícios e saídas da forge são andáveis. Problemas de dado encontrados:

- Volta da Brecha no spot 1 cai em **(1174,558), célula bloqueada** (`world.js:126` + `data.js:98`). O jogador consegue sair porque `moveEnt` só testa o destino.
- Spots dos portais (440,480) e (1174,534): o ponto é andável mas `canStand` (4 sondas) falha — irrelevante, o portal não colide.
- Vários waypoints de NPC da forge caem em células bloqueadas (ver §6.2) — os NPCs atravessam paredes por design de fallback.
- `door` das dungeons cobre parcialmente células bloqueadas (fish2 122/341 amostras andáveis etc.) — o teste é de ponto, então basta a parte andável.

### 2.6 Interagíveis (`world.js:182-197`)

Escolha: o mais próximo com `distância < r` do ponto do jogador; prompt "[E] rótulo" desenhado em `(x, y-44)` (`render.js:228-229`). Só quando `!G.ui && !dead && fadeTo===0`.

| Mapa | Rótulo | (x,y) | r | Ação |
|---|---|---|---|---|
| guild | Falar com Odete | (247,250) | 32 | `talkClerk` (entregar lanternas, resgate pago, explicações) |
| guild | Mural de Contratos | (400,228) | 36 | `openBoard` (`G.ui={type:'board'}`) |
| forge | Forja do Brás | (1150,265) | 50 | `talkSmith` (bloqueado durante invasão) |
| forge | Rua dos Ofícios | (305,765) | 34 | `oficiosDialog` (lista 4 lojas) |
| forge | Brecha · {bairro} | PORTAL_SPOTS[spot] = (440,480)/(1174,534) | 24 | `portalDialog(b)` (só Brechas `open`) |
| inn | Falar com Lúcia | (385,446) | 38 | `talkInn` (poção 30, dormir 10, boato) |
| inn | Descansar | (238,168) | 28 | `restDialog` (não funciona em run) |
| shop_arm | Falar com Gaspar | (510,206) | 38 | infusões elementais |
| shop_mag | Falar com Vesna | (330,226) | 36 | afinar Ecos / Diário da Brecha |
| shop_alq | Falar com Ivo | (355,236) | 36 | poções |
| shop_rel | Falar com Abel | (335,250) | 36 | lanternas/relíquias |
| qualquer | Falar com {NPC} | posição do NPC | 22 | diálogo com fala cíclica (`c.line++`), NPC para 4 s |
| dungeon | Voltar a Lumeira | `exitPortal` | 26 | `finishRun` |
| dungeon | Pegar a Lanterna de X / Recuperar sua Lanterna | pick `needE` | 18 | `collect` |
| dungeon | Abrir baú | baú fechado | 20 | `openChest` |

Os vendedores das lojas, Brás e Lúcia **não são entidades**: estão pintados na arte. Só Odete é desenhada como sprite (§4.3).

### 2.7 Guilda — detalhes de arte

- `patchGuild()` (`core.js:40`): ao carregar, copia o retângulo `(198,146,34,40)` para `(229,146)` no `map_guild` — apaga a recepcionista pintada para dar lugar ao sprite animado.
- `drawClerk` (`render.js:104-108`): `npc_odete_f` (ou `npc_odete_a` quando `sin(t*0.35) > 0.85`) em `(246, 193 + sin(t*1.6)*0.4)` com escala 1,12; depois redesenha a fatia do mapa `(158,186,168,44)` por cima (o balcão cobre as pernas).

---

## 3. Câmera, resolução e filtragem

- Canvas: CSS 100%×100%; `DPR = min(2.5, devicePixelRatio)`; `W = max(320, cssW*DPR)`, `H = max(240, cssH*DPR)` (`core.js:47-49`).
- Escala de UI `U = clamp(min(cssW/1280, cssH/760), 0.78, 1.6) * DPR` (`core.js:51`). Todo HUD usa múltiplos de `U`.
- **Escala do mundo `SC`** (device px por u), recalculada todo frame em jogo (`main.js:79`): `vh = map.hd ? 400 : 300`; `SC = max(1.2, H/vh)`; se `W/SC < 380` → `SC = max(1, W/380)`. Ou seja: **altura visível = 400 u em mapas hd, 300 u nos demais; largura visível mínima 380 u**. `SC` não é inteiro. (O `SC` de `resize()` em `core.js:50` só vale antes do primeiro `update`.)
- **Alvo da câmera** (`world.js:24-30`): foco `(p.x, p.y-14)`; se há chefe vivo, `fx = lerp(fx, boss.x, 0.3)`, `fy = lerp(fy, boss.y-30, 0.35)`. Por eixo: se a imagem é menor que a vista, centraliza (`(im-v)/2`, negativo); senão `clamp(f - v/2, 0, im - v)`.
- Seguimento: `cam = lerp(cam, alvo, min(1, dt*7))`; `snapCam` na troca de mapa.
- Transform do mundo: `setTransform(SC,0,0,SC, -round(cam.x*SC + sx), -round(cam.y*SC + sy))` (`render.js:25`) — câmera arredondada ao device px; shake `sx,sy = R(-1,1)*shake` em **device px**.
- **Filtragem**:
  - Mapa: `mapHi(img)` (`core.js:69-89`) gera uma versão 2× (algoritmo tipo Scale2x/EPX com tolerância RGB < 24, seguido de nitidez 3×3 com `a=0.28`), desenhada no tamanho original com `imageSmoothing` **alto** (bilinear). Cache LRU de 3 mapas.
  - Sprites (`core.js:59-67`): posição arredondada ao device px quando sem rotação; smoothing **desligado só se** a escala efetiva for inteira (±0,02) e ≥0,98; caso contrário smoothing alto. Como os sprites têm densidade ~5 px/u, na prática são **reduzidos com filtro bilinear**.
  - Ícones (`icon()`, `core.js:92-96`): sempre suavizados, ajustados para caber em `size`.
  - Fundo do título/história: smoothing só se reduzido (`k<1`); `lumeira_map` sem smoothing.

---

## 4. Renderização

### 4.1 `atlas.json` / `atlas.png`

- `atlas.png` 4096×2015 RGBA. `atlas.json`: objeto `{nome: [x, y, w, h, ax, ay, k]}` com 217 entradas, todas de 7 números:
  - `x, y, w, h`: retângulo no atlas (px).
  - `ax, ay`: **âncora em px do recorte** (pivô). Em todas as 217 entradas `ay == h` → âncora no **pé** (base). `ax` ≈ centro do corpo (não necessariamente `w/2`; ex. `c_sword_atk` 295 px de largura com `ax=159`).
  - `k`: **densidade** (px de atlas por u). Tamanho em mundo = `w/k × h/k`. Valores: 1 (110 entradas: UI, ícones, retratos `look*`, ranks `r_*`), ≈5 (personagens, NPCs, portais, monstros do atlas), 4,1667 (props `pr_*`), 3,333 (`reaperB_*`), 3,125 (`anglerB_*`).
- `spr(n, x, y, o)` (`core.js:59-67`): `s = (o.s||1)/k`; desenha o recorte em `(-ax*s, -ay*s)` com tamanho `(w*s, h*s)` após `translate(x,y)`, `rotate(o.rot)`, `scale(-1,1)` se `o.flip` (espelha em torno da âncora), `scale(o.sx,o.sy)`. `o.a` multiplica alpha; `o.white` usa a cópia branca do atlas (`atlasW`, silhueta para flash, `core.js:31-32`); `o.comp` muda o blend.
- **Overrides de âncora X** `AXO` (em **u**, `core.js:5`): `c_sword_atk 18, c_bow_atk 17, c_staff_atk 21, c_hammer_atk 20, c_dag_atk 22, wolf_a 18, fishm_a 19, anglerB_a 36`.
- Exemplos em u: `c_sword_front` 38,6×32,4; `c_sword_atk` 59×31,8; `shadow` 20,4×5; `lantern` 11,6×20,4; `npc_odete_f` 25,1×32,3; `pr_chest1` 24,7×18,5; `por4` 53,1×83,1.
- Grupos de chaves (217): `c_{sword,bow,staff,hammer,dag}_{front,back,side,atk}` (20); `npc_{bia,bras,faisca,ivo,lauro,marta,odete,vesna}_{f,b,a}` (24); monstros `wolf_*, fishm_*, anglerB_*, beetle_*, golem_*, knight_* (+_s), reaperB_*, spec_*` com `_f/_b/_a/_d`; `por0..por5`; `pr_*` (22: altar, barrel, bones, brazier, candles, chest0-4, cracked, crate, crate2, crys1, crys2, dummy, pillar, sack, sign, skull, tomb, urn); `shadow`, `lantern`; ícones `i_*` (17), `ic_*` (9), `m_*` (10), `eq_*` (23), `el_{arma}_{n}` (22), `ln_*`, `sh_*`, `rl_*`; ranks `r_E…r_ALVOR` (10); `look0-4`; UI `ui_dialog, ui_hpicon, ui_parch, ui_sticon`.
- **`assets/src/` (sprites de monstro e ícones `d_*`) NÃO é carregado pelo v2.** `src/meta.json` = `{nome: [w, h, k]}` (101 entradas; k=3 para monstros, 1 para ícones `d_*`), sem âncora nem posição em atlas (cada um é um PNG solto). Consequência no protótipo: monstros `cnev, coss, cvel, cosr, cceif, mbes, maut, mesc, msen, mcol, gbro, gsei, gmus, gflo, grai, kesc, ksin, ksom, kcav, karr` (temas crypt/mines/garden/cathedral) e os ícones `d_*` dos materiais **não têm entrada no atlas** → `spr()`/`icon()` retornam sem desenhar (`core.js:60,93`). Só `wolf`, `fishm`, `anglerB` (tema fish) aparecem. Para o Godot, esses PNG precisam ser importados; a âncora não está definida — **não está claro** qual pivô usar (assumir base-centro seria suposição).
- `src/old_atlas.png` + `old_meta.json` (`[x,y,w,h,k]`) — versão antiga, não usada.

### 4.2 Camadas e y-sort (`render.js:20-54`)

1. Limpa com `#0b0a14`.
2. Mapa (versão 2×, bilinear).
3. Tinta do tema em `multiply` — **nenhum tema define `tint`** em `data.js`, então é no-op.
4. Invasão na forge: tela inteira do mapa `rgba(230,74,142, 0.06+0.04 sin(3t))`.
5. Guilda: `drawClerk`.
6. `drawGround()`: porta da Brecha (magenta pulsante), **telegraphs** de ataques inimigos (linhas, círculos ×0,72, setores ×0,8 em `rgba(230,74,142)`, pulso `0.55+0.45 sin(18t)`), marcadores de FX no chão.
7. **Lista y-sort** ordenada por `y` crescente: props não quebrados (`y`), picks (`y`), mobs não-dummy (`y`; mortos `y-30` para ficarem atrás), caçadores, NPCs, portais abertos da forge (`spot.y`), portal de saída, jogador.
8. `drawAir()`: projéteis e FX aéreos — sempre por cima de tudo.
9. Iluminação (tela) → glows aditivos (mundo) → tintas de estação (tela) → clima (tela).

### 4.3 Sprites e animação

- **Jogador** (`render.js:157-171`, nomes em `combat.js:113-119`):
  - Sprite: `c_{arma}_atk` atacando/carregando/investida; `c_{arma}_side` no wind-up do martelo; senão `back` (cima), `front` (baixo), `side` (esq/dir). Flip: em ataque olha para a mira (`cos(aim)<0`), senão `dir==='left'`.
  - Bob: andando `-|sin(walkT*11)|*1.6`; parado `sin(t*2.4)*0.35`; carregando arco: 0. Um "squash" `sq` é calculado mas **não é usado** (`render.js:162`).
  - Sombra `shadow` em `(x, y+2)` alpha 0,5. Invulnerável: alpha 0,5 piscando a 9 Hz (`floor(t*18)%2`). Dano: sobreposição branca alpha `hurtT*3`. Rastro (dash/investida): cópias brancas alpha `a*0.35` que somem a `4/s`.
  - Carga do arco: arco de círculo r 5 em `(x, y-38)`, âmbar quando ≥94%.
  - Partícula de elemento: 12%/frame. Orbes do Eco do Arauto: 3 `lantern` (escala 0,7) orbitando `22×12` em `t*3 + i*2.094`.
  - Morto: `c_{arma}_front` girado até −90° em 1/3 s, lanterna ao lado pulsando.
- **Mobs** (`render.js:11-18,145-156`): sufixo `_d` morto; `_s` em estados especiais (summon/rain/burst/chain/tide/lure) se existir; `_a` atacando; `_b` se o jogador está acima (`p.y < m.y-14`) e não idle e existe; senão `_f`. Flip `face<0`. Escala `m.s` (`MON.s`, ex. Alfa 1,4). Bob: voador `sin(bob*3)*2-3`; perseguindo `-|sin(bob*9)|`. Tremor ±0,7 u no wind-up. Sombra `s=r/8` alpha 0,45. Flash branco `flash*6`; brilho branco pulsante durante telegraph. Fade-in no spawn (0,45 s) e fade-out na morte (após 0,9 s; chefe 2,4 s; dura 0,6 s). Elite solta partículas magenta.
- **Caçadores** (`render.js:172-176`): `c_{arma}_{back|front|side}` escala 0,85, alpha 0,92, filtro `saturate(.35) brightness(.78)`; bob `-|sin(walkT*11)|*1.4`.
- **NPCs** (`render.js:177-181`): `npc_{id}_b` subindo, senão `npc_{id}_f`; flip se `left`; bob andando `-|sin(walkT*9)|*1.2`, parado `sin(2t+x)*0.3`.
- **Props** (`render.js:133-136`): sombra escala 0,8 alpha 0,4 + sprite.
- **Picks** (`render.js:137-144`): sombra elipse 4×1,6; bob `needE ? sin(3t)*1.5+2 : sin(5t+x)*0.6` + altura `z` (física de quique em `combat.js:485`); ícone em tamanho fixo (eco 18, gear 13, lume 8, outros 11 u); lanternas usam o sprite `lantern`.
- **Portal** (`render.js:182-188`): `por0` (0–0,25 s), `por1` (–0,5), `por2` (–0,75), depois alterna `por3/por4` a cada 0,28 s (`por5` não usado); glow magenta aditivo elíptico 88×44 alpha `0.5+0.15 sin(3t)`; escala `1+0.03 sin(2.6t)`; partículas 35%/frame.

### 4.4 Partículas e FX

- `parts(x,y,n,cores,o)` (`combat.js:10-13`): cada partícula `{vx,vy}` com ângulo aleatório (ou `o.ang ± o.spr`), velocidade `R(v0=20, v1=90)`, `vy -= up`, vida `R(l0=.25, l1=.6)`, tamanho `R(1,2.2)` u (quadrado), gravidade `g=60`, `glow` = blend aditivo. Física: `vy += g*dt`, `vx *= 0.2^dt` (`combat.js:446`). Desenho: quadrado arredondado ao px, alpha `1-t/max`.
- Tipos de FX desenhados em `render.js:196-207`: `p`, `arc` (golpe), `ring` (onda elíptica ×0,7), `wave`, `chainline`, `lure`, `plure`, `korb`, `shard`. Textos flutuantes sobem 16 u/s (22 se `big`) e vivem 0,8/1,1 s (`combat.js:474`).

### 4.5 Iluminação (`render.js:55-83`)

Formato de luz: `[x, y, raio, tipo, jogador?]`, tipos de cor `LIGHTC` (`data.js:4`): `a` âmbar (242,165,65), `f` fogo (255,120,40), `m` magenta (230,74,142), `c` ciano (95,208,230), `g` verde (110,230,90); `w` (255,240,220) existe só como textura.

- **`lights.json`**: `{chave: [[x, y, 48, "a"], ...]}` com chaves `city` (58), `fish2` (23), `crypt2` (46), `cath` (60), `garden` (60), `mine` (60). Coordenadas em u (todas ≡3 mod 6). Na carga (`core.js:29`), para todo mapa com `lightsKey`, `map.lights = json.map(l => [l[0], l[1], 46, 'a'])` — **o raio 48 e o tipo do JSON são ignorados** (sempre 46, âmbar).
- Mapas sem `lightsKey` usam o array `lights` do `data.js` (raios 35–95, tipos variados) — ver JSON §9.3.
- Lista dinâmica (`lightsList`): luzes do mapa com cintilação `r*(1+0.05 sin(t*(7+i%3)+i))`; **lanterna do jogador** `(p.x ± 8 conforme flip, p.y-10)`, raio `(morto?40:72) * st.light` (luz da lanterna equipada: 1–1,45; ×0,45 sem lanterna), marcada como jogador; projéteis (orb 34, outros 18; cor conforme dono/cor); glow de mobs `MON.glow=[tipo, raio]` em `y - hh*0.6`; portais abertos da forge `(spot.x, spot.y-30, 90, 'm')`; portal de saída `(x, y-30, 100, 'm')`; porta liberada `(door.x+w/2, door.y+10, 70, 'a')`; FX `plure 50 a`, `lure 55 m`, `korb 30 c`; picks `eco 40 m`, lanternas `36 a`.
- **Camada de escuridão** (`drawLighting`): canvas a **meia resolução** (W/2×H/2); preenche `rgba(9,7,22, dk)`; para cada luz, "fura" com `destination-out` a textura radial branca (`makeGlow`, 128 px: alpha 1 no centro, 0,55 a 40% do raio, 0 na borda — `core.js:41-46`), raio na tela `l.r * 1.6 * SC` (ou seja, **1,6× o raio em u**), alpha 0,95 (jogador) ou 0,85 (demais). Composição resultante: `alpha = dk × Π(1 − a_i·g_i(d))`. A camada é ampliada para a tela com bilinear. Se `s.lost` dentro de Brecha: +`rgba(9,7,22,.15)` na tela toda.
- **Glows coloridos** (`drawGlows`, mundo, aditivo `lighter`): para cada luz, textura colorida (`makeGlow(cor, .9)`) com raio `0.7 × r`, alpha 0,22 (magenta) ou 0,14 (demais).
- **Escuridão `dk`** (`darkness()`, `render.js:2-8`):
  - Dungeon: `hd` 0,26; `core` 0,40; outros 0,46 (fixo, sem ciclo).
  - Cidade por hora `h = clock/60`: `h<17` 0,16; 17–19 lerp 0,16→0,50; 19–22 lerp 0,50→0,66; 22–29 **0,62** (salto de 0,66→0,62 às 22h — provável descuido, não está claro se intencional); 29–30 lerp 0,62→0,50.
  - Modificadores: loja `d*0.45+0.12`; indoor `d*0.55+0.14`; hd (Rua da Forja) `d*0.6`. Se `dk<0.02` não desenha.

| hora | base | forge (×0,6) | guild/inn (×0,55+0,14) | loja (×0,45+0,12) |
|---|---|---|---|---|
| 16:00 | 0,16 | 0,096 | 0,228 | 0,192 |
| 18:00 | 0,33 | 0,198 | 0,322 | 0,269 |
| 19:00 | 0,50 | 0,300 | 0,415 | 0,345 |
| 21:59 | ≈0,66 | ≈0,396 | ≈0,503 | ≈0,417 |
| 22:00–05:00 | 0,62 | 0,372 | 0,481 | 0,399 |
| 06:00 | 0,50 | — (vira o dia) | — | — |

### 4.6 Tintas de estação e clima (`render.js:50-53,84-97`)

- Estação = da Brecha se em run; senão `seasonOf(day)`.
- Tela inteira, se o mapa **não** é `indoor` (dungeons e forge recebem): Inverno `rgba(120,170,255,.07)`; Outono `rgba(255,140,60,.035)`.
- Clima em **espaço de tela** (não segue o mundo), desligado em `indoor` e no tema `core`:

| Estação | cores | vx, vy (u.: ×0,016×U px por frame) | qtd | tamanho base (×U) | extra |
|---|---|---|---|---|---|
| Inverno | `#e8f0ff`, `#c4d4f0` | −10, 40 | 90 | 2 | oscilação `sin(ph)*12` |
| Outono | `#d96a2a`, `#b8462a`, `#e6a541` | −30, 28 | 40 | 3 | idem |
| Primavera | `#ff9ac6`, `#ffd2e6` | −20, 22 | 35 | 2 | idem |
| Verão | `#F2A541`, `#ffe08a` | 0, −6 | 25 | 2 | vagalumes aditivos, alpha `0.4+0.4 sin(3ph)` |

  Passo fixo de 0,016 por frame (não usa `dt`); tamanho `s*U*R(0.7,1.3)`; wrap nas bordas.

### 4.7 Overlays em espaço de tela sobre o mundo (`render.js:212-230`)

- Mobs (não chefe, só feridos ou elite): barra HP 26u (elite ×1,4) acima de `y - hh*s - 8`; losangos por parte (quebrada `#4a3d52`, flash branco, normal magenta); nome do elite.
- Caçadores: nome + rank colorido em `y-38`; balão de fala `rgba(14,11,24,.85)`.
- NPCs: nome âmbar em `y-40`. Jogador: "{nome} (você)" + triângulo âmbar em `y-40`.
- Portais na forge: ícone do rank + bairro + timer em `spot.y-92` (vermelho se `t<60`).
- Floats de dano/ganho; prompt de interação.

### 4.8 HUD (`render.js:233-311` + `extra.js:139-158`)

Vinheta radial (`core.js:54-55`: transparente até 35% do menor lado, `rgba(8,6,16,.75)` em 75% do maior); flash vermelho de dano `rgba(180,20,50, redT*.5)` (`redT=0.35` ao levar dano); overlay de encanto; bloco superior esquerdo (rank, nome, nível, barra XP, HP com "lag", energia, Essência com marca do custo do Eco, aviso "Sem Lanterna"); dicas de tecla; canto superior direito (DIA, lugar, estação·hora·"véspera" se 16–19h, lista de Brechas com timer piscando <60 s, aviso de invasão); hotbar 5 armas + Eco [Q] + Poção [R]; Lumes/Estilhaços/bolsa; chat (6 linhas, somem após 22–26 s); notas; barra do chefe; toast; nome do lugar (3 s); intro do chefe (3,2 s); novo Selo (4 s). Extra v2: cadeado nas armas fora da classe, botão do Supremo (círculo 58u à esquerda da hotbar, varredura de recarga, glow quando pronto), painel da missão atual em `(18u, 170u [+18u se lost])` largura 260u (se `OPT.hints`), banner do Supremo (1,4 s, desliza 80u).

---

## 5. Movimento do jogador

### 5.1 Entrada (`core.js:176-205`, `combat.js:62`, `main.js:96-103`, `extra.js:165,178`)

| Ação | Teclas/mouse |
|---|---|
| Mover | `W/A/S/D` ou setas (8 direções, normalizado) |
| Mirar | mouse (só enquanto botão esquerdo está pressionado ou até 0,5 s após clique); senão direção do movimento/`dir` (`combat.js:44-48`) |
| Atacar | `J`, `K` ou clique esquerdo (arco: segurar carrega até 0,9 s) |
| Esquiva | `Space`, `Shift` (esq./dir.), `L` |
| Interagir | `E` |
| Eco | `Q` ou clique direito |
| Poção | `R` |
| Supremo | `F` ou `V` |
| Inventário | `Tab` ou `I` |
| Pausa | `Esc` |
| Chat | `Enter` (Enter envia, Esc cancela; até 80 caracteres) |
| Som | `M` |
| Armas | `1-5`, roda do mouse (v2: só a da classe; outras mostram aviso) |

- **Sem suporte a gamepad** (nenhuma referência a `getGamepads`).
- `held()` considera a tecla solta se não houve `keydown` (inclusive repetição) há >900 ms — proteção contra `keyup` perdido (`core.js:202`). Ctrl/Meta pressionado → solta tudo (`core.js:195`). Blur/visibilidade/foco → solta tudo.
- Coordenadas do mouse em device px do canvas.

### 5.2 Velocidades (`combat.js:78-92`)

- Base **78 u/s** × `st.spd` (1 + soma de `spd` dos equipamentos, ex. botas +0,04/+0,08) × 0,65 se `chill` (gelo do Inverno, 1,6 s após dano). Multiplicadores: atacando ×0,55; carregando arco ×0,45; wind-up do martelo ×0,2; fora de `fast` (só mapa legado) ×0,62.
- **Sem aceleração/inércia**: velocidade instantânea; para imediatamente.
- Direção visual: `|mx| > |my|*0.9` → `left/right`, senão `up/down`; parado >0,45 s → `down` (olha para a câmera).
- `walkT += dt*(spd/78)`; passos: a cada 0,28 s, 2 partículas de poeira.

### 5.3 Esquiva (dash)

- Condição: sem lock, `dashT<=0`, `dashCd<=0`, energia ≥22.
- Custo 22 de energia; duração **0,17 s** a **270 u/s** (≈45,9 u); `dashCd=0,32` s (conta desde o início); direção = input atual, senão mira. Durante o dash: invulnerável a dano (`hurtPlayer` ignora, `combat.js:15`), cancela carga/wind-up, deixa rastro.
- Energia regenera **30/s** quando não está em dash (máx. 100).
- Knockback ao levar dano: 170 u/s na direção oposta à fonte, decai `×0.0005^dt`, zerado 0,5 s após o dano. Invulnerabilidade pós-dano 0,75 s.

### 5.4 Colisão (`core.js:242-253`)

- Jogador: raio de colisão `cr = 4` (`combat.js:3`). Mobs/NPCs/caçadores: `min(6, r*0.5)` (`r` padrão 8 → 4).
- `canStand(x,y,r)` = 4 sondas de ponto andáveis: `(x−r, y)`, `(x+r, y)`, `(x, y−0.6r)`, `(x, y+0.6r)`.
- `walkable` = `inWalk` (máscara ou `walk`) e fora de `block` e fora de props sólidos não quebrados (elipse `dx² + 2.2·dy² < r²`). Voadores (`fly`) ignoram `block` e props, mas não a máscara/`walk`.
- `moveEnt(e,dx,dy)`: subdivide em passos de no máx. 3 u; em cada passo tenta X (`canStand(x+sx, y)`) e depois Y separadamente → desliza em paredes. Só o destino é testado.
- `randPoint(minD, opts)`: até 400 tentativas aleatórias no retângulo da imagem com margem 10, `canStand(r=opts.r||8)`, distância mínima do jogador e de `avoid`; falhou → `(p.x, p.y-60)`.

---

## 6. NPCs e vida ambiente

### 6.1 NPCs com rotina (`data.js:83-92`, `world.js:159-168`)

| id | nome | mapa | horas (`clock/60`) | vel. (u/s) | waypoints (loop) |
|---|---|---|---|---|---|
| lauro | Sargento Lauro | forge | [17, 27) = 17:00–03:00 | 30 | (600,500) (1100,500) (1150,640) (450,560) |
| marta | Marta | forge | [16, 21) | 22 | (250,600) (420,640) (300,800) |
| faisca | Faísca | forge | [16, 23) | 36 | (590,470) (960,520) (1060,350) (200,420) |
| vesna | Vesna | guild | [18, 24) | 24 | (140,410) (350,410) (250,330) |

- Movimento: espera `wait`; anda em linha reta até o waypoint com `moveEnt`; se andou <0,1 u (bloqueado), **move sem colisão** (atravessa); chegou (<3 u) → próximo waypoint, espera `R(2,5)` s, olha para baixo. Direção pelo eixo dominante.
- Waypoints da forge em células bloqueadas da máscara: lauro (600,500) (1150,640) (450,560); marta (250,600) (420,640); faisca (590,470) (960,520); faisca (200,420) só falha `canStand`. Ou seja, eles atravessam cenário em parte do percurso.
- Fala: interação r 22 → `dialog(nome, lines[line++ % n])` (3 falas cada, `data.js:85-91`) e o NPC espera 4 s.
- Retratos de diálogo (`data.js:93`): Odete `npc_odete_f`, Brás `npc_bras_f`, Lúcia `npc_bia_f`, Vesna, Ivo, Lauro, Marta, Faísca `npc_*_f`.

### 6.2 Caçadores ambiente (`world.js:169-179`, `main.js:109`)

- Lista `D.HUNTERS` (`data.js:280`): Nina C arco, Bento D martelo, Iara B cajado, Tomé E lâmina, Luzia C adagas, Rafa D lâmina, Jurema A cajado, Caio D arco.
- Wander: espera inicial `R(0,3)`; ao fim, alvo `randPoint(0,{r:6})`; anda a **42 u/s**; chegou (<3) → espera `R(1.5,5)` e 25% fala uma frase ("boa véspera", "alguém tem poção?", "vou pro mercado", "cuidado lá fora", "...") por 3 s; travado (moveu < 0,2·dt·42) → espera 0,5 s.
- Além disso, a cada frame, prob. `dt*0.04` de falar ("boa véspera", "alguém pra Brecha?", "preciso de Presa Rubra", "...", "cuidado lá fora").
- Não interagíveis.

### 6.3 Chat simulado

- A cada `R(14,28)` s, um caçador aleatório manda uma frase de `D.CHAT` com `{b}`∈{cemitério, mercado} e `{c}`∈`D.CASAS` (`world.js:154`).
- Mensagem do jogador: 60% de chance de resposta em 1,2–3 s, escolhida por palavra-chave (brecha/portal, oi/olá/boa, ajuda/socorro/lanterna) (`main.js:118-121`).
- Eventos (abertura/ruptura/fechamento de Brecha, resgate) também geram chat.

### 6.4 Gatilhos de diálogo

Interagíveis da §2.6; tutorial automático no novo jogo (0,9 s); falas de fim de missão (`extra.js:121`); diálogo de fuga ao tocar `back`; `G.ui={type:'dialog', who, text, opts, sel, o}` (`world.js:204`) com opções `{l, sub, dis, icon, fn}` e encadeamento `o.next`.

---

## 7. Ciclo do dia / véspera

- `s.clock` em **minutos de jogo**; avança **1 minuto por segundo real** (`world.js:143`) — só enquanto `update` roda (pausa/diálogo/UI congelam).
- Novo save: dia 1, 16:30. `newDay()` (`world.js:137-141`): `day++`, `clock = 16:00`, toast, `nextBrecha = min(next, 15)`, save. Dispara quando `clock >= 30*60` (06:00 do dia seguinte) ou ao dormir na estalagem (10 Lumes × preço; cura total, energia 100).
- Duração de um dia: 16:00 → 06:00 = **840 s (14 min reais)**.
- Exibição `fmtClock` usa `% 24h`; "véspera" no HUD quando `16 <= h < 19`.
- Estação `seasonOf(day) = SEASONS[(2 + floor((day-1)/3)) % 4]` (`core.js:224`): dias 1–3 Outono, 4–6 Inverno, 7–9 Primavera, 10–12 Verão, ciclo de 12 dias.
- O que reage à hora: escuridão da cidade (§4.5); presença de NPCs (só ao entrar no mapa); fala do Brás entre 23h e 5h (`world.js:225`); rótulo "véspera". **A abertura de Brechas não depende da hora** — é por timer (`nextBrecha`).
- O que reage à estação: tinta e clima (§4.6), título/modificador da Brecha (`SEASON_MOD`, `data.js:114-116`), baú extra na Primavera (`world.js:94`), `chill` no Inverno dentro da Brecha (`combat.js:20`) e Inverno anula a lentidão de água (`combat.js:88`); demais efeitos (Verão +15% dano, Outono espectros) estão fora deste escopo (`combat.js`).

---

## 8. `extra.js` — o que adiciona

1. **Classe permanente** (`extra.js:75-94,160-161`): `s.cls` fixado na criação; `s.cur` forçado a `s.cls` todo frame; troca de arma bloqueada com aviso; armas de outra classe aparecem com cadeado na hotbar e não podem ser empunhadas; Brás não forja arma nova.
2. **Travas de equipamento** (`extra.js:75-89`): `GEAR_CLS` = `escudo, sh_solar, sh_lunar, sh_vazio, couraca2 → sword/hammer`; `luva2 → sword/hammer/dag`; `capa2, gibao, rl_vazio → dag/bow`; `livro, rl_astro, ln_lunar → staff/bow`. Selo mínimo = `GEAR.rk` ou por raridade (`comum 0, incomum 1 (D), raro 2 (C), epico 3 (B)`). Itens travados mostram cadeado e não equipam (som `hurt`).
3. **Golpes Supremos** (`extra.js:8-72`), tecla F/V, recarga inicial 6 s, ao usar: hitstop 0,12, shake ≥6, banner 1,4 s, 30 partículas, save.

| Classe | Nome | Recarga | Efeito |
|---|---|---|---|
| sword | Fenda Rubra | 40 s | 3 investidas de 0,16 s a 420 u/s alternando ±0,35 rad da mira; invulnerável; acerto `m.r+30`, dano `atk*2.4+10`, part ×1,4, kb 200; fim 0,5 s |
| bow | Chuva de Vésperas | 45 s | alvo = mouse (≤150 u, se moveu nos últimos 2,5 s) ou mira×(90,70); flecha a cada 0,07 s por 2,5 s em ±52×±38; impacto após 0,18 s: `hypot(dx,1.2dy) < m.r+12`, dano `atk*0.75+3`; fim 2,8 s |
| staff | Lanterna Estelar | 50 s | 1,6 s puxando inimigos em 140 u (110 u/s; chefe 20); explode raio `80+m.r`, dano `atk*4+20`, atordoa 1,6 s (chefe 0,8), shake 10; fim 2 s |
| hammer | Terremoto da Forja | 45 s | 3 ondas a cada 0,35 s, anel até 90 u em 0,45 s; acerta na faixa ±16 u (y×1,3), dano `atk*1.6+8`, part ×2, atordoa 1 s; fim 1,3 s |
| dag | Dança das Sombras | 40 s | até 6 inimigos mais próximos; a cada 0,2 s teleporta para as costas (`m.x - face*16`, se `canStand`) e golpeia `atk*2.8+6` (costas ×1,5); invulnerável 2,4 s |

4. **Regra de Selo nas Brechas** (`extra.js:97`): entrar só até 1 Selo acima.
5. **Missões / Diário** (`extra.js:100-128`): 13 missões sequenciais (`s.qi`, contadores `s.qc`): Primeiros passos (falar com Odete, 20 L) → Bonecos de treino (8 acertos, 1 poção) → A primeira véspera (entrar numa Brecha, 20 L) → Golpe Supremo (usar em Brecha, 2 Estilhaços) → Feche a Brecha (1 chefe, 60 L + 3 Est.) → O fogo do Brás (arma +2, 1 poção) → Quebra-partes (12 partes, 80 L) → Selo D (50 L) → Cemitério das Velas (fechar crypt, 100 L + 4) → Minas da Forja (fechar mines, 140 L) → Estufa Silvestre (180 L) → O Arauto da Lanterna Partida (fechar cathedral, 300 L + 8) → Rumo ao Alvor (Selo ALVOR, 999 L). Conclusão: toast + som `rank` + save + diálogo final opcional.
6. **Tutorial novo** (`extra.js:129-136`): 3 falas de Odete sobre classe, Supremo e Diário.
7. **HUD extra** (§4.8).
8. **Telas** (`extra.js:170-242`): Como jogar (7 abas de texto), Controles (tabela de 12 linhas), Opções (Som, Tremor de tela, Painel de missão), Multijogador (**prévia falsa**: 5 salas fixas, criar sala, código de 6 caracteres; qualquer botão mostra "em construção"), Diário (missões feitas/atual/próxima "???").
9. **Pausa nova** (`extra.js:245-258`): Continuar, Diário, Como jogar, Controles, Multijogador, Opções, Salvar jogo, Voltar ao menu principal.
10. **Título novo** (§1.5) com confirmação para apagar save e seleção de classe com nome.

"Juice" já presente no núcleo (para não perder na reimplementação): hitstop (dano recebido 0,06 s, morte 0,15 s, Supremo 0,12 s, mundo a 8% da velocidade), shake com decaimento exponencial, flash branco por silhueta, rastro de dash, telegraphs magenta pulsantes, quique dos drops com ímã (48 u, coleta a 9 u após 0,45 s), números flutuantes, toasts/notas, fade de mapa, banner de lugar, intro de chefe, partículas de passo/poeira/elemento, portal animado.

---

## 9. Mapa para Godot

### 9.1 Unidades, câmera e filtro

- Usar **1 u = 1 px de mundo no Godot** (imagens de mapa em escala 1, origem (0,0) no canto superior esquerdo). Todos os números deste documento valem direto.
- Câmera: implementar `camTarget` manualmente (§3) e posicionar um `Camera2D` sem `limit_*` e sem smoothing nativo, aplicando `lerp(k=min(1,dt*7))`. Motivo: o protótipo centraliza mapas menores que a vista (offset negativo); reproduzir com fórmula própria evita depender do comportamento de limites do Camera2D. Zoom = `altura_viewport / (hd ? 400 : 300)`, com largura visível mínima de 380 u.
- **Conflito com o projeto atual** (para decisão do time, não alterado aqui): `project.godot` usa viewport 640×360, `stretch scale_mode="integer"` e `default_texture_filter=0` (Nearest). O protótipo usa escala contínua e **filtragem bilinear** (sprites com densidade ~5 px/u são reduzidos; mapas são pré-ampliados 2× e suavizados). Com Nearest a 640×360 os sprites de densidade 5 vão serrilhar. Opções: (a) importar sprites/mapas com filtro Linear (+ mipmaps para os sprites) por recurso; (b) pré-gerar offline os mapas 2× com o mesmo algoritmo de `mapHi` (`core.js:69-89`) e exibi-los em escala 0,5.
- Sprite do atlas → `AtlasTexture` (region `x,y,w,h`) num `Sprite2D` com `centered=false`, `offset=(-ax,-ay)` em px de textura e `scale = 1/k` (× escala extra do objeto). Para flip, espelhar um `Node2D` pai (`scale.x=-1`) para girar em torno da âncora, como `ctx.scale(-1,1)`. Overrides `AXO` em u ×k.
- Y-sort: `Node2D` com `y_sort_enabled`, origem de cada entidade no pé; mobs mortos com offset de sort −30; projéteis/FX aéreos numa camada acima.
- Iluminação: replicar com um `ColorRect` de tela cheia + shader (escuridão `dk` × Π(1 − a·g(d/(1,6r))), com `g` linear 1→0,55 em 0,4 → 0 em 1,0; `a` 0,85/0,95), alimentado por um array de luzes por frame, mais um pass aditivo de glows coloridos (raio 0,7r, alpha 0,14/0,22). `CanvasModulate`/`PointLight2D` não reproduzem o mesmo resultado sem ajuste.

### 9.2 Colisão a partir das máscaras

O repositório já tem `src/Core/World/WalkGrid.cs` (1 pixel da máscara = 1 célula, fora = bloqueado, `BlockedRects()` por greedy meshing, `NearestWalkable`). Recomendação:

1. **Fiel (recomendado)**: não usar física para o movimento. Carregar a máscara em `WalkGrid` (R>127 = andável, célula 8 u) e reimplementar `inWalk / walkable / canStand / moveEnt` exatamente como em `core.js:240-253` (sondas de ponto, r=4 jogador, passos de 3 u, eixos separados, props sólidos elípticos). Isso preserva deslize em parede, "fantasmas" de NPC e todos os pontos da §2.5.
2. **Se o time quiser física**: gerar `StaticBody2D` + `CollisionShape2D` (`RectangleShape2D`) a partir de `WalkGrid.BlockedRects()` multiplicando por 8, num tool C# de editor; jogador com `CircleShape2D` r=4. Atenção às diferenças: o protótipo testa só 4 pontos (não um círculo) e a borda inferior de 5 u (y≥936) também é bloqueada.
3. **Mapas sem máscara** (guild, inn, core, 4 lojas): manter as formas como dados (`walk` = união de retângulos/círculo, `block` = subtração) num `ShapeWalkArea` com a mesma interface do `WalkGrid`. Não rasterizar em células de 8 u: os retângulos não são múltiplos de 8 (ex. `[149,446,12,14]`) e a forma mudaria.
4. Saídas, portas, `back`: testar o **ponto** do jogador contra o retângulo por frame (como `main.js:110`, `world.js:114-115`), não `Area2D` com o corpo do jogador.

### 9.3 Dados por mapa (valores exatos do protótipo)

Sugestão: um `MapData` (`Resource` C#, seguindo o padrão de `src/Data/RankTableData.cs` + `.tres`) por mapa, importado deste JSON. Retângulos `[x,y,w,h]`, círculo `{"c":[cx,cy,r]}`, luzes `[x,y,raio,tipo]`. `fish` e `crypt` são legados (não usar no v2).

```json
{
 "guild": {
  "id": "guild",
  "image": "assets/map_guild.png",
  "imageSize": [500,896],
  "name": "Guilda dos Lanterneiros",
  "indoor": 1,
  "spawn": [250,560],
  "exits": [{"r":[200,872,100,24],"to":"forge","at":[1500,250]}],
  "walk": [[55,200,390,60],[50,258,400,388],[215,640,70,110],[160,740,180,102],[215,840,70,56]],
  "block": [[160,178,175,60],[50,250,85,45],[365,250,85,45],[50,295,72,80],[50,375,60,95],[50,480,50,100],[55,580,48,62],[390,280,60,70],[413,463,30,37],[393,500,52,70],[378,592,55,48],[149,446,12,14],[329,446,12,14],[164,536,12,14],[314,536,12,14]],
  "lights": [[180,100,60,"a"],[310,100,60,"a"],[100,340,55,"a"],[380,350,55,"a"],[190,690,60,"a"],[295,690,60,"a"],[185,190,40,"a"],[245,195,55,"a"]],
  "interactables": [
   {"label":"Falar com Odete","x":247,"y":250,"r":32,"action":"talkClerk"},
   {"label":"Mural de Contratos","x":400,"y":228,"r":36,"action":"openBoard"}
  ],
  "arrivals": {"novoJogo":[250,560],"continuar":[250,330],"morteNaBrecha":[250,330],"daRuaDaForja":[250,840]},
  "dummies": [[155,455],[335,455],[170,545],[320,545]],
  "clerkSprite": {"sprite":"npc_odete_f|npc_odete_a","x":246,"y":193,"scale":1.12,"foregroundSlice":[158,186,168,44]},
  "imagePatch": {"copyFrom":[198,146,34,40],"pasteAt":[229,146]},
  "hunters": "1 se closed>0, senao 0",
  "npcs": [
   {"id":"vesna","name":"Vesna","hours":[18,24],"spd":24,"path":[[140,410],[350,410],[250,330]]}
  ]
 },
 "forge": {
  "id": "forge",
  "image": "assets/map_city.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_city.png",
  "maskCell": 8,
  "name": "Rua da Forja",
  "outdoor": 1,
  "hd": 1,
  "lightsKey": "city",
  "spawn": [1500,250],
  "exits": [{"r":[250,268,40,25],"to":"inn","at":[250,760]},{"r":[1460,128,80,22],"to":"guild","at":[250,840]}],
  "lights": "lights.json[\"city\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]",
  "interactables": [
   {"label":"Forja do Brás","x":1150,"y":265,"r":50,"action":"talkSmith"},
   {"label":"Rua dos Ofícios","x":305,"y":765,"r":34,"action":"oficiosDialog"},
   {"label":"Brecha · <bairro> (spot 0)","x":440,"y":480,"r":24,"action":"portalDialog","cond":"brecha aberta no spot 0"},
   {"label":"Brecha · <bairro> (spot 1)","x":1174,"y":534,"r":24,"action":"portalDialog","cond":"brecha aberta no spot 1"}
  ],
  "arrivals": {"daGuilda":[1500,250],"daEstalagem":[268,325],"dasLojas":[305,790],"voltaDaBrecha":"PORTAL_SPOTS[spot] + [0,24]"},
  "portalSpots": [[440,480],[1174,534]],
  "hunters": "RI(1,2)",
  "npcs": [
   {"id":"lauro","name":"Sargento Lauro","hours":[17,27],"spd":30,"path":[[600,500],[1100,500],[1150,640],[450,560]]},
   {"id":"marta","name":"Marta","hours":[16,21],"spd":22,"path":[[250,600],[420,640],[300,800]]},
   {"id":"faisca","name":"Faísca","hours":[16,23],"spd":36,"path":[[590,470],[960,520],[1060,350],[200,420]]}
  ]
 },
 "inn": {
  "id": "inn",
  "image": "assets/map_inn.png",
  "imageSize": [498,896],
  "name": "Estalagem Repouso da Lanterna",
  "indoor": 1,
  "spawn": [250,520],
  "exits": [{"r":[200,815,100,15],"to":"forge","at":[268,325]}],
  "walk": [[45,200,405,400],[200,595,100,110],[100,700,300,75],[200,770,100,60],[70,60,195,140],[285,60,150,140]],
  "block": [[45,188,95,20],[265,60,22,150],[285,188,45,20],[78,92,138,92],[325,88,90,82],[218,92,36,46],[290,92,30,36],[85,240,175,90],[268,285,32,40],[335,235,115,95],[298,338,152,92],[68,352,80,70],[78,480,78,72],[308,478,82,72],[415,450,35,50],[60,560,35,40],[400,560,40,40],[392,680,40,40]],
  "lights": [[145,85,50,"a"],[410,85,50,"a"],[55,245,50,"a"],[50,420,50,"a"],[285,245,50,"a"],[425,290,55,"f"],[105,375,40,"a"],[115,495,40,"a"],[350,500,40,"a"],[160,655,55,"a"],[315,655,55,"a"],[365,385,35,"a"]],
  "interactables": [
   {"label":"Falar com Lúcia","x":385,"y":446,"r":38,"action":"talkInn"},
   {"label":"Descansar","x":238,"y":168,"r":28,"action":"restDialog"}
  ],
  "arrivals": {"daRuaDaForja":[250,760],"morteNaCidade":[250,520]},
  "hunters": "RI(1,2)"
 },
 "fish": {
  "id": "fish",
  "image": "assets/map_fish.png",
  "imageSize": [496,1014],
  "name": "Mercado de Peixes",
  "dungeon": 1,
  "theme": "fish",
  "spawn": [265,960],
  "door": [215,88,80,30],
  "back": [200,1000,135,14],
  "walk": [[35,130,425,670],[200,800,135,214],[215,85,80,50]],
  "block": [[35,120,100,175],[35,340,125,90],[72,450,88,50],[40,700,110,130],[68,615,52,65],[368,330,92,85],[368,545,92,175],[182,165,45,65],[296,175,38,55],[275,630,35,40],[202,825,16,40],[322,825,16,40]],
  "fast": [[150,125,200,70],[215,195,80,150],[178,195,50,55],[295,175,40,70],[295,290,165,150],[35,185,100,135],[35,340,160,180],[190,485,85,200],[165,645,295,135],[330,525,130,130],[195,780,145,234],[215,85,80,50]],
  "lights": [[185,75,55,"a"],[325,75,55,"a"],[115,160,45,"a"],[65,405,45,"a"],[425,270,45,"a"],[425,510,45,"a"],[380,620,45,"a"],[185,680,45,"a"],[200,180,40,"a"],[415,120,50,"c"]],
  "legacy": "nao usado no v2: imagem nao e carregada (core.js:25); THEMES usa fish2/crypt2"
 },
 "crypt": {
  "id": "crypt",
  "image": "assets/map_crypt.png",
  "imageSize": [456,1014],
  "name": "Cemitério",
  "dungeon": 1,
  "theme": "crypt",
  "spawn": [228,960],
  "door": [195,85,70,30],
  "back": [190,1000,75,14],
  "walk": [[30,150,395,650],[140,125,170,30],[190,800,75,214],[195,80,70,50]],
  "block": [[15,185,135,50],[300,185,140,50],[45,290,60,85],[128,315,28,32],[125,360,32,42],[38,245,40,48],[172,405,105,170],[68,488,42,58],[25,425,105,75],[343,265,70,112],[318,530,125,65],[345,630,68,110],[268,645,60,75],[300,725,28,35],[32,675,90,95],[25,568,115,55],[110,565,35,90],[25,765,160,55],[268,765,160,55],[150,860,45,55],[262,860,48,55]],
  "lights": [[160,125,45,"a"],[290,125,45,"a"],[90,265,40,"a"],[350,250,40,"a"],[155,425,40,"a"],[225,575,40,"a"],[385,760,40,"a"],[170,885,50,"f"],[285,885,50,"f"],[340,420,70,"m"],[100,640,70,"m"]],
  "legacy": "nao usado no v2: imagem nao e carregada (core.js:25); THEMES usa fish2/crypt2"
 },
 "fish2": {
  "id": "fish2",
  "image": "assets/map_fish2.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_fish2.png",
  "maskCell": 8,
  "name": "Mercado de Peixes",
  "dungeon": 1,
  "hd": 1,
  "theme": "fish",
  "lightsKey": "fish2",
  "spawn": [830,870],
  "door": [740,60,120,40],
  "back": [790,890,90,30],
  "lights": "lights.json[\"fish2\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]"
 },
 "crypt2": {
  "id": "crypt2",
  "image": "assets/map_crypt2.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_crypt2.png",
  "maskCell": 8,
  "name": "Cemitério das Velas",
  "dungeon": 1,
  "hd": 1,
  "theme": "crypt",
  "lightsKey": "crypt2",
  "spawn": [835,895],
  "door": [180,110,60,40],
  "back": [790,915,100,25],
  "lights": "lights.json[\"crypt2\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]"
 },
 "mine": {
  "id": "mine",
  "image": "assets/map_mine.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_mine.png",
  "maskCell": 8,
  "name": "Minas da Forja",
  "dungeon": 1,
  "hd": 1,
  "theme": "mines",
  "lightsKey": "mine",
  "spawn": [330,770],
  "door": [190,70,50,40],
  "back": [300,790,80,20],
  "lights": "lights.json[\"mine\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]"
 },
 "garden": {
  "id": "garden",
  "image": "assets/map_garden.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_garden.png",
  "maskCell": 8,
  "name": "Estufa Silvestre",
  "dungeon": 1,
  "hd": 1,
  "theme": "garden",
  "lightsKey": "garden",
  "spawn": [1040,895],
  "door": [1090,100,90,30],
  "back": [990,915,110,25],
  "lights": "lights.json[\"garden\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]"
 },
 "cath": {
  "id": "cath",
  "image": "assets/map_cath.png",
  "imageSize": [1672,941],
  "mask": "assets/mask_cath.png",
  "maskCell": 8,
  "name": "Catedral do Arauto",
  "dungeon": 1,
  "hd": 1,
  "theme": "cathedral",
  "lightsKey": "cath",
  "spawn": [835,905],
  "back": [790,920,90,20],
  "bossHere": [835,380],
  "exitAt": [835,330],
  "lights": "lights.json[\"cath\"] -> cada [x,y,_,_] vira [x,y,46,\"a\"]"
 },
 "core": {
  "id": "core",
  "image": "assets/map_core.png",
  "imageSize": [496,1014],
  "name": "Câmara do Núcleo",
  "dungeon": 1,
  "theme": "core",
  "spawn": [250,960],
  "back": [213,1000,74,14],
  "walk": [{"c":[250,505,142]},[213,235,78,190],[213,630,74,384],[8,475,115,75],[378,475,118,75],[55,205,390,75],[205,150,90,90]],
  "block": [[80,488,42,55],[383,488,42,55],[148,245,45,45],[313,245,45,45]],
  "lights": [[60,165,55,"f"],[170,255,50,"f"],[335,255,50,"f"],[450,165,55,"f"],[100,510,50,"f"],[405,510,50,"f"],[250,640,40,"a"],[180,800,55,"f"],[325,800,55,"f"],[105,350,60,"m"],[395,360,60,"m"],[130,705,55,"m"],[380,685,55,"m"],[250,170,90,"m"]],
  "arrivals": {"daPortaDaBrecha":[250,960]},
  "bossDefault": [250,500],
  "exitDefault": [250,228],
  "crystalProps": [[105,350],[395,360],[130,705],[380,685]]
 },
 "shop_arm": {
  "id": "shop_arm",
  "image": "assets/shop_arm.png",
  "imageSize": [696,408],
  "name": "Armaria Rubra",
  "indoor": 1,
  "shop": 1,
  "spawn": [352,370],
  "exits": [{"r":[300,394,105,14],"to":"forge","at":[305,790]}],
  "walk": [[45,135,610,170],[300,300,105,108]],
  "block": [[45,135,85,95],[45,250,80,55],[120,210,85,85],[130,145,30,55],[200,190,30,50],[255,150,75,60],[238,245,95,50],[440,128,145,70],[500,210,50,75],[555,210,60,80],[590,90,65,120],[600,210,55,95]],
  "lights": [[285,90,95,"f"],[65,65,45,"a"],[345,55,45,"a"],[590,55,45,"a"],[140,175,40,"a"],[40,235,45,"a"],[630,215,45,"a"],[465,140,35,"a"],[290,330,45,"a"],[410,330,45,"a"]],
  "interactables": [
   {"label":"Falar com Gaspar","x":510,"y":206,"r":38,"action":"talkGaspar"}
  ],
  "hunters": "0"
 },
 "shop_mag": {
  "id": "shop_mag",
  "image": "assets/shop_mag.png",
  "imageSize": [694,408],
  "name": "Casa dos Feitiços",
  "indoor": 1,
  "shop": 1,
  "spawn": [347,370],
  "exits": [{"r":[300,394,95,14],"to":"forge","at":[305,790]}],
  "walk": [[40,140,610,165],[300,300,95,108]],
  "block": [[55,120,40,70],[100,150,60,50],[150,145,45,70],[25,195,50,60],[55,230,80,75],[140,240,70,60],[230,140,190,75],[410,120,35,65],[475,40,115,120],[480,195,130,85],[620,215,40,50],[600,265,40,35],[215,255,40,40],[420,255,40,40],[600,120,50,60]],
  "lights": [[278,65,40,"a"],[385,65,40,"a"],[90,80,55,"m"],[620,85,55,"m"],[330,90,55,"c"],[195,110,35,"c"],[460,115,35,"c"],[40,215,55,"m"],[285,165,35,"a"],[370,165,35,"a"],[490,210,35,"a"],[155,245,35,"a"],[270,320,45,"m"],[395,320,45,"m"],[550,200,45,"c"]],
  "interactables": [
   {"label":"Falar com Vesna","x":330,"y":226,"r":36,"action":"talkVesna"}
  ],
  "hunters": "0"
 },
 "shop_alq": {
  "id": "shop_alq",
  "image": "assets/shop_alq.png",
  "imageSize": [696,434],
  "name": "Alquimia do Ivo",
  "indoor": 1,
  "shop": 1,
  "spawn": [360,395],
  "exits": [{"r":[305,420,110,14],"to":"forge","at":[305,790]}],
  "walk": [[40,165,620,160],[305,320,110,114]],
  "block": [[40,100,180,110],[60,205,155,75],[110,265,40,40],[20,250,40,40],[215,250,40,40],[275,165,160,60],[480,140,90,55],[515,205,150,70],[590,275,50,45],[490,255,40,40],[620,170,40,50]],
  "lights": [[270,60,45,"a"],[430,45,45,"a"],[600,110,45,"a"],[40,205,45,"a"],[290,160,35,"a"],[325,120,60,"g"],[100,165,60,"g"],[185,225,35,"c"],[580,195,40,"c"],[555,245,35,"a"],[290,345,45,"a"],[455,345,45,"a"]],
  "interactables": [
   {"label":"Falar com Ivo","x":355,"y":236,"r":36,"action":"talkIvo"}
  ],
  "hunters": "0"
 },
 "shop_rel": {
  "id": "shop_rel",
  "image": "assets/shop_rel.png",
  "imageSize": [694,434],
  "name": "Oficina de Relíquias & Lanternas",
  "indoor": 1,
  "shop": 1,
  "spawn": [350,395],
  "exits": [{"r":[300,420,100,14],"to":"forge","at":[305,790]}],
  "walk": [[40,160,610,155],[300,310,100,124]],
  "block": [[20,140,60,110],[70,185,120,50],[75,250,60,60],[140,240,40,60],[165,265,45,45],[250,170,175,65],[440,150,45,50],[500,215,95,80],[560,90,90,110],[610,215,40,90],[450,265,50,40]],
  "lights": [[85,90,55,"a"],[275,40,45,"a"],[395,40,45,"a"],[465,90,50,"a"],[45,215,55,"a"],[155,265,40,"a"],[565,235,40,"a"],[270,330,45,"a"],[390,330,45,"a"],[230,95,45,"m"],[560,100,50,"m"],[590,140,55,"a"],[130,180,40,"m"],[380,180,35,"a"],[330,60,45,"m"]],
  "interactables": [
   {"label":"Falar com Abel","x":335,"y":250,"r":36,"action":"talkAbel"}
  ],
  "hunters": "0"
 }
}
```

Dados globais relacionados (manter em outro resource): `PORTAL_SPOTS [[440,480],[1174,534]]` (`data.js:98`); `OFICIOS` ordem `shop_arm, shop_mag, shop_alq, shop_rel` (`data.js:78-82`); `THEMES` (mapa, chefe, props, mobs — `data.js:107-113`); `LIGHTC` (`data.js:4`); `lights.json` como está.

### 9.4 Assets para copiar

Pastas de destino propostas seguindo o que já existe no repo (`assets/maps/city/`, `assets/sprites/*`, `assets/fonts/`):

| Origem (`assets/`) | Destino no repo Godot | Uso no v2 |
|---|---|---|
| `map_city.png`, `mask_city.png` | `assets/maps/city/` (já existem) | Rua da Forja |
| `map_guild.png` | `assets/maps/guild/` | Guilda (aplicar o patch de §2.7 ou editar a arte) |
| `map_inn.png` | `assets/maps/inn/` | Estalagem |
| `map_core.png` | `assets/maps/core/` | Câmara do Núcleo |
| `map_fish2.png`, `mask_fish2.png` | `assets/maps/fish2/` | Brecha Mercado |
| `map_crypt2.png`, `mask_crypt2.png` | `assets/maps/crypt2/` | Brecha Cemitério |
| `map_mine.png`, `mask_mine.png` | `assets/maps/mine/` | Brecha Minas |
| `map_garden.png`, `mask_garden.png` | `assets/maps/garden/` | Brecha Estufa |
| `map_cath.png`, `mask_cath.png` | `assets/maps/cath/` | Brecha Catedral |
| `shop_arm.png`, `shop_mag.png`, `shop_alq.png`, `shop_rel.png` | `assets/maps/shops/` | 4 lojas |
| `lights.json` | `assets/maps/lights.json` (ou `src/Data/`) | luzes dos mapas hd |
| `atlas.png`, `atlas.json` | `assets/sprites/atlas/` | todos os sprites/ícones |
| `src/*_{f,a,s,d}.png` (cnev, coss, cvel, cosr, cceif, mbes, maut, mesc, msen, mcol, gbro, gsei, gmus, gflo, grai, kesc, ksin, ksom, kcav, karr) + `src/meta.json` | `assets/sprites/enemies/` | **não carregados no v2**; necessários para os temas crypt/mines/garden/cathedral (pivô não definido) |
| `src/d_*.png` (36 ícones) | `assets/sprites/items/` | ícones de materiais; **não carregados no v2** |
| `keyart.png` | `assets/sprites/ui/` | fundo do título/história |
| `lumeira_map.png` | `assets/sprites/ui/` | ilustração da história |
| `logo.png` | `assets/sprites/ui/` | cabeçalho do inventário |
| Fontes Google: **Cinzel** (500/700) e **Pixelify Sans** (400/600) | `assets/fonts/` | títulos (`FT`) e texto (`FU`) (`core.js:7`, `Vespera v2.dc.html`); Pixelify Sans já está no repo, **Cinzel não** |

Não copiar: `*_orig.png` (versões sem nitidez, não usadas), `map_fish.png`, `map_crypt.png`, `map_forge.png` (v1/legado), `src/old_atlas.png`, `src/old_meta.json`, `src/i_coin.png` (duplicado do atlas), pastas `scratch/` e `uploads/` (material de trabalho do protótipo).

---

## 10. Pontos não claros / prováveis bugs do protótipo

- Salto de escuridão 0,66→0,62 às 22h (`render.js:5`).
- Raio/tipo de `lights.json` ignorados (sempre 46/'a') (`core.js:29`).
- `tint` de tema nunca definido (`render.js:29`); `sq` (squash) calculado e não usado (`render.js:162`); `por5` não usado.
- Monstros/ícones de `assets/src/` ausentes do atlas → invisíveis no protótipo (§4.1); pivô desses sprites não definido.
- Volta da Brecha no spot 1 em célula bloqueada (§2.5).
- NPC: ponto inicial e índice de waypoint sorteados separadamente (`world.js:19`); atravessam paredes quando travados (`world.js:165`).
- `G.firstDone` não persistido: toda sessão começa com uma Brecha `fish` de 300 s.
- Fuga empurra até 28 u (`p.y-=14` + `moveEnt(p,0,-14)`, `world.js:115`); o termo `*0` em x é código morto.
- Clima usa passo fixo por frame (0,016), não `dt` (`render.js:91-92`).
