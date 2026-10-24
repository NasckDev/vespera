# Parte C · Monstros do Ato VII (Avesso profundo)

**O que é:** os 2 monstros comuns novos do **Ato VII** (CAMPANHA.md), 5 tiras cada (parado, mover, aviso, ataque, morte). **Ceifador de Pomar** é o *vigia*: só se move quando NÃO está no cone da lanterna, por isso o `parado` é uma pose congelada no meio do passo. **Sombra de Caçador** é o eco de um caçador caído que usa o verbo de uma classe; para isso vem em 3 variantes (Lâmina, Arco, Martelo), com o mesmo corpo e a arma de cada classe. Tiras horizontais, fundo verde #00FF00, vista de lado olhando para a direita; saída `<monstro>_<anim>.png`. Quadros: parado 4, mover 6, aviso 3, ataque 4, morte 6 (padrão dos comuns da #30). O CAMPANHA.md não define o rank desses dois; o título usa só "Ato VII".

Um prompt por imagem. Gere primeiro o `parado` de cada monstro e anexe-o nos pedidos seguintes do mesmo monstro. Nas 3 variantes da Sombra, anexe também o `parado` aprovado da primeira variante para manter o mesmo corpo.

### C-01 · `ceifador_pomar_parado.png` · Ceifador de Pomar (comum, Ato VII): parado

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [estilo de chefes](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: an orchard HARVESTER echo, a tall gaunt scarecrow-like reaper from the Rotten Orchard: a head made of an old burlap apple sack with two stitched eye holes and a faint magenta glow inside, a long patched coat stuffed with straw, thin twig fingers, holding a long wooden pole with a curved iron PRUNING HOOK at the end (an orchard sickle), rotten purple apples hanging from its belt. SCALE: about 1.4x the hunter height, very thin.
Animation: frozen still in an awkward mid-step pose, only the sack head trembling slightly (it freezes when the lantern light is on it). Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-02 · `ceifador_pomar_mover.png` · Ceifador de Pomar (comum, Ato VII): mover

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [estilo de chefes](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: an orchard HARVESTER echo, a tall gaunt scarecrow-like reaper from the Rotten Orchard: a head made of an old burlap apple sack with two stitched eye holes and a faint magenta glow inside, a long patched coat stuffed with straw, thin twig fingers, holding a long wooden pole with a curved iron PRUNING HOOK at the end (an orchard sickle), rotten purple apples hanging from its belt. SCALE: about 1.4x the hunter height, very thin.
Animation: fast jerky long strides forward, the coat flapping, straw falling. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-03 · `ceifador_pomar_aviso.png` · Ceifador de Pomar (comum, Ato VII): aviso

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [estilo de chefes](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: an orchard HARVESTER echo, a tall gaunt scarecrow-like reaper from the Rotten Orchard: a head made of an old burlap apple sack with two stitched eye holes and a faint magenta glow inside, a long patched coat stuffed with straw, thin twig fingers, holding a long wooden pole with a curved iron PRUNING HOOK at the end (an orchard sickle), rotten purple apples hanging from its belt. SCALE: about 1.4x the hunter height, very thin.
Animation: warning: raises the pruning hook high behind its head, the curved blade glows bright amber. Exactly 3 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 3 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-04 · `ceifador_pomar_ataque.png` · Ceifador de Pomar (comum, Ato VII): ataque

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [estilo de chefes](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: an orchard HARVESTER echo, a tall gaunt scarecrow-like reaper from the Rotten Orchard: a head made of an old burlap apple sack with two stitched eye holes and a faint magenta glow inside, a long patched coat stuffed with straw, thin twig fingers, holding a long wooden pole with a curved iron PRUNING HOOK at the end (an orchard sickle), rotten purple apples hanging from its belt. SCALE: about 1.4x the hunter height, very thin.
Animation: a wide low sweep forward with the pruning hook. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-05 · `ceifador_pomar_morte.png` · Ceifador de Pomar (comum, Ato VII): morte

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [estilo de chefes](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: an orchard HARVESTER echo, a tall gaunt scarecrow-like reaper from the Rotten Orchard: a head made of an old burlap apple sack with two stitched eye holes and a faint magenta glow inside, a long patched coat stuffed with straw, thin twig fingers, holding a long wooden pole with a curved iron PRUNING HOOK at the end (an orchard sickle), rotten purple apples hanging from its belt. SCALE: about 1.4x the hunter height, very thin.
Animation: falls apart into straw, twigs and rotten apples, the sack head rolls away. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-06 · `sombra_cacador_lamina_parado.png` · Sombra de Caçador, variante Lâmina (comum, Ato VII): parado

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Lâmina (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/lamina_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a steel sword. SCALE: exactly the hunter height (1.0x).
Animation: idle, standing still like a hunter on guard, the shadow body flickering, wisps rising. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-07 · `sombra_cacador_lamina_mover.png` · Sombra de Caçador, variante Lâmina (comum, Ato VII): mover

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Lâmina (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/lamina_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a steel sword. SCALE: exactly the hunter height (1.0x).
Animation: walking forward like a hunter on patrol, wisps trailing behind. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-08 · `sombra_cacador_lamina_aviso.png` · Sombra de Caçador, variante Lâmina (comum, Ato VII): aviso

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Lâmina (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/lamina_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a steel sword. SCALE: exactly the hunter height (1.0x).
Animation: warning: raises the shadow sword high, the blade glows bright amber. Exactly 3 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 3 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-09 · `sombra_cacador_lamina_ataque.png` · Sombra de Caçador, variante Lâmina (comum, Ato VII): ataque

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Lâmina (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/lamina_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a steel sword. SCALE: exactly the hunter height (1.0x).
Animation: a fast horizontal sword slash forward leaving a magenta arc trail. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-10 · `sombra_cacador_lamina_morte.png` · Sombra de Caçador, variante Lâmina (comum, Ato VII): morte

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Lâmina (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/lamina_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a steel sword. SCALE: exactly the hunter height (1.0x).
Animation: falls to its knees, the shadow dissolves into smoke and the unlit lantern drops to the ground. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-11 · `sombra_cacador_arco_parado.png` · Sombra de Caçador, variante Arco (comum, Ato VII): parado

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Arco (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/arco_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a wooden bow. SCALE: exactly the hunter height (1.0x).
Animation: idle, standing still like a hunter on guard, the shadow body flickering, wisps rising. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-12 · `sombra_cacador_arco_mover.png` · Sombra de Caçador, variante Arco (comum, Ato VII): mover

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Arco (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/arco_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a wooden bow. SCALE: exactly the hunter height (1.0x).
Animation: walking forward like a hunter on patrol, wisps trailing behind. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-13 · `sombra_cacador_arco_aviso.png` · Sombra de Caçador, variante Arco (comum, Ato VII): aviso

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Arco (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/arco_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a wooden bow. SCALE: exactly the hunter height (1.0x).
Animation: warning: draws the shadow bow and aims, the arrow tip glows bright amber. Exactly 3 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 3 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-14 · `sombra_cacador_arco_ataque.png` · Sombra de Caçador, variante Arco (comum, Ato VII): ataque

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Arco (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/arco_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a wooden bow. SCALE: exactly the hunter height (1.0x).
Animation: releases a glowing magenta arrow forward (the arrow leaves the frame), recoil. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-15 · `sombra_cacador_arco_morte.png` · Sombra de Caçador, variante Arco (comum, Ato VII): morte

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Arco (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/arco_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a wooden bow. SCALE: exactly the hunter height (1.0x).
Animation: falls to its knees, the shadow dissolves into smoke and the unlit lantern drops to the ground. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-16 · `sombra_cacador_martelo_parado.png` · Sombra de Caçador, variante Martelo (comum, Ato VII): parado

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Martelo (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/martelo_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a big two-handed war hammer. SCALE: exactly the hunter height (1.0x).
Animation: idle, standing still like a hunter on guard, the shadow body flickering, wisps rising. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-17 · `sombra_cacador_martelo_mover.png` · Sombra de Caçador, variante Martelo (comum, Ato VII): mover

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Martelo (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/martelo_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a big two-handed war hammer. SCALE: exactly the hunter height (1.0x).
Animation: walking forward like a hunter on patrol, wisps trailing behind. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-18 · `sombra_cacador_martelo_aviso.png` · Sombra de Caçador, variante Martelo (comum, Ato VII): aviso

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Martelo (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/martelo_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a big two-handed war hammer. SCALE: exactly the hunter height (1.0x).
Animation: warning: lifts the shadow hammer over its shoulder, the hammer head glows bright amber. Exactly 3 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 3 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-19 · `sombra_cacador_martelo_ataque.png` · Sombra de Caçador, variante Martelo (comum, Ato VII): ataque

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Martelo (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/martelo_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a big two-handed war hammer. SCALE: exactly the hunter height (1.0x).
Animation: slams the hammer into the ground in front of it, a small magenta shockwave. Exactly 4 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 4 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

### C-20 · `sombra_cacador_martelo_morte.png` · Sombra de Caçador, variante Martelo (comum, Ato VII): morte

**Anexe estas referências antes de mandar:** [estilo de monstros](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [caçador Martelo (silhueta e proporção)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/martelo_andar_lado.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE sprite strip for this game. Subject: the ECHO of a fallen hunter of the Order of the Lantern: the same chunky body proportions and silhouette as the attached hunter, but made of dark indigo-black shadow like thick smoke, a torn cape, an old brass lantern at the belt that is broken and UNLIT, two faint magenta eyes, the edges of the body dissolving into wisps; it carries a shadow copy of a big two-handed war hammer. SCALE: exactly the hunter height (1.0x).
Animation: falls to its knees, the shadow dissolves into smoke and the unlit lantern drops to the ground. Exactly 6 frames. side view facing RIGHT.
If an earlier strip of this same monster is attached, keep its design identical.
STYLE: detailed hand-painted pixel art, crisp pixel clusters, dark selective outlines, no blur, no smooth vector look, no 3D render look. Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT. Match the attached reference images as closely as possible.

FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (256x256 or larger), evenly spaced, the subject centered in each frame with the feet on the same baseline. Background: flat solid pure green #00FF00 everywhere (it will be removed), no ground shadow. The SAME character in every frame: same face, clothes, colors, proportions and size; only the pose changes, so the frames play as smooth animation. SCALE: the character must have exactly the same size as in the attached reference strips (do not zoom in, even for close actions like getting hit).
Do NOT add: text, letters, numbers, logos, watermark, UI, frame or border, grid lines, extra characters.
```

