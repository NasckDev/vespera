# VÉSPERA — contexto do projeto para o Claude Code

Este arquivo é a fonte de verdade do projeto. Leia inteiro antes de criar ou alterar qualquer coisa.

> **Atualização 2026-10-20:** o protótipo **v2** virou a referência de jogo e arte. Antes de implementar, leia
> [`docs/design/DECISIONS.md`](docs/design/DECISIONS.md) — ele **sobrepõe** este arquivo onde houver conflito
> (mapas pintados + máscara, 10 selos com SSSS, filtro Linear, dados em JSON, logger `Log`, plugins adiados)
> e as specs em `docs/design/prototype-v2/`. Playtest automático: `godot --headless --path . -- --smoke-test`.
Idioma do código: inglês (classes, métodos, variáveis). Idioma de textos do jogo, commits e documentação: português (pt-BR).

---

## 1. O jogo em um parágrafo

**Véspera** é um RPG de ação 2D em pixel art detalhada, para desktop (Windows, macOS, Linux), single-player por enquanto, com multiplayer cooperativo planejado para uma fase futura. Visual na linha de Stardew Valley (top-down, cidade viva, estações); gameplay na linha de Dauntless + Solo Leveling. A cidade de **Lumeira** é o hub. Toda *véspera* (fim de tarde) abrem **Brechas** (portais) em algum bairro; caçadores entram, enfrentam ondas de monstros e um boss (o **Guardião**), coletam **Fragmentos**, voltam, forjam armas melhores e sobem de **rank**: E → D → C → B → A → S → SS → SSS → **Alvor**. Mecânicas próprias: a **Lanterna** (ao morrer, seus Fragmentos daquela caçada ficam na Brecha até você voltar), **Ecos** (habilidades únicas deixadas por bosses) e Brechas que herdam o tema do bairro e da estação onde abrem.

Um protótipo do loop completo existe em HTML (`docs/prototype/vespera.html`) e serve de referência de *game feel*, não de código.

---

## 2. Stack oficial (decisões travadas — não trocar sem discussão com o time)

| Camada | Escolha | Observações |
|---|---|---|
| Engine | **Godot 4.5+ (versão .NET)** | Sempre a última stable 4.x. Nunca Godot 3, nunca GDScript no código do jogo. |
| Linguagem | **C# 12 / .NET 8 SDK (LTS)** | `TargetFramework` = `net8.0`. Godot 4.5 exige .NET 8 ou superior. |
| IDE | VS Code + extensão *C# Dev Kit* + *godot-tools*; ou JetBrains Rider | Qualquer uma; o repo deve funcionar com ambas. |
| Arte | Pixelorama (gratuito) ou Aseprite | Sprites em PNG, sem compressão com perda. |
| Mapas | **LDtk** + plugin *LDtk Importer* para Godot 4 | Arquivos `.ldtk` versionados em `assets/maps/`. Fallback: TileMapLayer nativo. |
| Câmera | Plugin *Phantom Camera* | |
| Diálogos | Plugin *Dialogic 2* | NPCs de Lumeira. |
| IA de inimigos | Plugin *LimboAI* (behavior trees + state machines) | Começar com state machine simples em C#; migrar para LimboAI no boss. |
| Testes | *GdUnit4* (versão .NET) | Testes unitários da lógica pura (ranks, XP, loot, save). |
| Versionamento | Git + GitHub, **Git LFS** para `*.png *.wav *.ogg *.mp3 *.ttf *.aseprite` | Branch `main` protegida; trabalho em branches + PR. |
| CI | **GitHub Actions** | Build headless, testes e export dos três desktops a cada push em `main` e em tags `v*`. |
| Distribuição | **itch.io** via `butler` | Canais `windows`, `mac`, `linux`. Página privada até o time decidir abrir. |
| Instalador Windows | Export do Godot gera `.exe` + `.pck`; instalador opcional com **Inno Setup** (`installer/vespera.iss`) | Para amigos testarem, o app do itch.io já instala e atualiza sozinho. |
| Save | JSON local em `user://save.json` via `FileAccess` | Versionado (`saveVersion`) para migrações futuras. |
| Online (fase 2, não implementar agora) | Supabase (Postgres + Auth) para contas e ranking; servidor dedicado em ASP.NET ou API multiplayer nativa do Godot | Deixar a lógica de jogo desacoplada da apresentação para isso ser possível. |

Pixel art: **Project Settings → Rendering → Textures → Default Texture Filter = Nearest**, `Stretch Mode = canvas_items`, `Stretch Aspect = keep`, resolução base **640×360** (escala inteira para 1280×720 e 1920×1080). Tiles **32×32**. Personagens 32×48. Pixel snap ligado (`Rendering → 2D → Snap 2D Transforms to Pixel`).

---

## 3. Estrutura do repositório

```
vespera/
├── CLAUDE.md                  ← este arquivo
├── README.md                  ← como rodar em 5 minutos
├── .gitignore .gitattributes .editorconfig
├── project.godot
├── Vespera.csproj  Vespera.sln
├── export_presets.cfg
├── .github/workflows/
│   ├── ci.yml                 ← build + GdUnit4 em cada PR
│   └── release.yml            ← export 3 plataformas + butler push em tag v*
├── installer/vespera.iss      ← Inno Setup (opcional)
├── docs/
│   ├── design/                ← GDD, decisões, referências
│   ├── brand/                 ← paleta, logo, selos (exportados do painel de identidade)
│   └── prototype/vespera.html ← protótipo HTML de referência
├── assets/                    ← SOMENTE arquivos de arte/áudio/fontes (LFS)
│   ├── sprites/{player,enemies,npcs,items,fx,ui}/
│   ├── tilesets/
│   ├── maps/                  ← .ldtk
│   ├── audio/{sfx,music}/
│   └── fonts/                 ← PixelifySans, VT323
├── src/                       ← todo o C#
│   ├── Core/                  ← lógica pura, sem Godot: Ranks, Xp, Loot, Stats, SaveData
│   ├── Autoload/              ← GameManager, SaveService, EventBus, AudioManager
│   ├── Player/                ← Player.cs, PlayerStateMachine, Weapons/
│   ├── Enemies/               ← Enemy.cs base, Lodo, Espectro, Bosses/Guardiao
│   ├── World/                 ← City/, Dungeon/ (geração de salas), Portal.cs, DayCycle.cs
│   ├── Items/                 ← Fragment, Lantern, Echo
│   ├── UI/                    ← Hud, RankSeal, DialogBox, Menus/, Theme/
│   └── Data/                  ← Resources (.tres): EnemyData, WeaponData, RankTable
├── scenes/                    ← .tscn espelhando src/ (Player/, Enemies/, World/, UI/)
├── tests/                     ← GdUnit4 (C#), espelhando src/Core
└── addons/                    ← plugins (commitados, versão fixa)
```

Regra: **`src/Core` não referencia `Godot`** (só `System.*`). Tudo que é regra de jogo — tabela de ranks, cálculo de dano, drops, save — vive ali e é testável sem a engine. É também o que vai para o servidor na fase online.

---

## 4. Convenções de código

- C# padrão Microsoft: `PascalCase` para tipos, métodos, propriedades e campos públicos; `_camelCase` para campos privados; `camelCase` para locais e parâmetros. `var` quando o tipo é óbvio.
- Um script por cena, mesmo nome do arquivo `.tscn`. Classes de nó marcadas com `[GlobalClass]` quando reutilizáveis; campos editáveis no inspector com `[Export]`.
- Comunicação entre sistemas pelo **`EventBus`** (autoload com `[Signal]`s C#): `PlayerDamaged`, `EnemyDied`, `XpGained`, `RankChanged`, `FragmentsChanged`, `PortalEntered`, `DungeonCleared`. Nós nunca procuram outros nós pela árvore com `GetNode("../../..")`.
- Dados de balanceamento (HP, dano, XP, custos de forja) em **Resources `.tres`**, nunca hardcoded em scripts. Tabela de ranks em `RankTable.tres`.
- Física e movimento em `_PhysicsProcess`; input lido via `Input.IsActionPressed` com ações nomeadas (`move_left`, `attack`, `interact`, `pause`). Suporte a teclado e gamepad desde o início.
- Nada de `GD.Print` em código final; usar `Logger` (wrapper fino com níveis) em `Autoload`.
- Commits em português, imperativo, curtos: `adiciona state machine do jogador`. Conventional Commits opcional.
- Toda PR precisa: build verde, testes verdes, e um GIF/print se mudar algo visual.

---

## 5. Arquitetura das cenas

- **`Main.tscn`** (autoload-like raiz): troca cenas via `GameManager.ChangeScene(...)` com fade.
- **`World/City/Lumeira.tscn`**: TileMapLayers (chão, decoração, colisão), `Player`, NPCs (`Odete` na forja), `Portal`, `DayCycle` (tint de iluminação por hora; a véspera abre a Brecha). Luzes 2D com `PointLight2D` nas lanternas e janelas.
- **`World/Dungeon/Dungeon.tscn`**: recebe um `DungeonConfig` (rank, tema, seed) e gera salas; `WaveSpawner` dispara ondas; ao limpar, spawna `ExitPortal`. Temas por rank em `DungeonThemes.tres` (ex.: *Esgotos do Mercado*, *Pomar Podre*, *Cais Afogado*, *Cemitério das Lanternas*…).
- **`Player/Player.tscn`**: `CharacterBody2D` + `AnimatedSprite2D` + `Hitbox (Area2D)` + `Hurtbox (Area2D)` + `StateMachine` (Idle, Move, Attack, Hurt, Dead). Arma atual define o `AttackState` (Lâmina primeiro; Arco, Cajado, Martelo, Adagas depois).
- **`Enemies/Enemy.tscn`** base com `EnemyData.tres`; variantes `Lodo` (pula em direção ao jogador), `Espectro` (flutua e persegue), `Bosses/Guardiao` (persegue, telegrafa e investe). Barra de vida só aparece quando ferido.
- **`Items/`**: `Fragment` (loot), `Lantern` (aparece na próxima Brecha se o jogador morreu com Fragmentos), `Echo` (fase 2).
- **`UI/Hud.tscn`**: `RankSeal` (forma + cor por rank), barras HP/XP, contador de Fragmentos, nível da arma, nome do local. **`UI/DialogBox.tscn`** via Dialogic. Tema global em `UI/Theme/vespera_theme.tres`.
- **`Autoload/SaveService`**: `SaveData` (em `Core`) ⇄ JSON em `user://save.json`; autosave ao sair da Brecha, forjar e subir de rank.

---

## 6. Identidade visual (usar exatamente estes valores)

- **Paleta de marca:** índigo profundo `#1B1F3B` (fundo, noite, menus) · roxo-véspera `#4A2C6B` (sombras, painéis, dungeons) · âmbar das lanternas `#F2A541` (luz, XP, ouro, destaque seguro) · luz-de-brecha `#E64A8E` (portais, perigo, dano, bosses) · creme `#F4E7C6` (texto, contornos, UI).
- **Cores de cena:** musgo `#5C8C4A`, terra `#8A5A3C`, pedra `#6B6F8A`, água `#3C7A8A`. HP usa `#E85D5D`.
- **Tipografia:** *Pixelify Sans* 700 para títulos, logo e nomes de boss; *VT323* para HUD, diálogos e inventário (mínimo 28 px na tela final).
- **Logo:** wordmark "VÉSPERA" em Pixelify Sans com o acento do É como uma rachadura emitindo luz magenta. Símbolo: lanterna pixel art com uma fenda luminosa — serve de ícone do app (`icon.png`, 128 e 256) e favicon.
- **Selos de rank** (forma e cor): E octógono `#8A8FA8` · D octógono `#B4C2D4` · C losango `#52E0C4` · B losango `#5B8DEF` · A hexágono `#9B6BD6` · S hexágono `#E64A8E` · SS estrela `#E64A8E` · SSS estrela `#F2A541` · Alvor sol `#F2A541`.
- **Tom:** melancólico mas esperançoso. A cidade é bonita e frágil; os caçadores são a razão de ela existir.

Os arquivos de referência do painel de identidade (logo, paleta, selos, tipografia e mockup da tela) ficam em `docs/brand/`.

---

## 7. Setup local (o README deve ter exatamente isto)

```bash
# 1. Pré-requisitos
#    - .NET 8 SDK  → https://dotnet.microsoft.com/download
#    - Godot 4.x .NET (não a versão padrão) → https://godotengine.org/download
#    - Git + Git LFS (git lfs install)

# 2. Clonar
git clone https://github.com/NasckDev/vespera.git && cd vespera
git lfs pull

# 3. Abrir no Godot: Import → selecionar project.godot → Build (ícone de martelo) → Play (F5)
#    Ou pela linha de comando (GODOT = caminho do executável Godot .NET):
dotnet build
$GODOT --path . --editor      # abre o editor
$GODOT --path .               # roda o jogo
```

Scripts auxiliares em `tools/`: `tools/run.ps1` / `tools/run.sh` (build + play), `tools/export.sh` (export local das 3 plataformas para `build/`).

---

## 8. Build, CI e instalador

- **`ci.yml`**: em cada PR → `dotnet build` + testes GdUnit4 (headless). Usar a action oficial do GdUnit4 com `godot-net: true`.
- **`release.yml`**: em tag `v*` → baixar Godot .NET + export templates da mesma versão, `godot --headless --export-release "Windows Desktop" build/windows/Vespera.exe` (idem Linux e macOS), zipar, publicar como GitHub Release e `butler push build/<plat> <user>/vespera:<canal> --userversion <tag>`. `BUTLER_API_KEY` em Secrets.
- **Export Windows**: embutir o `.pck` no executável (`embed_pck = true`) para distribuir um único `.exe`; ícone = `icon.ico` gerado da lanterna.
- **Instalador** (`installer/vespera.iss`, Inno Setup): instala em `%LocalAppData%\Vespera`, cria atalho, sem admin. Gerar só em release; o app do itch.io cobre a maioria dos testers.

---

## 9. Escopo

### MVP (v0.1 — "andar, caçar, forjar, subir")
- Lumeira com 3 telas (praça com o portal, forja da Odete, rua das casas), ciclo de dia com a véspera abrindo o portal.
- 1 Brecha (rank E), 2 ondas (Lodo, Espectro) + Guardião; dungeon de salas simples a partir de seed.
- 1 arma (Lâmina), 3 níveis de forja; Fragmentos como moeda; Lanterna ao morrer.
- Ranks E → D → C funcionando (XP, selo no HUD, HP máximo sobe).
- Save local, menu inicial, pausa, gamepad.
- Build Windows no itch.io.

### Depois (não implementar sem o time pedir)
Mais armas e Ecos · mais bairros e temas de Brecha · estações · Casas (guildas) · ruptura de Brecha ignorada · ranking público · multiplayer co-op · Steam.

### Marcos
1. **Semana 1:** repo, projeto Godot configurado, jogador andando em Lumeira com colisão, CI verde, build no itch.io.
2. **Semana 2–3:** combate (hitbox/hurtbox), Lodo, Espectro, HUD com HP/XP.
3. **Semana 4–5:** portal, dungeon com ondas, Guardião, Fragmentos, Lanterna.
4. **Semana 6:** forja, ranks, save, menus, polimento de pixel art e som. → **v0.1**

### Divisão do time
- **Alexandre (front):** `src/UI`, tema, HUD, menus, selos, Dialogic, identidade visual no jogo.
- **Dev 2 (.NET):** `src/Player`, `src/Enemies`, combate, state machines, LimboAI.
- **Dev 3 (.NET):** `src/World`, geração da dungeon, portal, `DayCycle`, `Core`, save, CI/CD.

---

## 10. Instruções para o Claude Code

**Primeira sessão — criar o esqueleto, nesta ordem:**
1. Inicializar o repo com `.gitignore` (template Godot + .NET), `.gitattributes` com LFS para os tipos de arte/áudio/fonte, `.editorconfig`.
2. Criar `project.godot` com as configurações da seção 2 (filtro Nearest, 640×360, stretch, pixel snap, input map completo para teclado e gamepad), `Vespera.csproj` com `Godot.NET.Sdk` e `net8.0`, e a estrutura de pastas da seção 3 (com `.gitkeep`).
3. Autoloads: `GameManager`, `EventBus`, `SaveService`, `Logger`.
4. `src/Core`: `RankTable` (tabela da seção 6 com XP: 0, 100, 300, 700, 1500, 3000, 6000, 12000, 25000), `XpSystem`, `DamageCalculator`, `SaveData` — com testes GdUnit4.
5. `Player.tscn` com movimento 8 direções, animação placeholder (ColorRect ou sprite de um pack gratuito), colisão.
6. `Lumeira.tscn` com uma TileMapLayer placeholder e colisões; `Hud.tscn` com HP/XP/RankSeal ligados ao `EventBus`.
7. `README.md` (seção 7), workflows da seção 8, `tools/run.*`.
8. Copiar o protótipo HTML para `docs/prototype/` e os assets de marca para `docs/brand/`.

**Regras de trabalho:**
- Pergunte antes de adicionar dependências que não estão na seção 2.
- Não gere sprites finais "na mão"; use placeholders claramente nomeados (`placeholder_*.png`) e deixe um `assets/README.md` listando o que precisa ser desenhado.
- Prefira cenas pequenas e compostas a cenas gigantes. Prefira `Resources` a JSON solto para dados do jogo.
- Sempre que criar um sistema, crie o teste em `tests/` se ele viver em `Core`.
- Ao terminar uma tarefa, atualize `docs/design/CHANGELOG.md` com uma linha em português.
- Em dúvida de API: documentação do **Godot 4.x C#** (`docs.godotengine.org`, aba C#). Não misture com GDScript nem com Godot 3.
