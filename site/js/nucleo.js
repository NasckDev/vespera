/* ==========================================================================
   VÉSPERA · núcleo: utilidades, rolagem, cabeçalho, revelação, cursor, brasas.
   Funciona sem GSAP e sem Lenis (se a CDN falhar).
   ========================================================================== */
(function () {
  "use strict";

  const html = document.documentElement;
  const movimentoReduzido = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const ponteiroFino = window.matchMedia("(hover: hover) and (pointer: fine)").matches;
  const economia = !!(navigator.connection && navigator.connection.saveData);

  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const limitar = (v, a, b) => Math.min(b, Math.max(a, v));
  const misturar = (a, b, t) => a + (b - a) * t;

  /* ---------------- toast ---------------- */
  let toastTimer = 0;
  function avisar(texto) {
    const t = $(".toast");
    if (!t) return;
    t.textContent = texto;
    t.classList.add("visivel");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => t.classList.remove("visivel"), 2200);
  }

  /* ---------------- Lenis (rolagem suave) ---------------- */
  let lenis = null;
  function iniciarLenis() {
    if (movimentoReduzido || typeof window.Lenis !== "function") return;
    try {
      lenis = new window.Lenis({ lerp: 0.1, smoothWheel: true, wheelMultiplier: 1 });
      if (window.gsap && window.ScrollTrigger) {
        lenis.on("scroll", window.ScrollTrigger.update);
        window.gsap.ticker.add((t) => lenis.raf(t * 1000));
        window.gsap.ticker.lagSmoothing(0);
      } else {
        const loop = (t) => { lenis.raf(t); requestAnimationFrame(loop); };
        requestAnimationFrame(loop);
      }
    } catch (erro) {
      lenis = null;
    }
  }
  function irPara(alvo) {
    const el = typeof alvo === "string" ? $(alvo) : alvo;
    if (!el) return;
    if (lenis) lenis.scrollTo(el, { offset: 0, duration: 1.4 });
    else el.scrollIntoView({ behavior: movimentoReduzido ? "auto" : "smooth" });
  }
  function travarRolagem(travar) {
    if (lenis) (travar ? lenis.stop() : lenis.start());
    document.body.style.overflow = travar ? "hidden" : "";
  }

  /* ---------------- motor de progresso por rolagem ----------------
     modo "fixo": 0 quando o topo encosta no topo da tela, 1 quando o fim encosta no fim (seções sticky).
     modo "atravessa": 0 quando o topo entra por baixo, 1 quando o fim sai por cima. */
  const rastreados = [];
  function rastrear(el, fn, modo = "atravessa") {
    if (!el) return;
    rastreados.push({ el, fn, modo, ultimo: -1 });
  }
  let pedido = 0;
  function calcular() {
    pedido = 0;
    const vh = window.innerHeight;
    for (const item of rastreados) {
      const r = item.el.getBoundingClientRect();
      if (r.bottom < -vh * 0.5 || r.top > vh * 1.5) continue;
      let p;
      if (item.modo === "fixo") {
        const total = r.height - vh;
        p = total > 0 ? limitar(-r.top / total, 0, 1) : 0;
      } else {
        p = limitar((vh - r.top) / (vh + r.height), 0, 1);
      }
      if (Math.abs(p - item.ultimo) > 0.0005) {
        item.ultimo = p;
        item.fn(p, r);
      }
    }
  }
  function agendar() { if (!pedido) pedido = requestAnimationFrame(calcular); }

  /* ---------------- barra de progresso + cabeçalho ---------------- */
  function iniciarTopo() {
    const barra = $(".progresso");
    const topo = $(".topo");
    let ultimoY = window.scrollY;
    const links = $$(".topo__nav a");
    const secoes = links.map((a) => $(a.getAttribute("href"))).filter(Boolean);

    function aoRolar() {
      const y = window.scrollY;
      const max = document.documentElement.scrollHeight - window.innerHeight;
      barra.style.setProperty("--p", max > 0 ? (y / max).toFixed(4) : 0);
      topo.classList.toggle("solido", y > 40);
      const descendo = y > ultimoY + 4;
      const subindo = y < ultimoY - 4;
      if (descendo && y > window.innerHeight * 0.8 && !document.body.classList.contains("menu-aberto")) topo.classList.add("escondido");
      if (subindo || y < 100) topo.classList.remove("escondido");
      ultimoY = y;
      let ativa = null;
      for (const s of secoes) { if (s.getBoundingClientRect().top < window.innerHeight * 0.45) ativa = s; }
      links.forEach((a) => a.classList.toggle("ativo", !!ativa && a.getAttribute("href") === "#" + ativa.id));
    }
    window.addEventListener("scroll", aoRolar, { passive: true });
    aoRolar();
  }

  /* ---------------- menu móvel ---------------- */
  function iniciarMenu() {
    const botao = $(".topo__menu");
    const menu = $("#menu-movel");
    if (!botao || !menu) return;
    $$("nav a", menu).forEach((a, i) => a.style.setProperty("--i", i));
    function abrir(aberto) {
      botao.setAttribute("aria-expanded", String(aberto));
      botao.setAttribute("aria-label", aberto ? "Fechar menu" : "Abrir menu");
      document.body.classList.toggle("menu-aberto", aberto);
      if (aberto) { menu.hidden = false; requestAnimationFrame(() => menu.classList.add("aberto")); }
      else { menu.classList.remove("aberto"); setTimeout(() => { if (!menu.classList.contains("aberto")) menu.hidden = true; }, 700); }
      travarRolagem(aberto);
    }
    botao.addEventListener("click", () => abrir(botao.getAttribute("aria-expanded") !== "true"));
    menu.addEventListener("click", (e) => { if (e.target.closest("a")) abrir(false); });
    document.addEventListener("keydown", (e) => { if (e.key === "Escape" && botao.getAttribute("aria-expanded") === "true") { abrir(false); botao.focus(); } });
  }

  /* ---------------- âncoras com rolagem suave ---------------- */
  function iniciarAncoras() {
    document.addEventListener("click", (e) => {
      const a = e.target.closest('a[href^="#"]');
      if (!a) return;
      const id = a.getAttribute("href");
      if (id.length < 2) return;
      const alvo = $(id);
      if (!alvo) return;
      e.preventDefault();
      irPara(alvo);
      history.replaceState(null, "", id);
    });
  }

  /* ---------------- revelação e títulos em palavras ---------------- */
  function dividirPalavras(el) {
    if (el.dataset.dividido) return;
    el.dataset.dividido = "1";
    const palavras = el.textContent.trim().split(/\s+/);
    el.setAttribute("aria-label", el.textContent.trim());
    el.innerHTML = palavras.map((p, i) => `<span class="palavra" aria-hidden="true" style="--i:${i}"><span>${p}</span></span>`).join(" ");
  }
  function iniciarRevelar() {
    const alvos = $$(".revelar, .titulo-secao, .titulo-final, .cap__titulo");
    if (movimentoReduzido || !("IntersectionObserver" in window)) {
      alvos.forEach((el) => el.classList.add("visivel"));
      return;
    }
    $$(".titulo-secao, .titulo-final").forEach(dividirPalavras);
    const io = new IntersectionObserver((entradas) => {
      entradas.forEach((en) => {
        if (en.isIntersecting) { en.target.classList.add("visivel"); io.unobserve(en.target); }
      });
    }, { rootMargin: "0px 0px -12% 0px", threshold: 0.08 });
    alvos.forEach((el) => io.observe(el));
  }

  /* ---------------- cursor: halo de lanterna + brilho nos cartões ---------------- */
  function iniciarCursor() {
    if (!ponteiroFino || movimentoReduzido) return;
    const halo = $(".halo");
    let x = -500, y = -500, hx = x, hy = y, rodando = false;
    function passo() {
      hx = misturar(hx, x, 0.18); hy = misturar(hy, y, 0.18);
      halo.style.transform = `translate3d(${hx}px, ${hy}px, 0)`;
      if (Math.abs(hx - x) + Math.abs(hy - y) > 0.5) requestAnimationFrame(passo); else rodando = false;
    }
    window.addEventListener("pointermove", (e) => {
      if (e.pointerType !== "mouse") return;
      x = e.clientX; y = e.clientY;
      halo.classList.add("ativo");
      if (!rodando) { rodando = true; requestAnimationFrame(passo); }
    }, { passive: true });
    document.addEventListener("pointerleave", () => halo.classList.remove("ativo"));
    document.addEventListener("pointerover", (e) => {
      halo.classList.toggle("grande", !!e.target.closest("a, button, .guardiao, .carrossel__botao"));
    });
  }

  /* brilho que segue o mouse (cartões, números, botões primários) */
  function iniciarBrilho() {
    if (!ponteiroFino) return;
    document.addEventListener("pointermove", (e) => {
      const alvo = e.target.closest(".guardiao, .numero, .botao--primario");
      if (!alvo) return;
      const r = alvo.getBoundingClientRect();
      const px = ((e.clientX - r.left) / r.width * 100).toFixed(1) + "%";
      const py = ((e.clientY - r.top) / r.height * 100).toFixed(1) + "%";
      if (alvo.classList.contains("botao--primario")) { alvo.style.setProperty("--bx", px); alvo.style.setProperty("--by", py); }
      else { alvo.style.setProperty("--mx", px); alvo.style.setProperty("--my", py); }
    }, { passive: true });
  }

  /* ---------------- botões magnéticos ---------------- */
  function iniciarMagneticos() {
    if (!ponteiroFino || movimentoReduzido) return;
    $$(".magnetico").forEach((b) => {
      const forca = 0.32;
      let anim = null;
      const mover = (dx, dy, dur) => {
        if (window.gsap) { window.gsap.to(b, { x: dx, y: dy, duration: dur, ease: dur > 0.5 ? "elastic.out(1, .4)" : "power3.out" }); return; }
        if (anim) anim.cancel();
        anim = b.animate([{ transform: getComputedStyle(b).transform === "none" ? "translate(0,0)" : getComputedStyle(b).transform }, { transform: `translate(${dx}px, ${dy}px)` }], { duration: dur * 1000, easing: "cubic-bezier(.22,1,.36,1)", fill: "forwards" });
      };
      b.addEventListener("pointermove", (e) => {
        const r = b.getBoundingClientRect();
        mover((e.clientX - r.left - r.width / 2) * forca, (e.clientY - r.top - r.height / 2) * forca, 0.4);
      });
      b.addEventListener("pointerleave", () => mover(0, 0, 0.9));
    });
  }

  /* ---------------- marquee ligado à velocidade da rolagem ---------------- */
  function iniciarFaixa() {
    const linhas = $$(".faixa__linha");
    if (!linhas.length || movimentoReduzido) return;
    html.classList.add("js-faixa");
    const estado = linhas.map((el) => ({ el, x: 0, dir: Number(el.dataset.dir) || 1, larg: 0 }));
    let ultimoY = window.scrollY, vel = 0, visivel = false, ultimoT = 0;
    const medir = () => estado.forEach((s) => { s.larg = s.el.scrollWidth / 2; });
    medir();
    window.addEventListener("resize", medir);
    new IntersectionObserver(([en]) => { visivel = en.isIntersecting; if (visivel) requestAnimationFrame(loop); }).observe($(".faixa"));
    function loop(t) {
      if (!visivel) return;
      const dt = Math.min(50, t - (ultimoT || t)); ultimoT = t;
      const y = window.scrollY; const dy = y - ultimoY; ultimoY = y;
      vel = misturar(vel, dy, 0.1);
      for (const s of estado) {
        s.x -= (0.05 * dt + vel * 0.35) * s.dir;
        if (s.larg) { s.x = ((s.x % s.larg) + s.larg) % s.larg; }
        s.el.style.transform = `translate3d(${-s.x}px,0,0)`;
      }
      requestAnimationFrame(loop);
    }
  }

  /* ---------------- brasas / vagalumes em canvas ---------------- */
  function Brasas(canvas) {
    const ctx = canvas.getContext("2d");
    const total = Math.round((Number(canvas.dataset.brasas) || 40) * (window.innerWidth < 700 ? 0.45 : 1));
    let w = 0, h = 0, ativo = false, ultimo = 0;
    const parts = [];
    // brilho pré-renderizado (evita criar gradientes a cada quadro)
    const brilho = {};
    ["255,190,110", "255,110,180"].forEach((cor) => {
      const c = document.createElement("canvas");
      c.width = c.height = 32;
      const g2 = c.getContext("2d");
      const g = g2.createRadialGradient(16, 16, 0, 16, 16, 16);
      g.addColorStop(0, `rgba(${cor},1)`);
      g.addColorStop(1, `rgba(${cor},0)`);
      g2.fillStyle = g;
      g2.fillRect(0, 0, 32, 32);
      brilho[cor] = c;
    });
    function nova(inicio) {
      return {
        x: Math.random() * w, y: inicio ? Math.random() * h : h + 10,
        vx: (Math.random() - 0.5) * 0.25, vy: -(0.25 + Math.random() * 0.7),
        r: 0.8 + Math.random() * 2.2, fase: Math.random() * Math.PI * 2,
        cor: Math.random() < 0.72 ? "255,190,110" : "255,110,180", vida: 0.4 + Math.random() * 0.6
      };
    }
    function medir() {
      w = canvas.width = canvas.offsetWidth;
      h = canvas.height = canvas.offsetHeight;
      parts.length = 0;
      for (let i = 0; i < total; i++) parts.push(nova(true));
    }
    function quadro(t) {
      if (!ativo) return;
      requestAnimationFrame(quadro);
      if (t - ultimo < 33) return; // ~30 fps, leve para GPU integrada
      ultimo = t;
      ctx.clearRect(0, 0, w, h);
      ctx.globalCompositeOperation = "lighter";
      for (let i = 0; i < parts.length; i++) {
        const p = parts[i];
        p.fase += 0.04;
        p.x += p.vx + Math.sin(p.fase) * 0.35;
        p.y += p.vy;
        if (p.y < -20) parts[i] = nova(false);
        ctx.globalAlpha = limitar(p.vida * (0.55 + Math.sin(p.fase * 1.7) * 0.45) * limitar(p.y / (h * 0.25), 0, 1), 0, 1);
        const raio = p.r * 5;
        ctx.drawImage(brilho[p.cor], p.x - raio, p.y - raio, raio * 2, raio * 2);
      }
      ctx.globalAlpha = 1;
    }
    medir();
    window.addEventListener("resize", medir);
    new IntersectionObserver(([en]) => {
      const antes = ativo;
      ativo = en.isIntersecting && !document.hidden;
      if (ativo && !antes) requestAnimationFrame(quadro);
    }).observe(canvas);
  }
  function iniciarBrasas() {
    if (movimentoReduzido) return;
    $$("canvas.brasas").forEach((c) => Brasas(c));
  }

  /* ---------------- copiar cores da paleta ---------------- */
  function iniciarPaleta() {
    document.addEventListener("click", (e) => {
      const b = e.target.closest("[data-copiar]");
      if (!b) return;
      const valor = b.dataset.copiar;
      const ok = () => avisar(`Cor ${valor} copiada`);
      if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(valor).then(ok, () => avisar(valor));
      else {
        const t = document.createElement("textarea");
        t.value = valor; document.body.appendChild(t); t.select();
        try { document.execCommand("copy"); ok(); } catch (erro) { avisar(valor); }
        t.remove();
      }
    });
  }

  /* ---------------- preloader ---------------- */
  function iniciarPreloader(pronto) {
    const pre = $(".preloader");
    const pct = $(".preloader__pct");
    const barra = $(".preloader__barra span");
    const tarefas = [];
    const poster = $(".hero__poster");
    if (poster && !poster.complete) tarefas.push(new Promise((r) => { poster.addEventListener("load", r, { once: true }); poster.addEventListener("error", r, { once: true }); }));
    if (document.fonts && document.fonts.ready) tarefas.push(document.fonts.ready);
    let feitas = 0;
    const total = Math.max(1, tarefas.length);
    tarefas.forEach((t) => t.then(() => { feitas++; mostrar(feitas / total); }));
    function mostrar(p) {
      if (pct) pct.textContent = Math.round(p * 100) + "%";
      if (barra) barra.style.setProperty("--p", Math.max(0.05, p));
    }
    let terminou = false;
    function terminar() {
      if (terminou) return;
      terminou = true;
      mostrar(1);
      setTimeout(() => { html.classList.add("carregado"); pronto(); if (pre) setTimeout(() => pre.remove(), 900); }, movimentoReduzido ? 0 : 280);
    }
    Promise.all(tarefas).then(terminar);
    setTimeout(terminar, 1800);
  }

  /* ---------------- API pública ---------------- */
  window.Vespera = {
    $, $$, limitar, misturar, avisar, rastrear, agendar, irPara, travarRolagem,
    movimentoReduzido, ponteiroFino, economia,
    get lenis() { return lenis; }
  };

  function iniciar() {
    iniciarLenis();
    iniciarTopo();
    iniciarMenu();
    iniciarAncoras();
    iniciarRevelar();
    iniciarCursor();
    iniciarBrilho();
    iniciarMagneticos();
    iniciarFaixa();
    iniciarBrasas();
    iniciarPaleta();
    window.addEventListener("scroll", agendar, { passive: true });
    // pausa as animações de sprite das seções fora da tela
    if ("IntersectionObserver" in window) {
      const io = new IntersectionObserver((ents) => ents.forEach((en) => en.target.classList.toggle("fora", !en.isIntersecting)), { rootMargin: "200px" });
      $$("main > section").forEach((s) => io.observe(s));
    }
    window.addEventListener("resize", () => { rastreados.forEach((i) => { i.ultimo = -1; }); agendar(); });
    iniciarPreloader(() => {
      document.dispatchEvent(new CustomEvent("vespera:pronto"));
      agendar();
    });
  }
  // Scripts "defer" rodam antes do DOMContentLoaded: esperar garante que secoes.js já montou o conteúdo.
  if (document.readyState === "complete") iniciar();
  else document.addEventListener("DOMContentLoaded", iniciar);
})();
