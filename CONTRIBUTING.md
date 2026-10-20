# Contribuindo com o Véspera

Que bom que você quer ajudar a manter as lanternas acesas! 🏮
Este guia cobre o essencial; as regras completas de arquitetura estão no [`CLAUDE.md`](CLAUDE.md).

## Antes de começar

1. Leia o [GDD](docs/design/GDD.md) e as [decisões do projeto](docs/design/DECISIONS.md).
2. Procure uma issue com `good first issue` ou `help wanted`, ou abra uma descrevendo o que quer fazer.
3. Comente na issue que vai pegá-la. Evita duas pessoas no mesmo trabalho.

## Ambiente

| Ferramenta | Versão |
|---|---|
| [Godot .NET](https://godotengine.org/download) (não a versão padrão) | **4.7.2** |
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 ou superior |
| Git + [Git LFS](https://git-lfs.com) | qualquer recente |

```bash
git clone https://github.com/NasckDev/vespera.git && cd vespera
git lfs install && git lfs pull
dotnet build Vespera.sln
dotnet test tests/Vespera.Tests.csproj
```

Abra o `project.godot` no Godot .NET e aperte **F5**. Scripts prontos: `tools/run.ps1` (Windows) e `tools/run.sh`.

## Fluxo de trabalho

1. Faça um fork (ou branch, se for do time) a partir de `main`: `feat/forja-nivel-3`, `fix/colisao-cais`, `art/lodo-mae`.
2. Commits **em português, no imperativo, curtos**: `adiciona state machine do jogador`.
3. Abra o PR cedo como *draft* se quiser feedback.
4. O PR precisa de: **CI verde**, testes para lógica nova em `src/Core`, e **GIF ou print** se mudar algo visual.

## Antes de abrir o PR

```bash
dotnet build Vespera.sln -warnaserror        # zero avisos
dotnet test tests/Vespera.Tests.csproj        # lógica pura
godot --headless --path . -- --smoke-test     # o jogo joga sozinho e verifica invariantes
```

O smoke test (`src/Testing/SmokeTest.cs`) roda cenários reais: carrega a cidade, anda contra as paredes do
mapa, entra na Brecha, luta e volta. Se você criou uma mecânica nova, **adicione um cenário**.

## Regras de código (resumo)

- **`src/Core` não usa Godot.** Regras de jogo (ranks, dano, loot, save) vivem lá e têm testes GdUnit4.
- Sistemas conversam pelo **`EventBus`**. Nada de `GetNode("../../..")`.
- Balanceamento em **Resources `.tres`** (`src/Data`), nunca número mágico em script.
- Nada de `GD.Print`: use `Log.Info/Debug/Warning/Error`.
- Física em `_PhysicsProcess`; input só por ações nomeadas (`InputActions`). Teclado **e** gamepad.
- Sem alocação por frame em `_Process`/`_PhysicsProcess` (o jogo precisa rodar liso em PC modesto).

## Arte e áudio

- Pixel art em PNG sem compressão com perda, filtro **Nearest**.
- Mapas são **pinturas + máscara de colisão** (branco = andável, preto = bloqueado, 1 px da máscara = 8 px do mapa).
  Se você repintar um mapa, **atualize a máscara junto**.
- Placeholder sempre com prefixo `placeholder_` e listado em [`assets/README.md`](assets/README.md).
- Todo asset enviado fica sob [CC BY-NC-SA 4.0](LICENSE-ASSETS.md). Não envie arte de terceiros sem licença compatível.

## Git LFS

PNG, áudio, fontes e arquivos de arte vão para o LFS automaticamente (veja `.gitattributes`). Se o `git status`
mostrar um PNG como texto modificado, rode `git lfs install` e faça checkout de novo.

## Dúvidas

Use as [Discussions](https://github.com/NasckDev/vespera/discussions). Bugs e ideias concretas viram issues.
