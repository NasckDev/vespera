# Guia de arte para agentes de imagem — Véspera

Leia isto **antes** de gerar qualquer arte para uma issue com a label `arte`. Ele resume o que foi aprovado e o que foi
reprovado nas rodadas de validação v1–v4 (21/10/2026). Cada issue traz o pedido específico; este guia traz as regras que
valem para todas.

## 1. Estilo (o que o jogo já é)

- **Pixel art ilustrada, vista top-down 3/4**, iluminação quente de lanterna (âmbar) contra noite índigo/roxa, com o
  magenta da Brecha como cor de perigo. Contorno escuro de 1 px, sombreamento por blocos, sem suavização.
- **Paleta de marca** (CLAUDE.md §6): índigo `#1B1F3B`, roxo-véspera `#4A2C6B`, âmbar `#F2A541`, luz-de-brecha `#E64A8E`,
  creme `#F4E7C6`. Cena: musgo `#5C8C4A`, terra `#8A5A3C`, pedra `#6B6F8A`, água `#3C7A8A`.
- **Referências de estilo aprovadas:** caçador da folha `sprite_sheet_do_caçador_crepuscular` (super kit) e a caminhada
  da Lâmina v3/v4; chefes angler v3 e Ceifador/Colosso/Rainha/Arauto (v4). **Novos personagens devem seguir ESTE estilo**
  (detalhado, escuro, capa/armadura texturizada), não o estilo chibi claro dos NPCs antigos do atlas.

## 2. Escala e enquadramento (medidas reais do jogo)

| Coisa | Medida no jogo | Observação |
|---|---|---|
| Porta da Rua da Forja | 64 u | referência de altura do mundo |
| Caçador (jogador) | ≈ 56,7 u | `characterScale = 1,75` (rules.json) |
| NPC humano | igual ao caçador (±10%) | criança menor |
| Monstro pequeno / médio / grande | 0,5× / 1× / 1,3× o caçador | |
| Chefe | 1,6–2,5× o caçador | silhueta própria, maior que o comum |
| Barril | ≈ 0,45× o caçador | lampião de rua ≈ 1,6×, barraca ≈ 2×, baú ≈ 0,4× |

**Formatos de quadro aprovados (use exatamente):**

| Tipo | Canvas | Pivô (pé) | Ritmo |
|---|---|---|---|
| Caçador (todas as classes) | 420×420 RGBA | (210, 412) | andar 6 quadros, 9 fps em loop; esquiva 6 quadros (~0,17 s) sem loop |
| Chefe | 820×490 RGBA | (410, 462) | poses estáticas por estado; animação 4–6 quadros |
| Angler (chefe) | 620×465 RGBA | (310, 445) | |
| Baú | 126×110 / 156×150 RGBA | pé no centro da base | estados: fechado, abrindo 1, abrindo 2, aberto, vazio |
| Ícone de HUD | 64×64 RGBA | — | moldura do `hud-kit.png` |
| Retrato de diálogo | 128×128 ou maior | — | busto (cabeça e ombros) |

- **Todos os quadros de uma animação têm o MESMO canvas e o MESMO pivô.** O personagem não pode crescer, encolher ou
  "pular" ao trocar de quadro ou de direção.
- Entregue em alta resolução se quiser; o jogo reduz. **Nunca** entregue menor que o canvas acima.

## 3. O que é PROIBIDO (motivos de reprovação nas rodadas anteriores)

1. **Animação por deformação**: repetir a mesma pose com tremor de 1–2 px, rotação, escala ou "warp". Animação precisa de
   **poses novas desenhadas** (pernas alternando, corpo mudando).
2. **Trocar uma direção por outra**: usar a pose de frente como "lado" ou "costas". Costas mostram as costas (sem rosto).
3. **Partes flutuando**: coroa, barbatana, arma ou acessório solto sem ligação com o corpo (reprovado no angler v2).
4. **Inpainting de chão** e recortes que levam o chão junto (retângulos/elipses com piso). Objetos têm fundo 100% transparente.
5. **Texto, rótulos, números ou marcas d'água** dentro da imagem (inclusive em inglês, como "Harbor District").
6. **Halo/franja** clara ou escura nas bordas; fundo que não é transparente; JPG.
7. **Escala inconsistente entre direções** (lado menor que frente) ou entre quadros.
8. **Materiais trocados entre estados** (tampa de madeira marrom colada num baú roxo).
9. "Segunda expressão" que é a mesma imagem; "busto" que é o corpo inteiro reduzido.
10. Inventar personagens/criaturas fora do GDD ou mudar a identidade de quem já foi aprovado.

## 4. Entrega

- PNG RGBA, nomes em inglês `snake_case`, uma pasta por personagem/objeto, com as mesmas pastas do repositório
  (`assets/sprites/animations/player/<classe>/walk_front_00.png`, `.../enemies/<id>/...`, `.../props/...`).
- Um `README.md` curto com: o que mudou, canvas, pivô, fps, e **o que ainda não está pronto** (honestidade vale mais).
- Uma prancha de pré-visualização por item (todas as poses lado a lado na mesma escala) para validação rápida.
- Coloque o zip em `Downloads\Vespera` e avise: cada item é validado quadro a quadro e volta com veredito na issue.
