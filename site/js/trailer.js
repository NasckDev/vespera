/* ==========================================================================
   VÉSPERA · trailer montado no navegador + modal de vídeo.
   Sequência de "tomadas" (ilustrações, mapas com sprites andando, capturas,
   cartelas, a abertura e o prólogo) com a trilha original. Som só ao clicar.
   ========================================================================== */
(function () {
  "use strict";
  const DADOS = window.VESPERA;
  const V = window.Vespera;
  const S = window.VesperaSprites;
  const ICONES = {
    play: '<svg viewBox="0 0 24 24"><path fill="currentColor" d="M7 4l13 8-13 8z"/></svg>',
    pausa: '<svg viewBox="0 0 24 24"><path fill="currentColor" d="M6 4h4v16H6zM14 4h4v16h-4z"/></svg>',
    somOn: '<svg viewBox="0 0 24 24"><path fill="currentColor" d="M4 9h4l5-4v14l-5-4H4zM16 8a5 5 0 0 1 0 8l-1.4-1.4a3 3 0 0 0 0-5.2zM18.5 5.5a8.5 8.5 0 0 1 0 13l-1.4-1.4a6.5 6.5 0 0 0 0-10.2z"/></svg>',
    somOff: '<svg viewBox="0 0 24 24"><path fill="currentColor" d="M4 9h4l5-4v14l-5-4H4zM16.5 9.5l1.4-1.4 2.1 2.1 2.1-2.1 1.4 1.4-2.1 2.1 2.1 2.1-1.4 1.4-2.1-2.1-2.1 2.1-1.4-1.4 2.1-2.1z"/></svg>',
    cheia: '<svg viewBox="0 0 24 24"><path fill="currentColor" d="M4 4h6v2H6v4H4zM14 4h6v6h-2V6h-4zM4 14h2v4h4v2H4zM18 14h2v6h-6v-2h4z"/></svg>'
  };
  const instancias = [];

  function montarTomadas() {
    const extras = (window.VESPERA_VIDEOS_EXTRAS || []).filter((v) => v && v.arquivo);
    const lista = [];
    DADOS.trailer.shots.forEach((s) => {
      if (s.tipo === "extras") extras.forEach((v) => lista.push({ tipo: "video", dur: 7, arquivo: "video/extra/" + v.arquivo, legenda: v.legenda || "" }));
      else lista.push(Object.assign({}, s));
    });
    return lista;
  }
  const fmt = (s) => { s = Math.max(0, Math.floor(s)); return Math.floor(s / 60) + ":" + String(s % 60).padStart(2, "0"); };

  function Cinema(raiz, opcoes) {
    const op = Object.assign({ autoplay: false }, opcoes);
    const tomadas = montarTomadas();
    let total = tomadas.reduce((a, s) => a + s.dur, 0);
    let idx = -1, t = 0, tocando = false, mudo = true, ultimo = 0, camadaAtual = null;
    const largura = () => raiz.clientWidth || 960;

    raiz.classList.add("pausado");
    raiz.setAttribute("role", "region");
    raiz.setAttribute("aria-label", "Trailer montado com cenas reais do jogo");
    raiz.tabIndex = 0;
    raiz.innerHTML = `
      <div class="cinema__tomadas"></div>
      <div class="cinema__flash"></div>
      <div class="cinema__vinheta"></div>
      <button class="cinema__grande" type="button" aria-label="Assistir ao trailer">${ICONES.play}</button>
      <button class="cinema__som-aviso" type="button">${ICONES.somOff}<span>Ligar o som</span></button>
      <div class="cinema__controles">
        <button class="cinema__btn" data-acao="play" type="button" aria-label="Tocar">${ICONES.play}</button>
        <button class="cinema__btn" data-acao="som" type="button" aria-label="Ligar o som">${ICONES.somOff}</button>
        <div class="cinema__linha" role="slider" tabindex="0" aria-label="Posição do trailer" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0"><span></span></div>
        <span class="cinema__tempo">0:00 / ${fmt(total)}</span>
        <button class="cinema__btn" data-acao="cheia" type="button" aria-label="Tela cheia">${ICONES.cheia}</button>
      </div>`;
    const caixa = raiz.querySelector(".cinema__tomadas");
    const flash = raiz.querySelector(".cinema__flash");
    const bPlay = raiz.querySelector('[data-acao="play"]');
    const bSom = raiz.querySelector('[data-acao="som"]');
    const linha = raiz.querySelector(".cinema__linha");
    const tempo = raiz.querySelector(".cinema__tempo");

    /* ---- trilha: uma faixa por trecho, com crossfade ---- */
    const faixas = DADOS.trailer.musica.map((m) => {
      const a = new Audio();
      a.preload = "none";
      a.loop = true;
      a.volume = 0;
      a.dataset.id = m.id;
      return { ...m, audio: a, carregada: false };
    });
    function fonte(a, id) {
      if (a.src) return;
      a.src = `audio/tema-${id}.m4a`;
    }
    function faixaDe(i) { return faixas.find((f) => i < f.ate) || faixas[faixas.length - 1]; }
    function inicioTrecho(f) {
      const k = faixas.indexOf(f);
      const deShot = k === 0 ? 0 : faixas[k - 1].ate;
      return tomadas.slice(0, deShot).reduce((a, s) => a + s.dur, 0);
    }
    function tempoGlobal() { return tomadas.slice(0, Math.max(0, idx)).reduce((a, s) => a + s.dur, 0) + t; }
    let rampa = 0;
    function ajustarSom(forcarPosicao) {
      const atual = faixaDe(Math.max(0, idx));
      const fimTotal = total - tempoGlobal();
      faixas.forEach((f) => {
        const ativa = f === atual && tocando && !mudo;
        if (ativa) {
          fonte(f.audio, f.id);
          if (forcarPosicao || f.audio.paused) {
            try { f.audio.currentTime = Math.max(0, tempoGlobal() - inicioTrecho(f)); } catch (erro) { /* ainda sem metadados */ }
          }
          if (f.audio.paused) f.audio.play().catch(() => {});
        }
        f.alvo = ativa ? Math.min(0.85, fimTotal < 3 ? fimTotal / 3 * 0.85 : 0.85) : 0;
      });
      if (!rampa) rampa = requestAnimationFrame(passoRampa);
    }
    function passoRampa() {
      rampa = 0;
      let mexeu = false;
      faixas.forEach((f) => {
        const alvo = f.alvo || 0;
        const v = f.audio.volume;
        if (Math.abs(v - alvo) > 0.01) { f.audio.volume = Math.max(0, Math.min(1, v + Math.sign(alvo - v) * 0.02)); mexeu = true; }
        else { f.audio.volume = alvo; if (alvo === 0 && !f.audio.paused) f.audio.pause(); }
      });
      if (mexeu) rampa = requestAnimationFrame(passoRampa);
    }

    /* ---- renderização de cada tomada ---- */
    function src(base) { return `${base}-${largura() > 820 ? 1600 : 800}.webp`; }
    function animarKB(el, kb, dur) {
      if (!kb || V.movimentoReduzido || !el.animate) return null;
      const [[s0, x0, y0], [s1, x1, y1]] = kb;
      return el.animate([
        { transform: `scale(${s0})`, transformOrigin: `${x0}% ${y0}%` },
        { transform: `scale(${s1})`, transformOrigin: `${x1}% ${y1}%` }
      ], { duration: (dur + 0.9) * 1000, easing: "linear", fill: "forwards" });
    }
    function criarCamada(s) {
      const cam = document.createElement("div");
      cam.className = "cinema__tomada";
      cam._anims = [];
      if (s.tipo === "card") {
        cam.innerHTML = `<div class="cinema__card${s.logo ? " cinema__card--logo" : ""}"><p>${s.texto}${s.sub ? `<small>${s.sub}</small>` : ""}</p></div>`;
        const p = cam.querySelector("p");
        if (!V.movimentoReduzido && p.animate) cam._anims.push(p.animate([{ opacity: 0, transform: "translateY(14px) scale(.98)", letterSpacing: "0.06em", filter: "blur(6px)" }, { opacity: 1, transform: "none", letterSpacing: "0.01em", filter: "blur(0)", offset: 0.3 }, { opacity: 1, transform: "scale(1.03)", offset: 1 }], { duration: s.dur * 1000, easing: "ease-out", fill: "forwards" }));
      } else if (s.tipo === "img" || s.tipo === "mapa") {
        const quadro = document.createElement("div");
        quadro.style.cssText = "position:absolute;inset:0";
        const img = new Image();
        img.src = src(s.src);
        img.alt = "";
        img.style.cssText = "position:absolute;inset:0;width:100%;height:100%;object-fit:cover";
        quadro.appendChild(img);
        (s.atores || []).forEach((a) => {
          const ator = document.createElement("div");
          ator.className = "cinema__ator" + (a.vira ? " vira" : "");
          ator.style.left = a.de[0] + "%";
          ator.style.top = a.de[1] + "%";
          ator.appendChild(S.criar(a.s, a.a, (a.esc * largura() / 1280).toFixed(3)));
          quadro.appendChild(ator);
          if (!V.movimentoReduzido && (a.de[0] !== a.para[0] || a.de[1] !== a.para[1]) && ator.animate) {
            cam._anims.push(ator.animate([{ left: a.de[0] + "%", top: a.de[1] + "%" }, { left: a.para[0] + "%", top: a.para[1] + "%" }], { duration: s.dur * 1000, easing: "linear", fill: "forwards" }));
          }
        });
        cam.appendChild(quadro);
        const kb = animarKB(quadro, s.kb, s.dur);
        if (kb) cam._anims.push(kb);
        if (s.legenda) cam.insertAdjacentHTML("beforeend", `<span class="cinema__legenda">${s.legenda}</span>`);
      } else if (s.tipo === "video") {
        const v = document.createElement("video");
        v.muted = true; v.playsInline = true; v.preload = "auto";
        v.setAttribute("muted", ""); v.setAttribute("playsinline", "");
        if (s.arquivo) v.src = s.arquivo;
        else v.innerHTML = `<source src="${s.src}.webm" type="video/webm"><source src="${s.src}.mp4" type="video/mp4">`;
        v.addEventListener("loadedmetadata", () => {
          if (s.arquivo && isFinite(v.duration) && v.duration > 1) { s.dur = Math.min(8, v.duration); total = tomadas.reduce((a, x) => a + x.dur, 0); }
        });
        cam.appendChild(v);
        cam._video = v;
        if (s.legenda) cam.insertAdjacentHTML("beforeend", `<span class="cinema__legenda">${s.legenda}</span>`);
      }
      return cam;
    }
    function preCarregar(i) {
      const s = tomadas[i];
      if (!s) return;
      if (s.tipo === "img" || s.tipo === "mapa") { const im = new Image(); im.src = src(s.src); }
      (s.atores || []).forEach((a) => S.imagem(a.s, a.a));
    }
    function mostrar(i, tLocal) {
      idx = i;
      t = tLocal || 0;
      const s = tomadas[i];
      const cam = criarCamada(s);
      caixa.appendChild(cam);
      const anterior = camadaAtual;
      camadaAtual = cam;
      void cam.offsetWidth;
      cam.classList.add("visivel");
      if (s.flash && !V.movimentoReduzido) { flash.classList.remove("ativo"); void flash.offsetWidth; flash.classList.add("ativo"); }
      cam._anims.forEach((a) => { a.currentTime = t * 1000; if (!tocando) a.pause(); });
      if (cam._video) {
        cam._video.currentTime = t;
        if (tocando) cam._video.play().catch(() => {});
      }
      if (anterior) setTimeout(() => { if (anterior._video) anterior._video.pause(); anterior.remove(); }, 800);
      preCarregar(i + 1);
      ajustarSom(false);
    }

    /* ---- relógio ---- */
    function quadroLoop(agora) {
      if (!tocando) return;
      const dt = Math.min(0.1, (agora - (ultimo || agora)) / 1000);
      ultimo = agora;
      t += dt;
      const s = tomadas[idx];
      if (t >= s.dur) {
        if (idx + 1 < tomadas.length) mostrar(idx + 1, 0);
        else { mostrar(0, 0); }
      }
      atualizarBarra();
      requestAnimationFrame(quadroLoop);
    }
    function atualizarBarra() {
      const g = tempoGlobal();
      const p = Math.min(1, g / total);
      linha.style.setProperty("--p", p.toFixed(4));
      linha.setAttribute("aria-valuenow", Math.round(p * 100));
      tempo.textContent = `${fmt(g)} / ${fmt(total)}`;
    }
    function tocar() {
      if (tocando) return;
      instancias.forEach((o) => { if (o !== api) o.pausar(); });
      tocando = true;
      raiz.classList.remove("pausado");
      bPlay.innerHTML = ICONES.pausa; bPlay.setAttribute("aria-label", "Pausar");
      if (idx < 0) mostrar(0, 0);
      else {
        if (camadaAtual) { camadaAtual._anims.forEach((a) => a.play()); if (camadaAtual._video) camadaAtual._video.play().catch(() => {}); }
        ajustarSom(true);
      }
      ultimo = 0;
      requestAnimationFrame(quadroLoop);
    }
    function pausar() {
      if (!tocando) return;
      tocando = false;
      raiz.classList.add("pausado");
      bPlay.innerHTML = ICONES.play; bPlay.setAttribute("aria-label", "Tocar");
      if (camadaAtual) { camadaAtual._anims.forEach((a) => a.pause()); if (camadaAtual._video) camadaAtual._video.pause(); }
      ajustarSom(false);
    }
    function definirSom(ligado) {
      mudo = !ligado;
      raiz.classList.toggle("com-som", ligado);
      bSom.innerHTML = ligado ? ICONES.somOn : ICONES.somOff;
      bSom.setAttribute("aria-label", ligado ? "Desligar o som" : "Ligar o som");
      if (ligado) document.dispatchEvent(new CustomEvent("vespera:som", { detail: { fonte: "trailer" } }));
      ajustarSom(true);
    }
    function irPara(g) {
      let acc = 0;
      for (let i = 0; i < tomadas.length; i++) {
        if (g < acc + tomadas[i].dur || i === tomadas.length - 1) { mostrar(i, Math.max(0, g - acc)); break; }
        acc += tomadas[i].dur;
      }
      ajustarSom(true);
      atualizarBarra();
    }

    raiz.querySelector(".cinema__grande").addEventListener("click", () => { definirSom(true); tocar(); });
    raiz.querySelector(".cinema__som-aviso").addEventListener("click", (e) => { e.stopPropagation(); definirSom(true); tocar(); });
    bPlay.addEventListener("click", () => (tocando ? pausar() : tocar()));
    bSom.addEventListener("click", () => definirSom(mudo));
    raiz.querySelector('[data-acao="cheia"]').addEventListener("click", () => {
      if (document.fullscreenElement) document.exitFullscreen();
      else if (raiz.requestFullscreen) raiz.requestFullscreen().catch(() => {});
    });
    caixa.addEventListener("click", () => (tocando ? pausar() : tocar()));
    linha.addEventListener("click", (e) => { const r = linha.getBoundingClientRect(); irPara((e.clientX - r.left) / r.width * total); });
    linha.addEventListener("keydown", (e) => {
      if (e.key === "ArrowRight") { e.preventDefault(); irPara(Math.min(total - 0.1, tempoGlobal() + 5)); }
      if (e.key === "ArrowLeft") { e.preventDefault(); irPara(Math.max(0, tempoGlobal() - 5)); }
    });
    raiz.addEventListener("keydown", (e) => {
      if ((e.key === " " || e.key === "k") && e.target === raiz) { e.preventDefault(); tocando ? pausar() : tocar(); }
      if (e.key === "m" && e.target === raiz) definirSom(mudo);
    });
    document.addEventListener("vespera:som", (e) => { if (e.detail.fonte !== "trailer" && !mudo) definirSom(false); });

    // primeira imagem como capa
    mostrar(0, 1.2);
    atualizarBarra();

    // prévia automática sem som quando aparece na tela
    if (op.autoplay && !V.movimentoReduzido && "IntersectionObserver" in window) {
      new IntersectionObserver(([en]) => {
        if (en.intersectionRatio > 0.45 && !api.modalAberto) tocar();
        else if (en.intersectionRatio < 0.2) pausar();
      }, { threshold: [0, 0.2, 0.45, 0.7] }).observe(raiz);
    }

    const api = { tocar, pausar, definirSom, reiniciar: () => irPara(0), get tocando() { return tocando; }, modalAberto: false };
    instancias.push(api);
    return api;
  }

  /* ==========================================================================
     Modal: trailer montado ou prólogo (vídeo com som e controles)
     ========================================================================== */
  function iniciarModal(cinemaPagina) {
    const modal = document.getElementById("modal-video");
    if (!modal) return;
    const video = modal.querySelector(".modal__video");
    const raizCinema = modal.querySelector("#cinema-modal");
    const abas = Array.from(modal.querySelectorAll("[data-aba]"));
    let cinemaModal = null;
    let origem = null;

    function aba(nome) {
      abas.forEach((b) => b.setAttribute("aria-selected", String(b.dataset.aba === nome)));
      const prologo = nome === "prologo";
      video.hidden = !prologo;
      raizCinema.hidden = prologo;
      if (prologo) {
        if (cinemaModal) cinemaModal.pausar();
        video.querySelectorAll("source[data-src]").forEach((s) => { if (!s.src) s.src = s.dataset.src; });
        if (video.readyState === 0) video.load();
        video.currentTime = 0;
        video.play().catch(() => {});
        document.dispatchEvent(new CustomEvent("vespera:som", { detail: { fonte: "prologo" } }));
      } else {
        video.pause();
        if (!cinemaModal) cinemaModal = Cinema(raizCinema, {});
        cinemaModal.definirSom(true);
        cinemaModal.reiniciar();
        cinemaModal.tocar();
      }
    }
    function abrir(nome) {
      origem = document.activeElement;
      modal.hidden = false;
      if (cinemaPagina) { cinemaPagina.modalAberto = true; cinemaPagina.pausar(); }
      V.travarRolagem(true);
      aba(nome || "trailer");
      modal.querySelector(".modal__fechar").focus();
    }
    function fechar() {
      video.pause();
      if (cinemaModal) cinemaModal.pausar();
      if (cinemaPagina) cinemaPagina.modalAberto = false;
      modal.hidden = true;
      V.travarRolagem(false);
      if (origem) origem.focus();
    }
    document.addEventListener("click", (e) => {
      const b = e.target.closest("[data-abrir-modal]");
      if (b) { e.preventDefault(); abrir(b.dataset.abrirModal); }
    });
    modal.addEventListener("click", (e) => { if (e.target.closest("[data-fechar]")) fechar(); });
    abas.forEach((b) => b.addEventListener("click", () => aba(b.dataset.aba)));
    modal.addEventListener("keydown", (e) => {
      if (e.key === "Escape") { e.preventDefault(); fechar(); }
      if (e.key === "Tab") {
        const foco = Array.from(modal.querySelectorAll("button, [href], video[controls]:not([hidden]), [tabindex]:not([tabindex='-1'])")).filter((el) => !el.hidden && el.offsetParent !== null);
        if (!foco.length) return;
        const primeiro = foco[0], ultimo = foco[foco.length - 1];
        if (e.shiftKey && document.activeElement === primeiro) { e.preventDefault(); ultimo.focus(); }
        else if (!e.shiftKey && document.activeElement === ultimo) { e.preventDefault(); primeiro.focus(); }
      }
    });
    document.addEventListener("vespera:som", (e) => { if (e.detail.fonte !== "prologo") video.pause(); });
  }

  window.VesperaTrailer = { Cinema, iniciarModal };
})();
