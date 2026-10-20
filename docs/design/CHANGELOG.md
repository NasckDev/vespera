# Changelog

Uma linha por tarefa concluída, mais recente primeiro.

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
