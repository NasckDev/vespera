# Áudio do Véspera

Todo o áudio é **gerado por código** (nada gravado ou baixado). Licença dos arquivos gerados:
**CC BY-NC-SA 4.0**, como o resto dos assets ([`LICENSE-ASSETS.md`](../../LICENSE-ASSETS.md)).
Os scripts em `tools/audio/` são código do projeto (licença MIT) e foram escritos para o Véspera.

| Pasta | Conteúdo | Gerador |
|---|---|---|
| `sfx/` | 18 efeitos, WAV 16-bit 44.1 kHz mono | `tools/audio/gen_sfx.py` |
| `music/` | 3 loops (`city_day`, `vespera`, `brecha`), WAV 16-bit 44.1 kHz mono | `tools/audio/gen_music.py` |

## Regenerar

Python 3, só biblioteca padrão (sem numpy). Saída determinística (seed fixa): rodar de novo gera os mesmos bytes.

```bash
python tools/audio/gen_sfx.py      # ~5 s
python tools/audio/gen_music.py    # ~3 min em Python puro (--only city_day,brecha para gerar só algumas)
"$GODOT" --headless --path . --import
```

Não apague os `.wav.import`: os de `music/` ligam o loop (`edit/loop_mode=2`, Forward, do início ao fim).

## Efeitos (`sfx/`)

Transcrição fiel da síntese WebAudio do protótipo v2 (`core.js`, seção "áudio"; spec 02 §2.7): mesmos
osciladores, rampas exponenciais de frequência e de ganho, ruído branco com passa-banda (Q 0.8) e ganho mestre
0.22 — por isso os picos dos arquivos ficam baixos (−37 a −23 dBFS), como no protótipo; no jogo, o bus Master
soma +12 dB com limitador (DECISIONS D32). Única diferença (DECISIONS D6): o
buffer de ruído de 0.5 s do protótipo cortava o ruído do `portal` (0.6 s); aqui ele faz loop.

## Músicas (`music/`)

Tom do GDD: melancólico mas esperançoso. Cada faixa é um loop sem clique (buffer circular, tons com ciclos
inteiros, filtros e reverb aquecidos com uma volta inteira).

| Id | Duração | Ideia |
|---|---|---|
| `city_day` | 50.5 s | Lumeira de dia. Lá menor/dó maior, 76 BPM: triângulo na melodia, arpejo de pulso 25%, pad e baixo; mar ao fundo (som diegético). |
| `vespera` | 42.7 s | A véspera. Ré menor, 90 BPM: o motivo de abertura da cidade em menor, ostinato de "relógio", pulso grave e drone. |
| `brecha` | 50.5 s | A música da cidade **tocada ao contrário**, abafada (passa-baixa 720 Hz, 24 dB/oct) e com reverb longo (GDD §6). |

Não há encoder OGG na máquina de geração; por isso WAV. Se quiser OGG depois, converta e troque a extensão em
`AudioManager` (o loop de OGG é `loop=true` no import).
