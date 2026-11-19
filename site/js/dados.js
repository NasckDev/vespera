/* =========================================================================
   VÉSPERA — dados do site (editar aqui).
   Tudo embutido em JS porque fetch de JSON é bloqueado em file://.
   ========================================================================= */
window.VESPERA = {
  /* Contadores. Fonte: repo C:\ale\vespera, branch v2 (commit a1882fe).
     Se o jogo mudar, troque só o número. */
  numeros: [
    { valor: 7,  sufixo: "",  rotulo: "Atos + o Final",       nota: "README.md, campanha da Parte 1 (Atos I–VII e Final)" },
    { valor: 8,  sufixo: "",  rotulo: "Guardiões",            nota: "data/progressao/guardians.json → guardians (8)" },
    { valor: 9,  sufixo: "",  rotulo: "Ranks, do E ao Alvor", nota: "data/progressao/ranks.json → ranks (9)" },
    { valor: 7,  sufixo: "",  rotulo: "Ecos de Guardião",     nota: "data/progressao/echoes.json → echoes (7), slots (2)" },
    { valor: 30, sufixo: "",  rotulo: "Bênçãos da Lanterna",  nota: "data/cacada/bencaos.json → blessings (30), families (5)" },
    { valor: 8,  sufixo: "",  rotulo: "Conjuntos de 4 peças", nota: "data/progressao/gear.json → sets (8), slots (4)" },
    { valor: 3,  sufixo: "",  rotulo: "Armas para escolher",  nota: "data/progressao/weapons.json → weapons (3)" },
    { valor: 4,  sufixo: "",  rotulo: "Exames da Ordem",      nota: "data/progressao/exams.json → exams (4: C, B, S, SSS)" }
  ],

  links: {
    baixar: "https://github.com/NasckDev/vespera/releases",
    codigo: "https://github.com/NasckDev/vespera",
    avisar: "https://github.com/NasckDev/vespera/releases",
    licencaArte: "https://creativecommons.org/licenses/by-nc-sa/4.0/deed.pt_BR",
    licencaCodigo: "https://github.com/NasckDev/vespera/blob/main/LICENSE"
  },

  /* Tiras de sprite geradas dos atlas em assets/v2/sprites (w/h = 1 quadro). */
  sprites: {
    lodo_mae:      { w: 410, h: 300, anims: { parado: { n: 6, fps: 6 }, engolir: { n: 8, fps: 10 } } },
    macieira_oca:  { w: 279, h: 300, anims: { parado: { n: 6, fps: 6 }, raizes:  { n: 8, fps: 10 } } },
    farol_afogado: { w: 193, h: 300, anims: { parado: { n: 6, fps: 6 }, feixe:   { n: 8, fps: 10 } } },
    velora:        { w: 219, h: 300, anims: { parado: { n: 6, fps: 6 }, lancar:  { n: 8, fps: 10 } } },
    sineiro_mudo:  { w: 238, h: 300, anims: { parado: { n: 6, fps: 6 }, badalar: { n: 8, fps: 10 } } },
    inquilino:     { w: 190, h: 300, anims: { parado: { n: 6, fps: 6 }, esmagar: { n: 8, fps: 10 } } },
    lodo:          { w: 96,  h: 76,  anims: { parado: { n: 4, fps: 6 }, pulo:  { n: 6, fps: 10 } } },
    espectro:      { w: 148, h: 116, anims: { parado: { n: 4, fps: 6 }, mover: { n: 6, fps: 10 } } },
    carrapicho:    { w: 82,  h: 85,  anims: { parado: { n: 4, fps: 6 }, mover: { n: 6, fps: 10 } } },
    lamina:        { w: 128, h: 121, anims: { parado: { n: 4, fps: 6 }, andar: { n: 6, fps: 10 } } },
    arco:          { w: 158, h: 121, anims: { parado: { n: 4, fps: 6 }, andar: { n: 6, fps: 10 } } },
    martelo:       { w: 188, h: 116, anims: { parado: { n: 4, fps: 6 }, andar: { n: 6, fps: 10 } } }
  },

  /* Guardiões sem spoiler (os dois últimos de guardians.json ficam escondidos). */
  guardioes: [
    { id: "lodo_mae", nome: "Lodo-Mãe", rank: "E", ato: "Ato I", brecha: "Esgotos do Mercado", mapa: "esgoto-arena", ataque: "engolir",
      linha: "Engole o que cai no esgoto e se divide quando apanha.",
      eco: "Eco Viscoso", ecoTexto: "Seu avanço deixa um rastro que deixa os monstros lentos." },
    { id: "macieira_oca", nome: "A Macieira Oca", rank: "D", ato: "Ato II", brecha: "Pomar Podre", mapa: "pomar-arena", ataque: "raizes",
      linha: "Apodrece de dentro e ataca com raízes que rasgam o chão.",
      eco: "Eco da Semente", ecoTexto: "Planta uma muda que atira espinhos por 10 s." },
    { id: "farol_afogado", nome: "O Farol Afogado", rank: "C", ato: "Ato III", brecha: "Cais Afogado", mapa: "cais-arena", ataque: "feixe",
      linha: "O farol que não acendeu agora varre o cais com o feixe.",
      eco: "Eco do Farol", ecoTexto: "Um feixe que puxa os monstros para um ponto." },
    { id: "velora", nome: "A Velória", rank: "B", ato: "Ato IV", brecha: "Cemitério das Lanternas", mapa: "cemiterio-arena", ataque: "lancar",
      linha: "Derrete e ressurge entre as velas do cemitério de antes.",
      eco: "Eco da Vela", ecoTexto: "Cura por 6 s, mas seu rank cai um nível enquanto dura." },
    { id: "sineiro_mudo", nome: "O Sineiro Mudo", rank: "A", ato: "Ato V", brecha: "Torre do Sino Mudo", mapa: "torre-arena", ataque: "badalar",
      linha: "Toca um sino sem som. Voa, mergulha e puxa as cordas.",
      eco: "Eco do Sino", ecoTexto: "Um anel que atordoa tudo em volta por 2 s." },
    { id: "inquilino", nome: "O Inquilino", rank: "S", ato: "Ato VI", brecha: "Ruas Invertidas", mapa: "ruas-arena", ataque: "esmagar",
      linha: "Mora numa casa que não existe mais e esmaga quem entra.",
      eco: "Eco do Lar", ecoTexto: "Um abrigo de 3 s onde ninguém leva dano." }
  ],

  /* data/progressao/ranks.json + exams.json. Guardiões de SS e SSS ficam sem nome (spoiler). */
  ranks: [
    { id: "E",     titulo: "Aprendiz",    raridade: "Comum",         exame: false, guardiao: "Lodo-Mãe",        estagio: 1, texto: "O recém-chegado. O selo queimou sozinho no pulso." },
    { id: "D",     titulo: "Vigia",       raridade: "Incomum",       exame: false, guardiao: "A Macieira Oca",  estagio: 1, texto: "Já fechou a primeira Brecha e voltou inteiro." },
    { id: "C",     titulo: "Lanterneiro", raridade: "Raro",          exame: true,  guardiao: "O Farol Afogado", estagio: 2, texto: "Primeiro exame da Ordem. O visual do caçador muda." },
    { id: "B",     titulo: "Sentinela",   raridade: "Épico",         exame: true,  guardiao: "A Velória",       estagio: 2, texto: "A Ordem já confia a você o Cemitério." },
    { id: "A",     titulo: "Caçador",     raridade: "Lendário",      exame: false, guardiao: "O Sineiro Mudo",  estagio: 3, texto: "Poucos na cidade chegam até a Torre." },
    { id: "S",     titulo: "Arauto",      raridade: "Mítico",        exame: true,  guardiao: "O Inquilino",     estagio: 3, texto: "Seu nome já corre pelas Ruas Baixas." },
    { id: "SS",    titulo: "Crepuscular", raridade: "Único",         exame: false, guardiao: "???",             estagio: 4, texto: "O Avesso profundo. O resto é com você." },
    { id: "SSS",   titulo: "Véspero",     raridade: "Transcendente", exame: true,  guardiao: "???",             estagio: 4, texto: "Poucos chegam aqui." },
    { id: "Alvor", titulo: "Alvor",       raridade: "Alvor",         exame: false, guardiao: "",                estagio: 5, texto: "Para a Ordem, quem faz amanhecer. Ninguém nunca chegou." }
  ],
  estagios: ["Aprendiz", "Caçador", "Veterano", "Elite", "Radiante"],

  /* Trilha original (assets/v2/audio/musica_original, ACE-Step 1.5, MIT). */
  trilhas: [
    { id: "titulo",   nome: "Tema do título" },
    { id: "praca",    nome: "Praça da Lanterna" },
    { id: "brecha",   nome: "A Brecha" },
    { id: "guardiao", nome: "Guardião" }
  ],

  /* Capturas reais do jogo (docs/v2/qa) para o carrossel e o lightbox. */
  capturas: [
    { src: "luta-lodo-mae",   legenda: "Arena da Lodo-Mãe, nos Esgotos do Mercado" },
    { src: "investida",       legenda: "A Lodo-Mãe avança: leia o aviso no chão" },
    { src: "golpe",           legenda: "Combo da Lâmina no corredor do esgoto" },
    { src: "divide",          legenda: "Quando apanha, a Lodo-Mãe se divide" },
    { src: "espectro-aviso",  legenda: "O aviso no chão antes do ataque do Espectro" },
    { src: "divide",          legenda: "Ferida, a Lodo-Mãe se divide em pedaços" },
    { src: "espectro-aviso",  legenda: "O aviso no chão vem antes do golpe do Espectro" },
    { src: "coop-chefe",      legenda: "Co-op local: dois caçadores contra o Guardião" },
    { src: "bau",             legenda: "Baú aberto: Fragmentos e materiais para os dois" },
    { src: "forja",           legenda: "A Forja da Odete: cada nível muda a arma" },
    { src: "rank-c",          legenda: "Rank C: Lanterneiro. O visual do caçador muda" },
    { src: "praca-portal",    legenda: "A Praça da Lanterna e a Brecha aberta" },
    { src: "dialogo-odete",   legenda: "Odete, a ferreira que nunca elogia" },
    { src: "dialogo-lis",     legenda: "Lis sabe onde a Brecha vai abrir" },
    { src: "inventario",      legenda: "Inventário com peças de conjunto" },
    { src: "coop-reanimando", legenda: "Caiu? O parceiro segura o botão para reanimar" }
  ],

  /* Roteiro do trailer montado no navegador. Tipos: card, img, mapa, video.
     dur em segundos. kb = Ken Burns: [escala inicial, x, y] → [escala final, x, y] (x/y em %). */
  trailer: {
    musica: [
      { id: "titulo",   ate: 6 },   // toca até o início do shot 6, depois cruza para a próxima
      { id: "guardiao", ate: 99 }
    ],
    shots: [
      { tipo: "card",  dur: 3.4, texto: "Há vinte anos, o sol não nasceu por um ano inteiro." },
      { tipo: "img",   dur: 4.2, src: "img/cena/noite", kb: [[1.0, 50, 60], [1.18, 50, 40]] },
      { tipo: "card",  dur: 3.2, texto: "Lumeira acende as luzes antes de escurecer.", sub: "Todo dia." },
      { tipo: "img",   dur: 3.8, src: "img/cena/dia", kb: [[1.15, 30, 50], [1.0, 60, 50]] },
      { tipo: "img",   dur: 3.6, src: "img/cena/vespera", kb: [[1.0, 50, 50], [1.2, 70, 30]] },
      { tipo: "card",  dur: 2.6, texto: "Toda véspera,", sub: "uma Brecha se abre." },
      { tipo: "video", dur: 8.1, src: "video/prologo-720" },
      { tipo: "mapa",  dur: 4.6, src: "img/mapa/esgoto-corredor", kb: [[1.25, 40, 55], [1.35, 60, 55]],
        atores: [ { s: "lamina", a: "andar", de: [8, 64], para: [52, 64], esc: 0.85 },
                  { s: "lodo", a: "pulo", de: [78, 60], para: [64, 62], esc: 0.9, vira: true },
                  { s: "espectro", a: "mover", de: [92, 48], para: [74, 50], esc: 0.8, vira: true } ] },
      { tipo: "img",   dur: 2.4, src: "img/jogo/golpe", kb: [[1.0, 30, 55], [1.22, 25, 60]], flash: true },
      { tipo: "img",   dur: 2.6, src: "img/jogo/investida", kb: [[1.0, 50, 40], [1.2, 50, 45]], flash: true },
      { tipo: "img",   dur: 2.6, src: "img/jogo/luta-lodo-mae", kb: [[1.2, 50, 45], [1.0, 50, 50]] },
      { tipo: "card",  dur: 2.2, texto: "Caçar." },
      { tipo: "img",   dur: 3.0, src: "img/cena/odete", kb: [[1.0, 40, 50], [1.15, 30, 50]] },
      { tipo: "card",  dur: 2.0, texto: "Forjar." },
      { tipo: "img",   dur: 3.0, src: "img/cena/selo", kb: [[1.0, 50, 50], [1.2, 40, 40]] },
      { tipo: "card",  dur: 2.0, texto: "Subir." },
      { tipo: "mapa",  dur: 4.4, src: "img/mapa/pomar-arena", kb: [[1.2, 45, 50], [1.3, 55, 50]],
        atores: [ { s: "arco", a: "andar", de: [10, 70], para: [40, 66], esc: 0.85 },
                  { s: "martelo", a: "andar", de: [4, 78], para: [30, 76], esc: 0.85 },
                  { s: "macieira_oca", a: "parado", de: [70, 62], para: [70, 62], esc: 0.75, vira: true } ] },
      { tipo: "img",   dur: 3.2, src: "img/jogo/coop-chefe", kb: [[1.0, 50, 50], [1.18, 50, 40]], legenda: "Co-op local para 2" },
      { tipo: "mapa",  dur: 4.2, src: "img/mapa/cemiterio-arena", kb: [[1.15, 50, 50], [1.3, 50, 40]],
        atores: [ { s: "velora", a: "parado", de: [50, 52], para: [50, 52], esc: 0.75 } ] },
      { tipo: "mapa",  dur: 4.0, src: "img/mapa/torre-arena", kb: [[1.3, 50, 45], [1.15, 50, 50]],
        atores: [ { s: "sineiro_mudo", a: "badalar", de: [50, 50], para: [50, 50], esc: 0.7 } ] },
      { tipo: "extras" },
      { tipo: "card",  dur: 2.8, texto: "Do selo E ao Alvor.", sub: "Ninguém nunca chegou." },
      { tipo: "video", dur: 1.3, src: "video/abertura-720", final: true },
      { tipo: "card",  dur: 4.5, texto: "VÉSPERA", sub: "Beta grátis para Windows", logo: true }
    ]
  }
};
