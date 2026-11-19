/* ==========================================================================
   VÉSPERA · sprites animados (CSS steps e canvas) e o diorama da Lanterna.
   As tiras img/sprites/<id>-<anim>.webp foram recortadas dos atlas do jogo
   (assets/v2/sprites/*, guiados pelos JSON de cada atlas).
   ========================================================================== */
(function () {
  "use strict";
  const DADOS = window.VESPERA;
  const V = window.Vespera;

  function caminho(id, anim) { return `img/sprites/${id}-${anim}.webp`; }

  /* ---------- sprite em CSS ---------- */
  function aplicar(el, id, anim) {
    const meta = DADOS.sprites[id];
    const a = meta && meta.anims[anim];
    if (!a) return el;
    el.style.setProperty("--w", meta.w);
    el.style.setProperty("--h", meta.h);
    el.style.setProperty("--n", a.n);
    el.style.setProperty("--fps", a.fps);
    el.style.backgroundImage = `url("${caminho(id, anim)}")`;
    el.dataset.sprite = id;
    el.dataset.anim = anim;
    return el;
  }
  function criar(id, anim, k) {
    const el = document.createElement("div");
    el.className = "sprite";
    el.setAttribute("aria-hidden", "true");
    if (k) el.style.setProperty("--k", k);
    return aplicar(el, id, anim);
  }
  function preparar(raiz) {
    (raiz || document).querySelectorAll(".sprite[data-sprite]").forEach((el) => aplicar(el, el.dataset.sprite, el.dataset.anim || "parado"));
  }

  /* ---------- sprite em canvas ---------- */
  const cache = {};
  function imagem(id, anim) {
    const k = id + "-" + anim;
    if (!cache[k]) {
      const img = new Image();
      img.decoding = "async";
      img.src = caminho(id, anim);
      cache[k] = img;
    }
    return cache[k];
  }
  function desenhar(ctx, id, anim, t, x, y, altura, virar) {
    const meta = DADOS.sprites[id];
    const a = meta.anims[anim];
    const img = imagem(id, anim);
    if (!img.complete || !img.naturalWidth) return;
    const q = Math.floor(t * a.fps) % a.n;
    const esc = altura / meta.h;
    const w = meta.w * esc, h = meta.h * esc;
    ctx.save();
    ctx.translate(x, y);
    if (virar) ctx.scale(-1, 1);
    ctx.drawImage(img, q * meta.w, 0, meta.w, meta.h, -w / 2, -h, w, h);
    ctx.restore();
  }

  /* ==========================================================================
     Diorama da Lanterna: praça escura, caçador andando, monstros escondidos.
     ========================================================================== */
  function Lanterna(secao) {
    const canvas = secao.querySelector(".lanterna__tela");
    const fallback = secao.querySelector(".lanterna__fallback");
    const ctx = canvas.getContext("2d");
    const escuro = document.createElement("canvas");
    const ectx = escuro.getContext("2d");
    const mapa = new Image();
    mapa.decoding = "async";
    let W = 0, H = 0, ativo = false, ultimo = 0, inicio = 0, pronto = false;
    const ptr = { x: 0, y: 0, tem: false, ultimo: 0 };
    const luz = { x: 0, y: 0 };

    // trajeto do caçador em coordenadas do mapa (0..1)
    const rota = [[0.36, 0.64], [0.58, 0.68], [0.68, 0.52], [0.56, 0.4], [0.4, 0.42], [0.3, 0.52]];
    const cacador = { x: rota[0][0], y: rota[0][1], alvo: 1, pausa: 0, virar: false, andando: true };
    const monstros = [
      { id: "lodo", anim: "parado", x: 0.62, y: 0.36, alt: 0.045, troca: 0 },
      { id: "espectro", anim: "mover", x: 0.3, y: 0.34, alt: 0.07, onda: 0 },
      { id: "carrapicho", anim: "parado", x: 0.74, y: 0.62, alt: 0.045 },
      { id: "lodo", anim: "parado", x: 0.22, y: 0.68, alt: 0.04, troca: 1.5 }
    ];
    ["lamina-parado", "lamina-andar", "lodo-parado", "lodo-pulo", "espectro-mover", "carrapicho-parado"].forEach((s) => { const [i, a] = s.split("-"); imagem(i, a); });

    let vista = { s: 1, ox: 0, oy: 0 };
    function medir() {
      const dpr = Math.min(window.devicePixelRatio || 1, 1.25);
      W = canvas.width = Math.min(1600, Math.round(canvas.clientWidth * dpr));
      H = canvas.height = Math.round(canvas.clientHeight * (W / Math.max(1, canvas.clientWidth)));
      escuro.width = W; escuro.height = H;
      ptr.x = luz.x = W * 0.5; ptr.y = luz.y = H * 0.55;
    }
    function calcularVista(t) {
      const mw = mapa.naturalWidth, mh = mapa.naturalHeight;
      const zoom = W > H ? 1.45 : 1.05;
      const s = Math.max(W / mw, H / mh) * zoom;
      const vw = mw * s, vh = mh * s;
      const deriva = V.movimentoReduzido ? 0 : Math.sin(t * 0.07) * 0.03;
      vista = { s, ox: (W - vw) / 2 + deriva * vw, oy: (H - vh) / 2 + Math.cos(t * 0.05) * 0.015 * vh, vw, vh };
    }
    const tela = (mx, my) => [vista.ox + mx * vista.vw, vista.oy + my * vista.vh];

    function atualizarCacador(dt) {
      if (cacador.pausa > 0) { cacador.pausa -= dt; cacador.andando = false; return; }
      const [ax, ay] = rota[cacador.alvo];
      const dx = ax - cacador.x, dy = ay - cacador.y;
      const d = Math.hypot(dx, dy);
      const vel = 0.045;
      if (d < 0.004) { cacador.alvo = (cacador.alvo + 1) % rota.length; cacador.pausa = 1 + Math.random() * 1.2; return; }
      cacador.andando = true;
      cacador.x += (dx / d) * vel * dt;
      cacador.y += (dy / d) * vel * dt;
      if (Math.abs(dx) > 0.002) cacador.virar = dx < 0;
    }

    function luzRadial(c, x, y, r, forca) {
      const g = c.createRadialGradient(x, y, 0, x, y, r);
      g.addColorStop(0, `rgba(0,0,0,${forca})`);
      g.addColorStop(0.45, `rgba(0,0,0,${forca * 0.75})`);
      g.addColorStop(1, "rgba(0,0,0,0)");
      c.fillStyle = g;
      c.fillRect(x - r, y - r, r * 2, r * 2);
    }

    function quadro(agora) {
      if (!ativo) return;
      requestAnimationFrame(quadro);
      if (agora - ultimo < 33) return;
      const dt = Math.min(0.1, (agora - (ultimo || agora)) / 1000);
      ultimo = agora;
      desenharCena((agora - inicio) / 1000, dt);
    }

    function desenharCena(t, dt) {
      if (!pronto) return;
      calcularVista(t);
      atualizarCacador(dt);
      ctx.drawImage(mapa, vista.ox, vista.oy, vista.vw, vista.vh);

      // monstros e caçador ordenados por profundidade (y)
      const lista = monstros.map((m) => {
        let anim = m.anim, x = m.x, y = m.y;
        if (m.troca !== undefined) anim = Math.floor((t + m.troca) / 2.2) % 2 ? "pulo" : "parado";
        if (m.onda !== undefined) { x += Math.sin(t * 0.4) * 0.05; y += Math.cos(t * 0.6) * 0.015; }
        return { id: m.id, anim, x, y, alt: m.alt, virar: m.onda !== undefined ? Math.cos(t * 0.4) < 0 : false };
      });
      lista.push({ id: "lamina", anim: cacador.andando ? "andar" : "parado", x: cacador.x, y: cacador.y, alt: 0.085, virar: cacador.virar });
      lista.sort((a, b) => a.y - b.y);
      for (const s of lista) {
        const [sx, sy] = tela(s.x, s.y);
        desenhar(ctx, s.id, s.anim, t, sx, sy, s.alt * vista.vh, s.virar);
      }

      // luz: segue o ponteiro; sem ponteiro, segue o caçador com atraso
      const [hx, hy] = tela(cacador.x, cacador.y - 0.04);
      const usaPtr = ptr.tem && performance.now() - ptr.ultimo < 4000;
      const alvoX = usaPtr ? ptr.x : hx, alvoY = usaPtr ? ptr.y : hy;
      luz.x += (alvoX - luz.x) * (V.movimentoReduzido ? 1 : 0.14);
      luz.y += (alvoY - luz.y) * (V.movimentoReduzido ? 1 : 0.14);
      const R = Math.min(W, H) * (W < 700 ? 0.46 : 0.38) * (1 + Math.sin(t * 3.1) * 0.02 + Math.sin(t * 7.3) * 0.012);

      ectx.globalCompositeOperation = "source-over";
      ectx.clearRect(0, 0, W, H);
      ectx.fillStyle = "rgba(5,4,14,0.86)";
      ectx.fillRect(0, 0, W, H);
      ectx.globalCompositeOperation = "destination-out";
      luzRadial(ectx, luz.x, luz.y, R, 1);
      luzRadial(ectx, hx, hy, R * 0.5, 0.9);
      ctx.drawImage(escuro, 0, 0);

      // brilho quente da chama
      ctx.globalCompositeOperation = "lighter";
      const g = ctx.createRadialGradient(luz.x, luz.y, 0, luz.x, luz.y, R * 0.8);
      g.addColorStop(0, "rgba(255,170,80,0.16)");
      g.addColorStop(1, "rgba(255,170,80,0)");
      ctx.fillStyle = g;
      ctx.fillRect(luz.x - R, luz.y - R, R * 2, R * 2);
      ctx.globalCompositeOperation = "source-over";
    }

    function aoMover(x, y) {
      const r = canvas.getBoundingClientRect();
      ptr.x = (x - r.left) * (W / r.width);
      ptr.y = (y - r.top) * (H / r.height);
      ptr.tem = true;
      ptr.ultimo = performance.now();
      if (V.movimentoReduzido) desenharCena(0, 0);
    }
    secao.addEventListener("pointermove", (e) => aoMover(e.clientX, e.clientY), { passive: true });
    secao.addEventListener("touchmove", (e) => { const t0 = e.touches[0]; if (t0) aoMover(t0.clientX, t0.clientY); }, { passive: true });
    secao.addEventListener("pointerleave", () => { ptr.tem = false; });

    function comecar() {
      medir();
      mapa.onload = () => {
        pronto = true;
        if (fallback) fallback.classList.add("oculto");
        inicio = performance.now();
        if (V.movimentoReduzido) setTimeout(() => desenharCena(0, 0), 300);
      };
      mapa.src = `img/mapa/praca-escura-${canvas.clientWidth > 900 ? 1600 : 800}.webp`;
      window.addEventListener("resize", () => { medir(); if (V.movimentoReduzido) desenharCena(0, 0); });
      new IntersectionObserver(([en]) => {
        const antes = ativo;
        ativo = en.isIntersecting && !V.movimentoReduzido;
        if (ativo && !antes) { ultimo = 0; requestAnimationFrame(quadro); }
      }, { rootMargin: "100px" }).observe(secao);
    }
    // só carrega o mapa quando a seção se aproxima
    const pre = new IntersectionObserver(([en]) => { if (en.isIntersecting) { pre.disconnect(); comecar(); } }, { rootMargin: "600px" });
    pre.observe(secao);
  }

  window.VesperaSprites = { criar, aplicar, preparar, imagem, desenhar, Lanterna };
})();
