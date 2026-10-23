# Falas dos Atos III ao Final (data/dialogue/ato3 … final)

Transcrição em JSON das falas do `docs/v2/ROTEIRO-V2.md` §13 (canônico). Formato igual ao de `data/dialogue/*.json`.
O id de cada fala é o caminho sem extensão (`"dialogue": "ato3/severo"` no level); o `StoryService` já lê
`res://data/dialogue/{id}.json`, então subpasta funciona sem mudar código. Validação: 52 arquivos, todo `next`,
entrada e escolha apontam para nó existente, nenhum texto vazio.

## O que o jogo precisa ligar (não existe ainda)

**Flags que o jogo liga sozinho:** `GuardianDefeated:farol_afogado|velora|sineiro_mudo|inquilino`, `hunt_cemiterio_1`,
`ruas_aviso_2|3` (vésperas sem ir às Ruas Baixas), `ruas_rompeu` / `ruas_fechada_a_tempo`, `ravi_lanterna_recuperada`,
`sineiro_fase_2|3`, `inquilino_fase_2|3`, `reflexo_min_2|3|4` (minutos de luta), `lis_entrou` (Lis entrou na Brecha
final), `<ato>_morte_N` (N-ésima morte no ato, 1–6; depois disso, sortear um nó `m1..m6` do `<ato>/morte`).

**Eventos novos:** `UnlockFishing`, `OpenFishing`, `ItemFound:lanterna_m`, `BurnSeal:<rank>`, `UnlockEchoSlot:1`,
`HideRivalScore`, `UnlockSopaDaVespera`, `InquilinoPull`, `GiveLegendaryWeapon`, `SoftGameOver:sopa` (depois toca
`ato7/nilza_sonho`), `CloseBreach`, `GiveItem:badalo`, `RingBell:benicio|lis|jogador`, `EndPart1`.

**Fichas novas no texto:** `{lanternas_perdidas}`, `{lado_esquiva}`, `{familia_medo}` (estatísticas do Reflexo, §19).

**Falantes novos para `story.json`:** `severo`, `vigia`, `mae_ravi`, `celeste`, `farol`, `velora`, `sineiro`,
`inquilino`, `avesso`, `reflexo` (os seis últimos sem retrato; o Sineiro mostra a palavra em pedra, sem voz).

**Rotas do Ravi:** `ravi_rota_a` / `ravi_rota_b` nascem em `ato4/ravi` e escolhem a fala em `ato5..ato7/ravi`.
**Final:** `final/sino` mostra Benício só com `benicio_na_decisao` (escolha em `ato7/benicio`) e Lis só com
`lis_escondida` + `lis_entrou`; o jogador sempre pode tocar. Use `final/epilogo_sem_benicio` quando `final_benicio`.
