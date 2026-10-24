# Véspera v2 — plano da beta pública

Escrito em 22/10/2026, depois do playtest do dono na v0.1 ("ruim, mal implementado, feio, desconexo da ideia").
Este documento é o **contrato** da v2: todo agente lê antes de trabalhar. Onde conflitar com documentos antigos
(`CLAUDE.md` da v0.1, `docs/design/DECISIONS.md`, `docs/design/prototype-v2/`, `docs/design/plano-v2.md`), **este vence**.
A fonte da história é `docs/design/GDD.md`. A v0.1 fica congelada na tag `v0.1-final`.

## 1. Por que a v0.1 falhou (não repetir)

| Problema | Causa-raiz | Regra da v2 |
|---|---|---|
| Anda nas paredes, trava em objetos | mapa = pintura IA + máscara de colisão separada | mapa em **tiles**; colisão vem do próprio tile; objeto = cena própria com colisão no pé |
| "Imagem deslizando", "gif é mais fluida" | 1 pose por direção + balanço em código | animação **quadro a quadro** em folhas com grade fixa; nada de mover imagem parada |
| Igual do E ao S | sem arte por estágio | personagem em **camadas** (corpo, roupa, capa, arma, aura) que mudam por estágio |
| Feio, desconexo | escalas misturadas (pintura HD + chibi + ícones) | **uma** grade de pixel, **uma** paleta, filtro Nearest, escala inteira |
| Não segue a ideia | seguiu o protótipo HTML | segue o **GDD**: Ano Sem Alvor, Guardiões, NPCs, Reflexo, Alvor |

## 2. Padrões técnicos (travados)

> **Revisão 22/10, 14:30 (vale mais que o resto desta seção):** o dono vetou a arte "feita por código" ("cara de Paint,
> quadrado cru; a v1 era mais bonita"). A v2 **usa a arte pintada do dono** (estilo das imagens em
> `Downloads\Vespera` e `docs/brand/`), e o que mudou foi a **estrutura**, não o estilo:
> - **Resolução base 1920×1080**, `stretch canvas_items`, filtro **linear** (a arte é pintada em alta resolução).
> - **Cenário = chão limpo pintado** (sem objetos) **+ objetos e prédios como sprites RGBA separados**, cada um com a
>   pegada no pé. A colisão é uma **grade de 48 px desenhada por nós** sobre o chão limpo (camada invisível de TileMap)
>   mais as pegadas dos objetos, e um bot valida tudo. A pintura achatada com máscara automática está proibida.
> - **Personagens e monstros:** folhas de animação quadro a quadro no estilo pintado, encomendadas pelas issues
>   `arte-v2` (GPT/Gemini), com grade e âncora fixas. Personagem com ~128 px de altura em célula 192×192, pé em (96,176).
>   Enquanto a folha não chega, entra a melhor folha disponível, nunca retângulo.
> - **UI:** molduras ornamentadas 9-patch (kit de UI em issue), key-art no título. Nada chapado.
> - Os itens abaixo sobre 480×270, tile 16 px, Nearest e arte por código ficam **revogados**.

- **Engine:** Godot 4.7.2 .NET, C# (`C:\Godot\Godot_v4.7.2-stable_mono_win64.exe`). Desktop Windows primeiro; Linux/macOS no CI.
- **Resolução base:** 480×270, `stretch_mode=viewport`, escala **inteira** (2× 960×540, 4× 1080p, 5× 1440p com borda, 8× 4K). Filtro **Nearest**, snap de pixel.
- **Grade:** tile **16×16**. Personagem humano: corpo ~20×30 px numa **célula 48×48** (sobra para arma e golpe). Monstros comuns em células 32/48/64; Guardiões 96–160.
- **Perspectiva:** 3/4 top-down (chão visto de cima, objetos e personagens de frente), luz vinda de cima-esquerda.
- **Paleta:** única, em `tools/art/palette.json`, com rampas que variam o matiz (marca: índigo `#1B1F3B`, roxo-véspera `#4A2C6B`, âmbar `#F2A541`, luz-de-brecha `#E64A8E`, creme `#F4E7C6`; cena: musgo `#5C8C4A`, terra `#8A5A3C`, pedra `#6B6F8A`, água `#3C7A8A`). Contorno escuro seletivo, nunca preto puro.
- **Animação do jogador** (por classe e estágio, 4 direções: baixo, cima, lado; esquerda espelha o lado):
  idle 4q @6fps · andar 8q @12fps · esquiva 5q · combo leve 3×5q · pesado (carga 2q em loop + golpe 6q) · habilidade 6q · dano 2q · morte 7q.
  Cada ataque tem **antecipação → impacto → recuperação**; a hitbox liga só nos quadros de impacto (evento de quadro nos dados).
- **Animação de monstro:** idle 4 · mover 6 · aviso 3 (segura) · ataque 4 · dano 2 · morte 6; lado (espelha) + baixo/cima quando o verbo exigir.
- **Game feel:** pausa no impacto (hit-stop) de 40–120 ms conforme o peso, flash branco 1 quadro, recuo, tremor curto que decai, partículas e som em todo golpe. Aviso âmbar de 0,4–0,8 s antes de todo golpe forte de chefe.
- **Profundidade:** entidades em `y_sort`; pé = origem; oclusão por transparência quando o jogador passa atrás de algo alto.
- **Arte:** gerada por **código** em `tools/art/` (Python + Pillow + numpy): peças desenhadas pixel a pixel, montadas por um esqueleto de poses, e exportadas como folhas PNG + `SpriteFrames .tres`. A arte pintada antiga só entra **convertida** para a grade e a paleta (script de pixelização) e só se ficar boa; retratos de diálogo podem usar os bustos pintados.
- **Áudio:** sons sintetizados no começo (`tools/audio/`), um por ação; música depois.

## 2.1 Contrato de escala (obrigatório em todo mapa, personagem, monstro e objeto)

Pedido do dono (22/10): "tamanho dos players, monstros e NPC dentro do mapa tem que fazer sentido com o cenário, não só juntar".

- **O mapa é a régua.** Cada `data/levels/<id>.json` declara `doorHeight` em px de mundo, medido numa porta pintada do
  chão (ou, num mapa sem porta, na altura de um elemento equivalente: arco, grade de esgoto, bancada = 0,45 porta).
- **Altura humana do mapa** = `1,0 × doorHeight` (revisão do playtest de 23/10: "mapas enormes, todo o resto minúsculo"; antes era 0,75).
  Tudo deriva dela; não existe `actorScale` escolhido à mão por mapa.
- **Câmera:** o zoom sai da régua para o caçador ter **~140 px de altura numa tela de 1080p** em todo mapa (aceito 130–150 px,
  conferido por `--camera`; na apresentação do Guardião a câmera pode se afastar até 0,8×).
- **Tabela de alturas** (altura do corpo, pés à cabeça, sem arma nem efeito), em múltiplos da altura humana:

  | Ator | Altura | Ator | Altura |
  |---|---|---|---|
  | Caçador (Lâmina, Arco, Martelo) | 1,00 | Lodo | 0,55 (na cintura; revisão do painel de 23/10: a 0,35 sumia na tela) |
  | Odete, Ravi, Nilza | 1,00 | Espectro | 1,00 (flutua 0,15 acima do chão) |
  | Benício (curvado) | 0,95 | Lodo-Mãe (Guardião) | 3,00 |
  | Lis (criança) | 0,75 | Objetos | medidos contra o equivalente pintado no chão |

- **No código:** um registro único (`ScaleRegistry`) calcula a escala do sprite = altura-alvo ÷ altura opaca medida no
  JSON da tira (`standing_height`). Um ator novo entra só com uma linha na tabela.
- **Teste automático** (headless, roda no CI): para cada mapa, instancia cada ator que pode aparecer nele, mede a altura
  opaca renderizada em px de mundo e falha se fugir mais de ±8% do alvo. Objetos: idem contra a medida registrada.
- **Prova visual por mapa:** captura "fila de escala" com caçador ao lado da porta, de cada NPC e de cada monstro daquele
  mapa, revisada pelo coordenador antes de qualquer entrega.

## 3. Arquitetura de código

```
src/Core/        regras puras sem Godot (reaproveitado da v0.1 e enxugado): Ranks, Combat math, Loot, Save, Data, Rng
src/Game/
  Boot/          Main, SceneRouter (troca de cena com fade), Settings, InputSetup (teclado + gamepad, remapeável)
  World/         Level (TileMapLayer chão/parede/detalhe + YSort), Portal, CameraRig, Interactable
  Actors/        Player, Enemy (base + comportamento por verbo), Npc
  Components/    Health, Hitbox, Hurtbox, Knockback, AnimDriver (eventos de quadro), Telegraph
  Dungeon/       gerador de salas por rank e tema, WaveDirector, Loot drop
  Story/         Campaign (atos, flags), Dialogue (falas em JSON), Quests
  UI/            Title, Options, Controls, Help, Pause, HUD, Inventory, Forge, Dialog
  Net/           (camada 8) co-op local, depois online
data/            JSON de conteúdo (classes, monstros, itens, falas, dungeons)
tools/art/       pipeline de pixel art   tools/tiles/   gerador de tileset   tools/audio/   sons
tests/           xUnit do Core + testes de cena headless (bot de colisão, smoke)
```

Regras: Core não referencia Godot; nenhum script > 400 linhas; comunicação por sinais/EventBus; dados de balanceamento em JSON; o jogador é dirigido por uma **fonte de input** (teclado, gamepad N ou rede), nunca por `Input` direto, para o co-op entrar sem refazer nada.

## 4. Camadas (uma de cada vez; só avança com a anterior aprovada)

| # | Camada | Pronto quando (DoD) |
|---|---|---|
| A | **Pipeline de arte (prova)** | Lâmina estágio 1 com todas as animações nas 4 direções, GIF de prévia aprovado na revisão visual; 1 tileset de cidade com bordas automáticas |
| 0 | Fundação | Título (História, Co-op local, Continuar, Opções, Controles, Ajuda, Créditos, Sair), pausa, save, remapear teclas, janela/escala |
| 1 | **Piso** | Praça + 1 Brecha em tiles; jogador anda nas 4 direções com animação real; **bot de colisão** tenta atravessar toda parede e falha; câmera presa ao mapa |
| 2 | Objetos | lampiões, barracas, árvores, caixas, baús, portas: colisão no pé, y-sort, oclusão, interação com som e animação |
| 3 | Dungeons | Brechas por rank (E→Alvor) e tema de bairro, salas geradas a partir de seed, portal de entrada/saída, minimapa |
| 4 | Monstros | 1 verbo por monstro (GDD §4), aviso legível, partes quebráveis, drops por rank (§6) |
| 5 | NPCs | Odete, Benício, Nilza, Ravi, Lis, Celeste, Severo, Vigia: andam, têm função (forja, exames, comida, mapas, poções, pesca, Ecos) |
| 6 | Falas e campanha | atos da §5 jogáveis do início ao fim, com Guardiões e exames de rank |
| 7 | Classes | Lâmina, Arco, Martelo completos com 5 estágios visuais, habilidades e armas por rank; Cajado e Adagas depois |
| 8 | Multiplayer | co-op local 2P (câmera compartilhada); depois online 2–4 com host (ENet) |
| 9 | Beta pública | balanceamento, acessibilidade, instalador, itch.io |

Cada camada: implementação (Sonnet) → testes automáticos → **tester jogador** (nota ≥ 8, prints em 720p/1080p/4K) → correções → aprovação do coordenador. Bug reaberto sobe de severidade.

## 5. Campanha — Parte 1 ("até o Alvor")

| Ato | Rank | Bairro → Brecha | Guardião | Gancho de história |
|---|---|---|---|---|
| Prólogo | E | chegada a Lumeira, Praça | — | lanterna herdada, selo E no pulso, Benício registra o jogador, Ravi aparece |
| I | E | Bairro das Forjas → Esgotos do Mercado | Lodo-Mãe | Odete desconfia; primeira forja |
| II | D | Pomar Alto → Pomar Podre | Macieira Oca | Celeste; o pomar como era antes |
| III | C | Mercado das Marés → Cais Afogado | Farol Afogado | **Exame C**; Severo e o farol; lanterna de Marisa |
| IV | B | Cemitério das Lanternas → Cemitério | Velória | o Vigia e suas perguntas; Ravi em perigo (Brecha B) |
| V | A | Torre do Sino → Torre do Sino Mudo | Sineiro Mudo | **Exame A**; o segredo de Benício |
| VI | S | Ruas Baixas → Ruas Invertidas | Inquilino | **Exame S**; Nilza e a Sopa da Véspera |
| VII | SS–SSS | Avesso profundo (Brechas que misturam bairros) | variações dos Guardiões | **Exame SSS**; o Avesso cresce |
| Final | Alvor | Praça da Lanterna (a primeira Brecha da praça) | **O Reflexo** | o sino toca de novo → rank **Alvor** → fim da Parte 1 e gancho da Parte 2 |

### 5.1 Ato I jogável (revisão do playtest de 23/10: meta de 25–35 min)

Roteiro em `data/dialogue/story.json` (objetivo atual sempre visível em 1 linha; caçada mostra sala, onda e contratos):
chegada (lanterna herdada, selo E, a Brecha se abre) → Benício explica por que se caça (Fragmentos mantêm as lanternas
acesas) → Ravi (aposta) → Odete dá a arma da classe escolhida no jogo novo → **caçada 1** (5 salas, termina num covil de
elite) → Tia Nilza vem dar a primeira poção → Odete melhora a arma → Lis explica as salas, contratos no mural →
**caçada 2** → Ravi (rival) → **exame do Benício** (2 caçadas completas, arma forjada, XP do rank D; diz o que falta) →
a Brecha fica vermelha → **caçada da Lodo-Mãe** (5 salas) → rank D, Eco Viscoso (Q), Odete reconhece a lanterna da
Marisa, carta da Dona Celeste e a lanterna apontando para o Pomar Alto (gancho do Ato II). Cair na Brecha volta à Praça
(Fragmentos ficam na Lanterna); o bot sem invulnerabilidade cai 0–1 vez com Lâmina/Martelo.

O Alvor é "impossível para muitos": a Brecha da praça exige SSS, conjunto de armadura de Guardião e 2 Ecos, e o Reflexo copia o estilo de luta do próprio jogador (estatísticas registradas desde o prólogo).

## 6. Regras de progressão e dificuldade

- **Ranks:** E, D, C, B, A, S, SS, SSS, Alvor. Sobe por XP **e** por conquista: matar o Guardião do rank e passar no exame quando houver (C, A, S, SSS). Cada rank: +HP e novo selo.
- **Estágios visuais (5):** E–D aprendiz · C–B caçador · A–S veterano · SS–SSS elite · Alvor radiante.
- **Loot por Brecha de rank R:** 80% material/equipamento R · 15% rank R+1 · 5% raro R (com afixo). Guardião: garante material R + 30% de peça do conjunto dele.
- **Poder recomendado** visível em cada Brecha; monstros escalam por rank (HP, dano, velocidade e **padrões novos**, não só números).
- **Equipamento:** arma (sobe de nível na forja com Fragmentos + material), armadura por conjunto de Guardião (bônus de 2 e 4 peças), 2 Ecos.
- **Morte (Lanterna):** os Fragmentos da caçada ficam na Brecha; o equipamento nunca é perdido. No co-op, um aliado pode recuperar a Lanterna.
- **Caçadas (Ato I, `data/cacada/cacada.json`):** salas sorteadas por semente (combate, elite, tesouro, descanso com
  lanterna que cura 50%, covil); a 1ª sala é só de Lodos; as ondas crescem um pouco a cada caçada completa; barris e
  caixotes quebram e dão Lumes; 3 contratos no mural por volta (caçada, coleta, proteção, sem dano) pagam XP,
  Fragmentos e Lumes; poções da Tia Nilza (14 Lumes, até 3, tecla R, curam 40%); tela de resultado ao voltar.
- **Portão do Guardião:** a Lodo-Mãe só aparece depois do exame do Benício (flag `exame_e`).
- **Economia:** Fragmentos (Avesso → forja, Ecos) e Lumes (cidade → comida, poções, mapas).

## 7. Classes (3 primeiro)

| Classe | Ritmo/alcance | Verbo único | Evolução (habilidade nova por estágio) |
|---|---|---|---|
| Lâmina | médio, equilibrada | combo de 3 + corte giratório | investida → aparar → lâmina de luz → corte duplo da véspera → Lâmina de Duas Vésperas |
| Arco | longo, kiting | flecha carregada perfura | rolar e atirar → flecha de luz → chuva de flechas → marca do caçador → feixe do Alvor |
| Martelo | lento, curto, pesado | quebra partes em dobro, hit-stop forte | tremor → escudo de bronze → martelada de âmbar → onda sísmica → queda do sino |

Ajuste do rank E (playtest 23/10, bot sem invulnerabilidade): dano base Lâmina 12, Arco 16 (flecha empurra mais),
Martelo 22; mouse mira o golpe no cursor (o Arco gira a mira na carga).

## 8. Fora do escopo da beta

Horta, pesca jogável, estações, festivais, Casas/guildas, relação 0–5 completa (entra versão simples), Cajado e Adagas (camada 7b), online acima de 4 jogadores.
