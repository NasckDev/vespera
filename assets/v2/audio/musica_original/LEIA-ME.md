# Trilha sonora original de Véspera (ACE-Step 1.5, licença MIT)

Gerada localmente em CPU com ACE-Step 1.5 (DiT turbo 2B, sem LM, 8 passos, seed 7), instrumental, 48 kHz estéreo, .ogg Vorbis.
Os trechos não são loop perfeito: o modelo gera um trecho linear. Faça crossfade no engine ou use loop com fade.

| Faixa | Duração | Uso no jogo | Prompt |
|---|---|---|---|
| titulo | 90 s, 66 bpm, D minor | Tela de título, opções, controles, ajuda, slots (chave 'title' e afins em sons.json) | main theme, melancholic yet hopeful, solo harp arpeggios with soft string pad and distant church bells, slow, cinematic, warm reverb, lantern-lit coastal city, no drums, instrumental |
| praca | 90 s, 84 bpm, G major | Praça central (mapa 'praca') | calm town square at dusk, gentle nylon guitar fingerpicking and soft accordion, warm and nostalgic, light strings, relaxed folk, instrumental |
| brecha | 90 s, 100 bpm, C# minor | Brechas / esgoto (esgoto_corredor, esgoto_cruzamento, esgoto_tesouro) | tense dark atmosphere, brooding analog synthesizer drones, pulsing low bass, eerie sustained strings, unsettling rift between worlds, suspense, instrumental |
| guardiao | 90 s, 138 bpm, E minor | Combate contra guardião / arena (esgoto_arena) | epic boss battle, driving taiko and orchestral percussion, powerful low strings and brass stabs, urgent, dramatic, cinematic action, instrumental |
| final | 90 s, 72 bpm, D major | Final do jogo / amanhecer (também serve para 'vitoria' e créditos) | hopeful dawn, the sun rises at last, uplifting orchestral strings, harp, gentle piano and soft choir pads, emotional crescendo, warm, triumphant but tender, instrumental |
| forjas | 60 s, 96 bpm, A minor | Distrito das Forjas (mapa 'forjas'; talvez 'forja_interior') | blacksmith forge, rhythmic anvil hits and metallic percussion, light steady groove, plucked strings, earthy folk, instrumental |
| pomar | 60 s, 76 bpm, E minor | Pomar (mapa novo) | sad folk orchard, melancholic acoustic guitar and fiddle, soft hand percussion, wistful, autumn, instrumental |
| cais | 60 s, 68 bpm, F# minor | Cais / porto (mapa novo) | mysterious harbor at night, ocean waves ambience, creaking ship, mellow marimba, soft cello, maritime sea shanty mood, foggy, instrumental |
| cemiterio | 60 s, 56 bpm, B minor | Cemitério (mapa novo) | solemn graveyard, wordless choir ooh pads, slow tolling bells, ethereal, sorrowful, reverent, instrumental |
| torre | 60 s, 52 bpm, C minor | Torre do sino (mapa novo) | solemn bell tower, deep slow tolling low bells, dark organ drone, sparse piano, majestic and grave, instrumental |
| ruas | 60 s, 80 bpm, A minor | Ruas da cidade (mapa novo; também serve para 'melancolia') | melancholic urban night streets, muted piano, soft upright bass, brushed drums, rainy lantern light, lonely, instrumental |
| avesso (NÃO GERADA) | 60 s, 60 bpm, F minor | Avesso (não gerada) | dissonant ethereal otherworld, shimmering glassy pads, detuned bells, whispering textures, uneasy floating atmosphere, instrumental |
| reflexo (NÃO GERADA) | 90 s, 144 bpm, D minor | Chefe final Reflexo (não gerada) | final boss battle, epic orchestral with powerful choir pads, thunderous percussion, soaring strings and brass, intense and tragic, cinematic climax, instrumental |

## Reproduzir
`gerar.py <faixas>` lê `faixas.json`; rodar com o venv de `C:leerramentas\ACE-Step-1.5` e as variáveis `ACESTEP_CPU_INT8LOW=1 ACESTEP_GENERATION_TIMEOUT=7200`. `converter.py` converte `wav/` para .ogg.
Tempo medido: ~16 a 20 min por faixa de 90 s e ~8 a 18 min por faixa de 60 s, mais ~6 min para carregar o modelo.
