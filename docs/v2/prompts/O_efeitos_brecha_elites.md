# [Arte v2] Efeitos: Brecha abrindo e auras de elite (5 imagens)

Dois efeitos animados pedidos pelo painel de testes. **1) Brecha abrindo** (F-01): hoje a Brecha só aparece, sem rasgar; o roteiro (§3, minuto 1:00) quer que o jogador **veja a Brecha abrir antes de ouvir o nome dela**. É uma tira de 10 quadros: fenda, rasgo, flash, abertura estável e brilho em loop. O desenho (rasgo vertical no ar, com rachaduras no chão e a Lumeira de cabeça para baixo dentro) é uma **proposta** do roteiro 'portal magenta'; ajuste se o dono preferir outro formato. **2) Auras de elite** (F-02 a F-05, ROTEIRO-V2 §5.3): um modificador **visível** por elite: **Blindado** (casca de pedra), **Veloz** (rastro), **Faminto** (Fragmentos sendo sugados e boca) e **Gêmeo** (eco duplicado). São **overlays** (só o efeito, centro vazio), tiras de 6 quadros em loop, que o jogo põe por cima de qualquer monstro comum, 1,3 vez maior.

Um prompt por imagem; anexe as referências indicadas e mande cada arquivo com o nome indicado.

### F-01 · `fx_brecha_abrindo.png` · Brecha abrindo: a fenda magenta se rasgando (10 quadros)

**Anexe estas referências antes de mandar:** [praça (onde a Brecha abre)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_mapa_praca.png) · [esgoto (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_sala_esgoto.png) · [prancha de mapas (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_prancha_mapas.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE animated visual-effect strip for this game. Subject: a Breach opening. A glowing rift tears open in the air, standing upright like a vertical jagged tear in reality about twice the height of a hunter, seen slightly from the front (3/4 top-down), with thin cracks spreading across the stone ground around its base. Colors: rift magenta #E64A8E edges, a violet #4A2C6B and deep indigo #1B1F3B inside with a few faint inverted-town shapes (tiny upside-down roofs and lamp posts) visible within, cream-white hot sparks at the tips, a few small stone chips flying.
Animation: the Breach opening in ONE continuous sequence: frame 1 a hairline crack with a tiny magenta spark; frames 2-3 the crack grows and splits; frames 4-6 the tear rips wider with a bright flash and flying chips; frames 7-8 it stabilizes wide open with the inner glow swelling; frames 9-10 a calm pulsing idle state with embers floating up, drawn so that frame 10 can loop smoothly back to frame 9. Exactly 10 frames.
ART STYLE IS MANDATORY: the same detailed hand-painted pixel art as the attached maps and monsters (crisp pixel clusters, dark selective outlines where there is a solid shape, glowing parts built from hard-edged pixel steps, no smooth gradients or blur). Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT.
FORMAT: the 10 frames side by side in ONE single horizontal row, every frame the same square size (512x512 or larger), evenly spaced, the effect centered in each frame on the same center point. Background: flat solid pure green #00FF00 everywhere (it will be removed). Do not use green in the effect itself.
NEVER: no text or letters, no numbers, no frame or border, no grid lines, no characters, no creatures, no ground or scenery.
```

### F-02 · `fx_elite_blindado.png` · Elite Blindado (armored): aura em volta do monstro (6 quadros)

**Anexe estas referências antes de mandar:** [monstros (estilo e tamanho)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [chefes (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE animated visual-effect strip for this game. Subject: an OVERLAY aura for the elite modifier 'Blindado (armored)', drawn around an EMPTY center (the game puts the monster there, so do NOT draw any monster or body): the center is left completely empty flat #00FF00, about 55% of the frame width, and the effect lives around it, extending to about 85% of the frame. The overlay is for a creature about 1.3 times bigger than a common monster. The effect: a shell of thick grey-blue stone armor plates (#6B6F8A with lighter cracked edges) floating around the empty center: five or six chunky chiselled plates, hovering and slowly orbiting, with small cracks and a faint stone-dust trail.
Animation: looping idle aura: plates hover and rotate slowly, bobbing, a small dust puff; frame 6 flows smoothly back into frame 1. Exactly 6 frames.
ART STYLE IS MANDATORY: the same detailed hand-painted pixel art as the attached maps and monsters (crisp pixel clusters, dark selective outlines where there is a solid shape, glowing parts built from hard-edged pixel steps, no smooth gradients or blur). Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT.
FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (512x512 or larger), evenly spaced, the effect centered in each frame on the same center point. Background: flat solid pure green #00FF00 everywhere (it will be removed). Do not use green in the effect itself.
NEVER: no text or letters, no numbers, no frame or border, no grid lines, no characters, no creatures, no ground or scenery.
```

### F-03 · `fx_elite_veloz.png` · Elite Veloz (fast): aura em volta do monstro (6 quadros)

**Anexe estas referências antes de mandar:** [monstros (estilo e tamanho)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [chefes (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE animated visual-effect strip for this game. Subject: an OVERLAY aura for the elite modifier 'Veloz (fast)', drawn around an EMPTY center (the game puts the monster there, so do NOT draw any monster or body): the center is left completely empty flat #00FF00, about 55% of the frame width, and the effect lives around it, extending to about 85% of the frame. The overlay is for a creature about 1.3 times bigger than a common monster. The effect: speed: long thin teal and cream (#3C7A8A and #F4E7C6) streak lines trailing to the LEFT of the empty center (the monster runs to the right), plus two fading after-image outlines of a round shape behind it, small wind swirls.
Animation: looping idle aura: the streaks stretch and shimmer, the after-images flicker, wind swirls travel left; frame 6 flows smoothly back into frame 1. Exactly 6 frames.
ART STYLE IS MANDATORY: the same detailed hand-painted pixel art as the attached maps and monsters (crisp pixel clusters, dark selective outlines where there is a solid shape, glowing parts built from hard-edged pixel steps, no smooth gradients or blur). Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT.
FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (512x512 or larger), evenly spaced, the effect centered in each frame on the same center point. Background: flat solid pure green #00FF00 everywhere (it will be removed). Do not use green in the effect itself.
NEVER: no text or letters, no numbers, no frame or border, no grid lines, no characters, no creatures, no ground or scenery.
```

### F-04 · `fx_elite_faminto.png` · Elite Faminto (hungry): aura em volta do monstro (6 quadros)

**Anexe estas referências antes de mandar:** [monstros (estilo e tamanho)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [chefes (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE animated visual-effect strip for this game. Subject: an OVERLAY aura for the elite modifier 'Faminto (hungry)', drawn around an EMPTY center (the game puts the monster there, so do NOT draw any monster or body): the center is left completely empty flat #00FF00, about 55% of the frame width, and the effect lives around it, extending to about 85% of the frame. The overlay is for a creature about 1.3 times bigger than a common monster. The effect: hunger: a ring of small glowing amber crystal Fragments (#F2A541) being pulled in a spiral toward the empty center, a faint dark magenta hunger mist, and a toothy glowing mouth arc of small white fangs at the bottom of the empty center.
Animation: looping idle aura: the Fragments spiral inward and shrink, the mist pulses, the fangs open and close slightly; frame 6 flows smoothly back into frame 1. Exactly 6 frames.
ART STYLE IS MANDATORY: the same detailed hand-painted pixel art as the attached maps and monsters (crisp pixel clusters, dark selective outlines where there is a solid shape, glowing parts built from hard-edged pixel steps, no smooth gradients or blur). Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT.
FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (512x512 or larger), evenly spaced, the effect centered in each frame on the same center point. Background: flat solid pure green #00FF00 everywhere (it will be removed). Do not use green in the effect itself.
NEVER: no text or letters, no numbers, no frame or border, no grid lines, no characters, no creatures, no ground or scenery.
```

### F-05 · `fx_elite_gemeo.png` · Elite Gêmeo (twin): aura em volta do monstro (6 quadros)

**Anexe estas referências antes de mandar:** [monstros (estilo e tamanho)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_monstros.png) · [chefes (estilo)](https://raw.githubusercontent.com/NasckDev/vespera/v2/docs/v2/referencias/estilo_chefes.png)

```
I am making VÉSPERA, a 2D pixel-art action RPG for PC. It takes place in Lumeira, a lantern-lit coastal town. Twenty years ago the sun did not rise for a whole year. Since then, every dusk, glowing magenta rifts called Breaches open into the Underside, an inverted copy of the town made of everything people lost or threw away. Monsters there are 'echoes' of human things. Hunters of the Order of the Lantern go in carrying lanterns. Mood: melancholic but hopeful; cozy warm town versus eerie rifts.

Please create ONE animated visual-effect strip for this game. Subject: an OVERLAY aura for the elite modifier 'Gêmeo (twin)', drawn around an EMPTY center (the game puts the monster there, so do NOT draw any monster or body): the center is left completely empty flat #00FF00, about 55% of the frame width, and the effect lives around it, extending to about 85% of the frame. The overlay is for a creature about 1.3 times bigger than a common monster. The effect: twin: a ghostly duplicate of the monster is implied by two mirrored violet and magenta (#4A2C6B and #E64A8E) outline rings of a round creature shape, one offset to the left and one offset to the right of the empty center, with thin glowing links between them and sparkles.
Animation: looping idle aura: the two echo outlines drift apart and together, flicker, and the links crackle; frame 6 flows smoothly back into frame 1. Exactly 6 frames.
ART STYLE IS MANDATORY: the same detailed hand-painted pixel art as the attached maps and monsters (crisp pixel clusters, dark selective outlines where there is a solid shape, glowing parts built from hard-edged pixel steps, no smooth gradients or blur). Palette: lantern amber #F2A541, rift magenta #E64A8E, deep indigo #1B1F3B shadows, violet #4A2C6B, cream #F4E7C6 highlights, moss green #5C8C4A, earth brown #8A5A3C, stone grey-blue #6B6F8A, water teal #3C7A8A. Light always comes from the TOP-LEFT.
FORMAT: the 6 frames side by side in ONE single horizontal row, every frame the same square size (512x512 or larger), evenly spaced, the effect centered in each frame on the same center point. Background: flat solid pure green #00FF00 everywhere (it will be removed). Do not use green in the effect itself.
NEVER: no text or letters, no numbers, no frame or border, no grid lines, no characters, no creatures, no ground or scenery.
```
