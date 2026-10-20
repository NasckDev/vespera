# Changelog

Uma linha por tarefa concluída, mais recente primeiro.

- 2026-10-20 — Smoke do mundo: visita os 13 mapas pelas saídas reais (ida e volta), porta e fuga das Brechas, interagíveis, câmera e sprites, com prints. API em `api-mundo.md`; decisões D18–D24.
- 2026-10-20 — Jogador com o visual da v2 (`AtlasSprite`, `c_{classe}_*`, sombra, bob), 78 u/s, círculo r=4 no pé, teclado e gamepad.
- 2026-10-20 — Cena `World`: carrega qualquer mapa por id, colisão por máscara ou formas, saídas/porta/fuga pelo pé, `GoMap` com fade, interagíveis no EventBus, `WorldCamera`; Main → World na Guilda (Lumeira provisória removida).
- 2026-10-20 — Core do mundo: `AtlasIndex`, `MapCatalog` + `src/Data/maps.json`, `ShapeWalkArea`/`MaskWalkArea`, `WalkPathfinder`, `CameraFraming`, com testes.
- 2026-10-20 — Assets da v2: mapas, máscaras, atlas, 20 monstros e 36 ícones de `assets/src`, UI e fonte Cinzel; `assets/README.md` e exceções de fontes.
- 2026-10-20 — Adota o protótipo v2 como referência; registra decisões D1–D17 em `DECISIONS.md`.
- 2026-10-20 — Smoke test com descoberta de cenários por atributo (`[SmokeScenario]`).
- 2026-10-20 — CI (Core + Godot + smoke) e release (Windows, Linux, macOS, itch.io).
- 2026-10-20 — Fundação: projeto Godot 4.7 C#, Core testado (ranks, XP, dano, save, WalkGrid), colisão por máscara, HUD.
