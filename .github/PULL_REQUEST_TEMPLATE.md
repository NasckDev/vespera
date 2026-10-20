## O que muda

<!-- Uma ou duas frases. Ex.: "Adiciona o Lodo com pulo a cada 1,2 s e parte quebrável (Núcleo)." -->

Fecha #

## Como testar

<!-- Passo a passo no jogo. Ex.: Novo jogo → Brecha das Forjas → quebrar o núcleo de um Lodo. -->

## Mídia

<!-- GIF ou print se mudou algo visual. Obrigatório para mudanças visuais. -->

## Checklist

- [ ] `dotnet build Vespera.sln -warnaserror` sem avisos
- [ ] `dotnet test tests/Vespera.Tests.csproj` verde
- [ ] `godot --headless --path . -- --smoke-test` verde (cenário novo se a mecânica é nova)
- [ ] Lógica de regra em `src/Core` com teste; números de balanceamento em `.tres`
- [ ] Funciona no teclado **e** no gamepad
- [ ] Linha adicionada em `docs/design/CHANGELOG.md`
