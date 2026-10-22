# Plano da v0.2 — "Véspera de verdade"

Escrito em 22/10/2026 depois do playtest do dono na v0.1: *"o player está sempre igual, as armas iguais, do E ao S fica a
mesma coisa, parece imagens travadas mexendo; uma gif é mais fluida"*. Ele tem razão. A v0.1 provou que os **sistemas**
funcionam (ciclo completo, 521 testes), mas não entrega **sensação de progresso nem animação de jogo profissional**.
A v0.2 é reconstruída em cima do que ele desenhou, nem que partes sejam refeitas do zero.

## 0. Barra de qualidade ("pronto" de verdade)

Nada vai para o dono sem passar **nos dois**:
1. **Gravação**: sequência de quadros/GIF de 3–5 s de cada mecânica visual, olhada pelo coordenador e comparada com as
   referências abaixo. Se uma GIF de referência é mais fluida, reprova.
2. **Playtest humano simulado** em janela fora da tela (nunca na tela do dono), com nota ≥ 8 de um tester que joga como
   amigo que nunca viu o jogo.

Referências de padrão (pesquisa 22/10): animação de personagem jogável com **6–8 quadros** de caminhada (4 é o mínimo),
ataque com **antecipação → golpe rápido (~50 ms) → recuperação**, **hit-stop de 50–120 ms** no golpe forte, tremor pequeno
com decaimento, flash, partículas e som coerente — os três maiores fatores de "impacto" são hit-stop, som e câmera.
Progressão visual: o jogador de nível alto tem que ser **reconhecível só pela aparência** (silhueta, armadura, aura, postura).

## 1. Evolução visual por Selo (o pilar da v0.2)

Fonte: imagem **20** (Lâmina em 5 estágios, RGBA, estilo aprovado) e pranchas **55–57** (5 classes × níveis 1/15/30/50 ×
parado/andar/ataque/habilidade).

| Estágio | Selos | Nome (prancha) | O que muda no visual |
|---|---|---|---|
| 1 | E, D | Aprendiz | roupa de tecido, capa curta, lanterna simples |
| 2 | C, B | Caçador | ombreiras e braçadeiras de couro/metal, emblema da Lanterna |
| 3 | A, S | Veterano | armadura escura com detalhes âmbar, capa longa, arma com brilho, aura leve |
| 4 | SS, SSS | Elite | armadura ornamentada, peles/penas, brilho magenta/âmbar, partículas |
| 5 | SSSS, Alvor | Alvor | dourado radiante, halo de luz, lanterna grande, rastro de luz ao andar |

- Cada estágio é um **conjunto de animação completo** por classe (parado, andar 3 direções, esquiva, ataque leve/pesado,
  habilidade, dano, morte). Enquanto a arte de todos não chega, o estágio 1 usa os quadros atuais e os estágios
  superiores ganham **camadas** (capa, ombreira, aura, brilho de arma) desenhadas sobre a animação base — nunca deformação.
- **Subir de Selo é um evento**: tela de transformação com o antes/depois (imagem 20), som e luz, e o personagem no
  mundo troca de visual na hora.
- HUD, inventário (aba Selos), título e retrato usam o estágio atual.

## 2. Armas que evoluem

- Cada **nível da forja** muda a arma visível na mão do personagem: brilho, cor do rastro do golpe e partículas
  (nível 1 aço opaco → nível 3 lâmina âmbar → nível máximo luz da Brecha).
- **Infusão elemental** muda a cor do golpe e do rastro (ígneo, gélido, vazio…).
- Os 146 itens do Arsenal (docs/design/arsenal) entram como equipamentos com **ícone e visual por rank**; equipar armadura
  muda a camada de armadura do personagem quando houver arte.

## 3. Animação e sensação de jogo

- Máquina de estados de animação no jogador e nos monstros (parado → andar → antecipação → golpe → recuperação),
  10–12 fps na caminhada, quadros-chave segurados no ataque, *smear* no golpe, poeira e rastro na esquiva.
- Monstros: respiração/flutuação no parado, telegrafia legível, recuo e flash ao levar dano, morte com quadros próprios.
- Câmera: leve antecipação na direção do movimento, *punch* no golpe, tremor configurável.
- Objetos: tudo que é interativo reage visualmente (tremor, rachadura, abrir, acender).

## 4. Mundo remontado com objetos de verdade

- Interiores (Guilda, Estalagem, Forja) refeitos **do zero** com as plantas limpas **N22–N24** + móveis **N21**: cada
  móvel é um objeto com colisão, oclusão e interação.
- Rua da Forja e Brechas: objetos das folhas **59/61/63/65/67** posicionados como peças independentes (barracas, barcos,
  lampiões, estátuas) onde a arte permitir; chão limpo quando o designer exportar as camadas (issue #4).
- Mais coisas para fazer na rua: baús escondidos com recompensa que importa, eventos da véspera, NPCs que reagem.

## 5. A história do GDD dentro do jogo

O jogo seguiu o protótipo v2 (Guilda, Brás, Ivo, Vesna). A v0.2 costura o **GDD** por cima: o Ano Sem Alvor contado na
introdução e no diário, os Guardiões com os nomes do GDD, os arcos dos NPCs (Odete/Marisa, Benício e o sino, Ravi rival,
Lis), relação com NPCs (0–5 lanternas) e os temas (luz contra esquecimento, comunidade, subir é um fardo). Detalhamento a
partir do relatório de vivência (docs/design/relatorio-vivencia-v0.1.md).

## 6. Processo

- O coordenador **joga, grava e reprova** cada entrega antes de chegar ao dono; testes automatizados continuam, mas não
  definem "pronto".
- Arte que falta vira issue com especificação completa (guia-arte-agentes.md) e critério de aprovação.
- Janelas de teste sempre fora da tela do dono.
