# VÉSPERA — Equipamentos, receitas e animações

**Conteúdo:** 140 equipamentos principais (14 por rank, em 10 patamares), 6 bônus meme; cada item tem poderes, receitas, materiais ligados aos monstros criados no bestiário, stats propostos, animação/frames/fps e IDs para implementação.

## Regras do jogo
- **Sem classe fixa:** o jogador troca o estilo de combate equipando lâmina, arco, cajado, martelo ou adagas.
- **Ranks:** E, D, C, B, A, S, SS, SSS, SSSS, ALVOR. Rank e nível são distintos; os níveis no JSON são apenas sugestões de balanceamento.
- **Slots:** arma, cabeça, peito, mãos, pernas, botas, capa, lanterna, até dois anéis e amuleto.
- **Drops:** cinco dungeons originais, com 25 monstros (15 comuns + 5 elites + 5 chefes) e materiais. Valores de drop/chance ainda devem ser definidos.
- **Customização:** variantes cromáticas; ranuras rúnicas escalonadas; Ecos são habilidades únicas de boss e não viram classes fixas.

## Para o Claude implementar
1. Ler `vespera_catalogo_146_itens.json` e expor tipos TypeScript para `Item`, `Recipe`, `ItemAnimation` e `MaterialSource`.
2. Implementar um `EquipmentSystem` com slots, validação de rank e troca de família de arma.
3. Implementar um `CraftingSystem` que consulte `materiais`, verifique inventário, moedas e desbloqueios, e consuma ingredientes.
4. Implementar animações de personagens **em camadas**, sincronizando armas nas âncoras por direção (4 direções) e ação, mantendo 64px de resolução lógica como ponto de partida.
5. Para sprites finais, separar manualmente os personagens e efeitos das pranchas ilustrativas; esses posters **não são** atlas prontos, com frames transparentes e tempos exatos.
6. Renderizar com `image-rendering: pixelated` (ou textura com NEAREST), evitando suavização e arte borrada.
7. Preservar compatibilidade com os cenários top-down 3/4 e as proporções dos NPCs do projeto.

## Estrutura
- `vespera_catalogo_146_itens.json`: fonte de verdade, receitas e animações.
- `vespera_itens_146.csv`: leitura e revisão no Excel.
- `pranchas/`: artes de referência por ranking; ilustrativas, não sprites exportados.

## Exemplo de uso (TypeScript)
```ts
import catalogo from './vespera_catalogo_146_itens.json';
const espada = catalogo.itens.find(x => x.rank === 'A' && x.slot === 'lamina');
const dropOrigem = espada?.receita.materiais.map(x => `${x.monstro} — ${x.dungeon}`);
console.log(espada?.nome, dropOrigem);
```

**Balanceamento:** custos e números são um protótipo.

## Visualizador e assets prontos
- `VISUALIZADOR_ABRIR_NO_NAVEGADOR.html` funciona localmente sem internet; abra em um navegador.
- `arsenal_140_itens_4K.png` apresenta todos os 140 equipamentos em 3840 × 2160.
- `icones_64px/` contém 140 PNG RGBA transparentes, pixel art simples de protótipo, desenhados com silhuetas por slot e cor por rank.
- `animacoes_prototipo/` contém 50 spritesheets PNG com oito frames cada e 50 GIFs de prévia para os 5 estilos de arma × 10 ranks. **São animações prototipadas**, não substituem os sprites refinados do estilo final dos NPCs.
- `pranchas/` contém as referências ilustrativas geradas para direção artística. Os textos nessas pranchas são conceituais; **o JSON é a fonte de verdade** para nomes, drops e receitas.
