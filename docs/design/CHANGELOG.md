# Changelog

Uma linha por tarefa concluída, mais recente primeiro.

- 2026-10-21 — Biblioteca de objetos (B5): 445 objetos recortados de 6 folhas em 9 temas, `objects.json` calibrado pelo jogador + `ObjectCatalog`, 20 mapas candidatos (salas extras e Brechas 6–10), galeria de depuração com smoke e triagem das 70 imagens em `catalogo-assets.md` (B5-1 a B5-5).
- 2026-10-21 — Motor do mundo (B4a): escala inteira e câmera no pixel, andar com aceleração curta e interpolação, caçador do super kit com caminhada procedural, proporção pela porta (1,75×), oclusão translúcida, água/luzes/fumaça/vagalumes, quebráveis, baús, decoração (lampião, flor, balsa, pesca, banco, placas), objetos do catálogo, sons ambiente e minimapa (B4a-D39–D47).
- 2026-10-21 — Rua da Forja refeita (B4b): colisão de 4 px andável em todas as escadas, pontes, cais e becos, 42 oclusores, água, 15 sons ambiente, 35 quebráveis, 6 baús, 38 decorações com lore, 6 balsas e portas reais para as 4 lojas; smoke `rua da forja` (B4b-D60–D63).
- 2026-10-20 — Do super kit: animação do portal, guia de HUD e catálogo do Arsenal como referência (D38).
- 2026-10-20 — Áudio (correções): música por local sem depender de nomes (D22), crossfade sem cortes (D23), +12 dB no Master (D18), músicas pré-carregadas, `SetBusVolume(persist)` + `SaveSettings`, pitch limitado e cenários de smoke sem depender da cena Lumeira.
- 2026-10-20 — Áudio: 18 SFX da v2 e 3 músicas em loop gerados por script, barramentos Master/Music/SFX/UI e `AudioManager` com pool, crossfade e volumes persistidos.
- 2026-10-20 — Smoke do mundo: visita os 13 mapas pelas saídas reais (ida e volta), porta e fuga das Brechas, interagíveis, câmera e sprites, com prints. API em `api-mundo.md`; decisões D18–D24.
- 2026-10-20 — Jogador com o visual da v2 (`AtlasSprite`, `c_{classe}_*`, sombra, bob), 78 u/s, círculo r=4 no pé, teclado e gamepad.
- 2026-10-20 — Cena `World`: carrega qualquer mapa por id, colisão por máscara ou formas, saídas/porta/fuga pelo pé, `GoMap` com fade, interagíveis no EventBus, `WorldCamera`; Main → World na Guilda (Lumeira provisória removida).
- 2026-10-20 — Core do mundo: `AtlasIndex`, `MapCatalog` + `src/Data/maps.json`, `ShapeWalkArea`/`MaskWalkArea`, `WalkPathfinder`, `CameraFraming`, com testes.
- 2026-10-20 — Assets da v2: mapas, máscaras, atlas, 20 monstros e 36 ícones de `assets/src`, UI e fonte Cinzel; `assets/README.md` e exceções de fontes.
- 2026-10-20 — Smoke: cenários de progressão (XP → Nível e os 10 Selos), economia (preços com arredondamento JS) e save → load.
- 2026-10-20 — HUD e RankSeal com 10 Selos (cores de `ranks.json`, SSSS em estrela de oito pontas), Nível e Lumes.
- 2026-10-20 — GameManager sobre o `GameState`; novos sinais de estado no EventBus; autoload `Database` carrega os JSON.
- 2026-10-20 — `GameState` (Core): XP, Lumes, materiais, equipamento, armas, lojas, poções, Ecos, infusões, Lanterna, relógio e missões.
- 2026-10-20 — SaveData v2 com migração do save v1 e saneamento de ids.
- 2026-10-20 — Regras da v2 no Core: Progression (10 Selos + Nível), stats, preços, custos, dano, escala, saque, estações, Brechas, Lanterna, travas e missões.
- 2026-10-20 — `GameDatabase` carrega e valida `src/Data/*.json`; `JsMath.Round` e `IRandom` com seed.
- 2026-10-20 — Dados da v2 em JSON (`src/Data`): selos, estações, temas, armas, materiais, equipamentos, lojas, monstros, Ecos, NPCs e missões.
- 2026-10-20 — Adota o protótipo v2 como referência; registra decisões D1–D17 em `DECISIONS.md`.
- 2026-10-20 — Smoke test com descoberta de cenários por atributo (`[SmokeScenario]`).
- 2026-10-20 — CI (Core + Godot + smoke) e release (Windows, Linux, macOS, itch.io).
- 2026-10-20 — Fundação: projeto Godot 4.7 C#, Core testado (ranks, XP, dano, save, WalkGrid), colisão por máscara, HUD.
