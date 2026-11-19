/* ==========================================================================
   VÉSPERA · seções: monta o conteúdo vindo de dados.js e liga as interações.
   ========================================================================== */
(function () {
  "use strict";
  const DADOS = window.VESPERA;
  const V = window.Vespera;
  const S = window.VesperaSprites;
  const { $, $$, limitar } = V;
  const suave = (x) => { x = limitar(x, 0, 1); return x * x * (3 - 2 * x); };
  const faixa = (p, a, b) => suave((p - a) / (b - a));

  /* ---------------- selos de rank (formas de docs/brand/seals) ---------------- */
  const OCT = "56.0,41.9 41.9,56.0 22.1,56.0 8.0,41.9 8.0,22.1 22.1,8.0 41.9,8.0 56.0,22.1";
  const LOS = "32.0,4.0 60.0,32.0 32.0,60.0 4.0,32.0";
  const HEX = "32.0,5.0 55.4,18.5 55.4,45.5 32.0,59.0 8.6,45.5 8.6,18.5";
  const EST = "32.0,4.0 39.6,22.5 59.6,24.0 44.4,37.0 49.0,56.5 32.0,46.0 15.0,56.5 19.6,37.0 4.4,24.0 24.4,22.5";
  const SELOS = {
    E: [OCT, "#8A8FA8", 22, 39.9], D: [OCT, "#B4C2D4", 22, 39.9], C: [LOS, "#52E0C4", 22, 39.9], B: [LOS, "#5B8DEF", 22, 39.9],
    A: [HEX, "#9B6BD6", 22, 39.9], S: [HEX, "#E64A8E", 22, 39.9], SS: [EST, "#E64A8E", 16, 37.8], SSS: [EST, "#F2A541", 12, 36.3]
  };
  function selo(id) {
    if (id === "Alvor") {
      const raios = [[53, 32, 62, 32], [46.8, 46.8, 53.2, 53.2], [32, 53, 32, 62], [17.2, 46.8, 10.8, 53.2], [11, 32, 2, 32], [17.2, 17.2, 10.8, 10.8], [32, 11, 32, 2], [46.8, 17.2, 53.2, 10.8]]
        .map(([a, b, c, d]) => `<line x1="${a}" y1="${b}" x2="${c}" y2="${d}" stroke="#F2A541" stroke-width="4"/>`).join("");
      return `<svg viewBox="0 0 64 64" shape-rendering="crispEdges" aria-hidden="true">${raios}<polygon points="50,32 48.6,38.9 44.7,44.7 38.9,48.6 32,50 25.1,48.6 19.3,44.7 15.4,38.9 14,32 15.4,25.1 19.3,19.3 25.1,15.4 32,14 38.9,15.4 44.7,19.3 48.6,25.1" fill="#F2A541" stroke="#F4E7C6" stroke-width="2"/><text x="32" y="38.5" text-anchor="middle" font-family="'Pixelify Sans', monospace" font-weight="700" font-size="18" fill="#1B1F3B">☀</text></svg>`;
    }
    const [pts, cor, fs, y] = SELOS[id];
    return `<svg viewBox="0 0 64 64" shape-rendering="crispEdges" aria-hidden="true"><polygon points="${pts}" fill="${cor}" stroke="#F4E7C6" stroke-width="2"/><text x="32" y="${y}" text-anchor="middle" font-family="'Pixelify Sans', monospace" font-weight="700" font-size="${fs}" fill="#1B1F3B">${id}</text></svg>`;
  }
  const corSelo = (id) => (id === "Alvor" ? "#FFD66F" : SELOS[id][1]);

  /* ==========================================================================
     Montagem imediata (antes do núcleo observar os elementos .revelar)
     ========================================================================== */
  function montarNumeros() {
    const ul = $("#numeros-lista");
    if (!ul) return;
    ul.innerHTML = DADOS.numeros.map((n) => `<li class="numero revelar"><span class="numero__valor" data-valor="${n.valor}">${n.valor}${n.sufixo}</span><span class="numero__rotulo">${n.rotulo}</span></li>`).join("");
  }
  function montarGuardioes() {
    const grade = $("#guardioes-grade");
    if (!grade) return;
    grade.innerHTML = DADOS.guardioes.map((g) => `
      <article class="guardiao revelar" tabindex="0" data-id="${g.id}" data-ataque="${g.ataque}" aria-label="${g.nome}, Guardião do ${g.ato}">
        <div class="guardiao__palco">
          <img class="guardiao__mapa" src="img/mapa/${g.mapa}-800.webp" alt="" loading="lazy" decoding="async">
          <span class="guardiao__rank" title="Rank ${g.rank}">${selo(g.rank)}</span>
          <span class="guardiao__ato">${g.ato}</span>
          <div class="sprite" data-sprite="${g.id}" data-anim="parado" role="img" aria-label="Sprite animado: ${g.nome}"></div>
        </div>
        <div class="guardiao__corpo">
          <span class="guardiao__brecha">${g.brecha}</span>
          <h3 class="guardiao__nome">${g.nome}</h3>
          <p class="guardiao__linha">${g.linha}</p>
          <p class="guardiao__eco"><b>${g.eco}</b>${g.ecoTexto}</p>
        </div>
      </article>`).join("") + `
      <article class="guardiao guardiao--misterio revelar">
        <span class="q" aria-hidden="true">?</span>
        <h3 class="guardiao__nome">E lá embaixo…</h3>
        <p class="guardiao__linha">Mais Guardiões esperam no Avesso profundo. Sem spoiler: descubra jogando.</p>
      </article>`;
    S.preparar(grade);
  }
  function montarEscada() {
    const ol = $("#escada");
    if (!ol) return;
    ol.innerHTML = DADOS.ranks.map((r, i) => `<li class="degrau${r.id === "Alvor" ? " degrau--alvor" : ""}" style="--i:${i};--c:${corSelo(r.id)}">${selo(r.id)}<span>${r.id === "Alvor" ? "Alvor" : r.titulo}</span></li>`).join("");
    $$(".selo-mini[data-selo]").forEach((el) => { el.innerHTML = selo(el.dataset.selo); });
  }
  function montarCarrossel() {
    const ul = $("#carrossel");
    if (!ul) return;
    const extras = (window.VESPERA_VIDEOS_EXTRAS || []).filter((v) => v && v.arquivo).map((v) => ({ video: "video/extra/" + v.arquivo, legenda: v.legenda || "Vídeo" }));
    const itens = extras.concat(DADOS.capturas.map((c) => ({ img: c.src, legenda: c.legenda })));
    window.VESPERA_GALERIA = itens;
    ul.innerHTML = itens.map((it, i) => `
      <li class="carrossel__item">
        <figure style="margin:0">
          <button class="carrossel__botao" type="button" data-indice="${i}" aria-label="Ampliar: ${it.legenda}">
            ${it.video
              ? `<video src="${it.video}" muted loop playsinline preload="metadata" aria-hidden="true"></video><span class="carrossel__selo">Vídeo</span>`
              : `<img src="img/jogo/${it.img}-800.webp" alt="Captura do jogo: ${it.legenda}" loading="lazy" decoding="async">`}
          </button>
          <figcaption>${it.legenda}</figcaption>
        </figure>
      </li>`).join("");
  }
  function montarLogo() {
    const el = $(".logo-letras");
    if (!el || V.movimentoReduzido) return;
    el.innerHTML = Array.from(el.textContent).map((c, i) => `<span class="letra" style="--i:${i}">${c}</span>`).join("");
  }
  montarNumeros();
  montarGuardioes();
  montarEscada();
  montarCarrossel();
  montarLogo();
  S.preparar(document);

  /* ==========================================================================
     Interações (depois que o núcleo iniciou)
     ========================================================================== */
  function iniciarHero() {
    const hero = $(".hero");
    const video = $(".hero__video");
    if (!hero) return;
    V.rastrear(hero, (p, r) => hero.style.setProperty("--p", limitar(-r.top / r.height, 0, 1).toFixed(4)));
    document.addEventListener("vespera:pronto", () => hero.classList.add("iniciado"));
    if (!video || V.movimentoReduzido) return;
    const leve = window.innerWidth < 768 || V.economia;
    const base = leve ? "video/hero-540" : "video/hero-1080";
    video.innerHTML = `<source src="${base}.webm" type="video/webm"><source src="${base}.mp4" type="video/mp4">`;
    video.preload = "auto";
    video.addEventListener("playing", () => video.classList.add("pronto"), { once: true });
    video.load();
    const tentar = () => video.play().catch(() => {});
    new IntersectionObserver(([en]) => { if (en.isIntersecting) tentar(); else video.pause(); }).observe(hero);
    document.addEventListener("visibilitychange", () => { if (document.hidden) video.pause(); else tentar(); });
  }

  function iniciarHistoria() {
    const secao = $(".historia");
    if (!secao || V.movimentoReduzido) return;
    const cenas = $$(".cena", secao);
    const caps = $$(".cap", secao);
    const ind = $$(".historia__indice li", secao);
    const rasgo = $(".cena__rasgo", secao);
    let atual = -1;
    V.rastrear(secao, (p) => {
      const ent1 = faixa(p, 0.3, 0.4), ent2 = faixa(p, 0.63, 0.73);
      cenas[0].style.setProperty("--o", 1);
      cenas[1].style.setProperty("--o", ent1.toFixed(3));
      cenas[2].style.setProperty("--o", ent2.toFixed(3));
      const loc = [p / 0.4, (p - 0.3) / 0.43, (p - 0.63) / 0.37].map((x) => limitar(x, 0, 1));
      cenas.forEach((c, i) => {
        c.style.setProperty("--s", (1.04 + loc[i] * 0.16).toFixed(4));
        c.style.setProperty("--x", ((i % 2 ? -1 : 1) * loc[i] * 2).toFixed(2) + "%");
      });
      if (rasgo) cenas[2].style.setProperty("--r", faixa(p, 0.74, 0.92).toFixed(3));
      const t = [1 - faixa(p, 0.22, 0.3), faixa(p, 0.38, 0.45) * (1 - faixa(p, 0.56, 0.63)), faixa(p, 0.72, 0.8)];
      caps.forEach((c, i) => c.style.setProperty("--t", t[i].toFixed(3)));
      const k = p < 0.35 ? 0 : p < 0.68 ? 1 : 2;
      if (k !== atual) { atual = k; ind.forEach((li, i) => li.classList.toggle("ativo", i === k)); }
    }, "fixo");
  }

  function iniciarComo() {
    const passos = $$(".passo");
    const imgs = $$(".monitor__tela img");
    const trilho = $(".monitor__trilho");
    if (!passos.length) return;
    let ativo = -1, ciclo = 0, timer = 0;
    function mostrar(k) {
      if (k === ativo) return;
      ativo = k;
      passos.forEach((p, i) => p.classList.toggle("ativo", i === k));
      if (trilho) trilho.style.setProperty("--p", (k + 1) / passos.length);
      ciclo = 0;
      trocar();
      clearInterval(timer);
      if (!V.movimentoReduzido) timer = setInterval(() => { ciclo++; trocar(); }, 3400);
    }
    function trocar() {
      const doPasso = imgs.filter((im) => Number(im.dataset.passo) === ativo);
      const alvo = doPasso[ciclo % Math.max(1, doPasso.length)];
      imgs.forEach((im) => im.classList.toggle("ativo", im === alvo));
    }
    if (!("IntersectionObserver" in window)) { passos.forEach((p) => p.classList.add("ativo")); return; }
    const io = new IntersectionObserver((ents) => {
      ents.forEach((en) => { if (en.isIntersecting) mostrar(Number(en.target.dataset.passo)); });
    }, { rootMargin: "-45% 0px -45% 0px" });
    passos.forEach((p) => io.observe(p));
    mostrar(0);
  }

  function iniciarNumeros() {
    const valores = $$(".numero__valor");
    if (!valores.length || V.movimentoReduzido || !("IntersectionObserver" in window)) return;
    valores.forEach((el) => { el.textContent = "0"; });
    const io = new IntersectionObserver((ents) => {
      ents.forEach((en) => {
        if (!en.isIntersecting) return;
        io.unobserve(en.target);
        const alvo = Number(en.target.dataset.valor);
        const obj = { v: 0 };
        const escrever = () => { en.target.textContent = Math.round(obj.v); };
        if (window.gsap) window.gsap.to(obj, { v: alvo, duration: 1.8, ease: "expo.out", onUpdate: escrever, delay: 0.15 });
        else {
          const t0 = performance.now();
          const passo = (t) => { const k = limitar((t - t0) / 1800, 0, 1); obj.v = alvo * (1 - Math.pow(2, -10 * k)); escrever(); if (k < 1) requestAnimationFrame(passo); else en.target.textContent = alvo; };
          requestAnimationFrame(passo);
        }
      });
    }, { threshold: 0.6 });
    valores.forEach((el) => io.observe(el));
  }

  function iniciarBairros() {
    const secao = $(".bairros");
    if (!secao) return;
    const trilho = $(".bairros__trilho", secao);
    const paineis = $$(".painel", secao);
    const barra = $(".bairros__barra", secao);
    const anda = $(".cacador-anda", secao);
    const sprite = anda && $(".sprite", anda);
    const podeFixar = () => window.innerWidth >= 900 && !V.movimentoReduzido;
    let dist = 0, ultimoP = 0, parado = 0;

    const dica = $(".painel__dica b", secao);
    function medir() {
      secao.classList.toggle("fixar", podeFixar());
      if (dica) dica.textContent = podeFixar() ? "Role para atravessar a cidade" : "Arraste para o lado";
      trilho.style.transform = "";
      if (!podeFixar()) return;
      dist = Math.max(0, trilho.scrollWidth - window.innerWidth);
      secao.style.setProperty("--altura", (dist + window.innerHeight) + "px");
    }
    medir();
    window.addEventListener("resize", medir);
    V.rastrear(secao, (p) => {
      if (!secao.classList.contains("fixar")) return;
      trilho.style.transform = `translate3d(${(-p * dist).toFixed(1)}px,0,0)`;
      barra.style.setProperty("--p", p.toFixed(4));
      const cx = window.innerWidth / 2;
      paineis.forEach((pn) => {
        const r = pn.getBoundingClientRect();
        if (r.right < -200 || r.left > window.innerWidth + 200) return;
        pn.style.setProperty("--lp", ((r.left + r.width / 2 - cx) / window.innerWidth).toFixed(3));
      });
      if (sprite) {
        const dp = p - ultimoP;
        if (Math.abs(dp) > 0.0002) {
          if (sprite.dataset.anim !== "andar") S.aplicar(sprite, "lamina", "andar");
          anda.classList.toggle("esq", dp < 0);
          clearTimeout(parado);
          parado = setTimeout(() => S.aplicar(sprite, "lamina", "parado"), 180);
        }
        anda.style.transform = `translateX(${(p * (window.innerWidth * 0.62)).toFixed(1)}px)`;
      }
      ultimoP = p;
    }, "fixo");

    // atravessar para o lado da Brecha
    secao.addEventListener("click", (e) => {
      const b = e.target.closest("[data-virar]");
      if (!b) return;
      const pn = b.closest(".painel");
      const q = $(".painel__quadro", pn).getBoundingClientRect();
      const br = b.getBoundingClientRect();
      pn.style.setProperty("--cx", ((br.left + br.width / 2 - q.left) / q.width * 100).toFixed(1) + "%");
      pn.style.setProperty("--cy", ((br.top + br.height / 2 - q.top) / q.height * 100).toFixed(1) + "%");
      const virado = !pn.classList.contains("virado");
      pn.classList.toggle("virado", virado);
      b.setAttribute("aria-pressed", String(virado));
      b.textContent = virado ? "Voltar ao bairro" : "Ver a Brecha";
    });
  }

  function iniciarGuardioes() {
    $$(".guardiao[data-id]").forEach((card) => {
      const sp = $(".sprite", card);
      const id = card.dataset.id, ataque = card.dataset.ataque;
      const atacar = () => S.aplicar(sp, id, ataque);
      const parar = () => S.aplicar(sp, id, "parado");
      card.addEventListener("pointerenter", (e) => { if (e.pointerType === "mouse") atacar(); });
      card.addEventListener("pointerleave", (e) => { if (e.pointerType === "mouse") parar(); });
      card.addEventListener("focus", atacar);
      card.addEventListener("blur", () => { card.classList.remove("ativo"); parar(); });
      card.addEventListener("click", (e) => {
        if (e.pointerType === "mouse") return;
        const on = !card.classList.contains("ativo");
        $$(".guardiao.ativo").forEach((c) => { if (c !== card) { c.classList.remove("ativo"); S.aplicar($(".sprite", c), c.dataset.id, "parado"); } });
        card.classList.toggle("ativo", on);
        on ? atacar() : parar();
      });
    });
  }

  function iniciarRanks() {
    const secao = $(".ranks");
    if (!secao) return;
    const degraus = $$(".degrau", secao);
    const ficha = $(".ranks__ficha", secao);
    const palco = $(".ranks__palco", secao);
    let atual = -1;
    function preencher(k) {
      const r = DADOS.ranks[k];
      const cor = corSelo(r.id);
      palco.style.setProperty("--cor", cor);
      ficha.style.setProperty("--cor", cor);
      $(".ficha__selo", ficha).innerHTML = selo(r.id);
      $(".ficha__id", ficha).textContent = r.id === "Alvor" ? "☀" : r.id;
      $(".ficha__titulo", ficha).textContent = r.titulo;
      $(".ficha__texto", ficha).textContent = r.texto;
      $(".ficha__raridade", ficha).textContent = r.raridade;
      $(".ficha__guardiao", ficha).textContent = r.guardiao || "—";
      $(".ficha__exame", ficha).textContent = r.exame ? "Sim" : "Não";
      $(".ficha__estagio", ficha).textContent = `Estágio ${r.estagio} de 5: ${DADOS.estagios[r.estagio - 1]}`;
    }
    function definir(k) {
      if (k === atual) return;
      atual = k;
      degraus.forEach((d, i) => { d.classList.toggle("aceso", i <= k); d.classList.toggle("atual", i === k); });
      preencher(k);
      secao.classList.toggle("alvor", k === degraus.length - 1);
    }
    if (V.movimentoReduzido) { definir(degraus.length - 1); degraus.forEach((d) => d.classList.add("aceso")); return; }
    definir(0);
    V.rastrear(secao, (p) => definir(Math.min(degraus.length - 1, Math.floor(limitar((p - 0.04) / 0.84, 0, 1) * degraus.length * 0.999))), "fixo");
  }

  function iniciarFilme() {
    $$("[data-filme]").forEach((f) => {
      const imgs = $$("img", f);
      let i = 0, timer = 0;
      imgs[0].classList.add("ativo");
      if (V.movimentoReduzido) return;
      new IntersectionObserver(([en]) => {
        clearInterval(timer);
        if (en.isIntersecting) timer = setInterval(() => {
          imgs[i].classList.remove("ativo");
          i = (i + 1) % imgs.length;
          imgs[i].classList.add("ativo");
        }, 4200);
      }).observe(f);
    });
  }

  function iniciarParallax() {
    const cit = $(".citacao");
    if (cit) V.rastrear(cit, (p) => cit.style.setProperty("--p", p.toFixed(3)));
    const fim = $(".final");
    if (fim) V.rastrear(fim, (p) => fim.style.setProperty("--p", p.toFixed(3)));
  }

  /* ---------------- carrossel com arraste e inércia ---------------- */
  function iniciarCarrossel() {
    const tr = $("#carrossel");
    if (!tr) return;
    const passo = () => (tr.querySelector(".carrossel__item") || tr).getBoundingClientRect().width + 18;
    $(".carrossel__seta--esq").addEventListener("click", () => tr.scrollBy({ left: -passo(), behavior: "smooth" }));
    $(".carrossel__seta--dir").addEventListener("click", () => tr.scrollBy({ left: passo(), behavior: "smooth" }));
    tr.addEventListener("keydown", (e) => {
      if (e.key === "ArrowRight") { e.preventDefault(); tr.scrollBy({ left: passo(), behavior: "smooth" }); }
      if (e.key === "ArrowLeft") { e.preventDefault(); tr.scrollBy({ left: -passo(), behavior: "smooth" }); }
    });
    let x0 = 0, s0 = 0, vel = 0, ultimoX = 0, ultimoT = 0, arrastou = false, segurando = false;
    tr.addEventListener("pointerdown", (e) => {
      if (e.pointerType !== "mouse" || e.button !== 0) return;
      segurando = true; arrastou = false; x0 = ultimoX = e.clientX; s0 = tr.scrollLeft; vel = 0; ultimoT = performance.now();
    });
    window.addEventListener("pointermove", (e) => {
      if (!segurando) return;
      const dx = e.clientX - x0;
      if (!arrastou && Math.abs(dx) > 6) { arrastou = true; tr.classList.add("arrastando"); }
      if (!arrastou) return;
      tr.scrollLeft = s0 - dx;
      const t = performance.now();
      vel = (e.clientX - ultimoX) / Math.max(1, t - ultimoT);
      ultimoX = e.clientX; ultimoT = t;
    });
    window.addEventListener("pointerup", () => {
      if (!segurando) return;
      segurando = false;
      if (!arrastou) return;
      let v = vel * 16;
      const inercia = () => {
        tr.scrollLeft -= v; v *= 0.93;
        if (Math.abs(v) > 0.4) requestAnimationFrame(inercia); else tr.classList.remove("arrastando");
      };
      requestAnimationFrame(inercia);
      setTimeout(() => { arrastou = false; }, 0);
    });
    tr.addEventListener("click", (e) => {
      if (arrastou) { e.preventDefault(); return; }
      const b = e.target.closest("[data-indice]");
      if (b) abrirLuz(Number(b.dataset.indice));
    });
    // vídeos extras tocam só quando aparecem
    const vids = $$("video", tr);
    if (vids.length && "IntersectionObserver" in window) {
      const io = new IntersectionObserver((ents) => ents.forEach((en) => { if (en.isIntersecting && !V.movimentoReduzido) en.target.play().catch(() => {}); else en.target.pause(); }), { threshold: 0.4 });
      vids.forEach((v) => io.observe(v));
    }
  }

  /* ---------------- lightbox ---------------- */
  let luzIdx = 0, luzOrigem = null;
  function renderLuz() {
    const itens = window.VESPERA_GALERIA || [];
    const it = itens[luzIdx];
    const box = $("#caixa-luz");
    const midia = $(".caixa-luz__midia", box);
    midia.innerHTML = it.video
      ? `<video src="${it.video}" controls autoplay muted loop playsinline></video>`
      : `<img src="img/jogo/${it.img}-1600.webp" alt="Captura do jogo: ${it.legenda}">`;
    $(".caixa-luz__legenda", box).textContent = it.legenda;
    $(".caixa-luz__contador", box).textContent = `${luzIdx + 1} / ${itens.length}`;
  }
  function abrirLuz(i) {
    const box = $("#caixa-luz");
    luzOrigem = document.activeElement;
    luzIdx = i;
    box.hidden = false;
    V.travarRolagem(true);
    renderLuz();
    $(".caixa-luz__fechar", box).focus();
  }
  function fecharLuz() {
    const box = $("#caixa-luz");
    box.hidden = true;
    $(".caixa-luz__midia", box).innerHTML = "";
    V.travarRolagem(false);
    if (luzOrigem) luzOrigem.focus();
  }
  function moverLuz(d) {
    const n = (window.VESPERA_GALERIA || []).length;
    luzIdx = (luzIdx + d + n) % n;
    renderLuz();
  }
  function iniciarLuz() {
    const box = $("#caixa-luz");
    if (!box) return;
    $(".caixa-luz__fechar", box).addEventListener("click", fecharLuz);
    $(".caixa-luz__seta--esq", box).addEventListener("click", () => moverLuz(-1));
    $(".caixa-luz__seta--dir", box).addEventListener("click", () => moverLuz(1));
    box.addEventListener("click", (e) => { if (e.target === box) fecharLuz(); });
    box.addEventListener("keydown", (e) => {
      if (e.key === "Escape") fecharLuz();
      if (e.key === "ArrowRight") moverLuz(1);
      if (e.key === "ArrowLeft") moverLuz(-1);
      if (e.key === "Tab") {
        const f = $$("button", box);
        const i = f.indexOf(document.activeElement);
        e.preventDefault();
        f[(i + (e.shiftKey ? -1 : 1) + f.length) % f.length].focus();
      }
    });
    let x0 = null;
    box.addEventListener("pointerdown", (e) => { x0 = e.clientX; });
    box.addEventListener("pointerup", (e) => {
      if (x0 === null) return;
      const dx = e.clientX - x0;
      x0 = null;
      if (Math.abs(dx) > 50) moverLuz(dx < 0 ? 1 : -1);
    });
  }

  /* ---------------- player da trilha ---------------- */
  function iniciarPlayer() {
    const raiz = $("#player");
    if (!raiz) return;
    const audio = $("audio", raiz);
    const bPlay = $(".player__play", raiz);
    const bProx = $(".player__prox", raiz);
    const nome = $(".player__faixa", raiz);
    const trilhas = DADOS.trilhas;
    let i = 0, tocando = false, rampa = 0;
    const VOL = 0.55;
    function carregar(k) {
      i = (k + trilhas.length) % trilhas.length;
      audio.src = `audio/tema-${trilhas[i].id}.m4a`;
    }
    function volumeAte(alvo, depois) {
      cancelAnimationFrame(rampa);
      const passo = () => {
        const v = audio.volume;
        if (Math.abs(v - alvo) < 0.02) { audio.volume = alvo; if (depois) depois(); return; }
        audio.volume = limitar(v + Math.sign(alvo - v) * 0.02, 0, 1);
        rampa = requestAnimationFrame(passo);
      };
      passo();
    }
    function estado(on) {
      tocando = on;
      raiz.dataset.estado = on ? "tocando" : "parado";
      bPlay.setAttribute("aria-pressed", String(on));
      bPlay.setAttribute("aria-label", on ? "Pausar a trilha original" : "Tocar a trilha original");
      nome.textContent = on ? trilhas[i].nome : "Começa mudo · toque para ouvir";
    }
    function tocar() {
      if (!audio.src) carregar(0);
      audio.volume = 0;
      audio.play().then(() => { estado(true); volumeAte(VOL); document.dispatchEvent(new CustomEvent("vespera:som", { detail: { fonte: "player" } })); })
        .catch(() => V.avisar("Não foi possível tocar a trilha agora"));
    }
    function pausar() { estado(false); volumeAte(0, () => audio.pause()); }
    bPlay.addEventListener("click", () => (tocando ? pausar() : tocar()));
    bProx.addEventListener("click", () => { carregar(i + 1); if (tocando) { audio.volume = VOL; audio.play().catch(() => {}); nome.textContent = trilhas[i].nome; } else tocar(); });
    audio.addEventListener("ended", () => { carregar(i + 1); audio.play().catch(() => {}); nome.textContent = trilhas[i].nome; });
    document.addEventListener("vespera:som", (e) => { if (e.detail.fonte !== "player" && tocando) pausar(); });
  }

  function iniciar() {
    iniciarHero();
    iniciarHistoria();
    const lant = $(".lanterna");
    if (lant) S.Lanterna(lant);
    iniciarComo();
    iniciarNumeros();
    iniciarBairros();
    iniciarGuardioes();
    iniciarRanks();
    iniciarFilme();
    iniciarParallax();
    iniciarCarrossel();
    iniciarLuz();
    iniciarPlayer();
    const pag = $("#cinema-pagina");
    const cinema = pag ? window.VesperaTrailer.Cinema(pag, { autoplay: true }) : null;
    window.VesperaTrailer.iniciarModal(cinema);
  }
  document.addEventListener("DOMContentLoaded", iniciar);
})();
