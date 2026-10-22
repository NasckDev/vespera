namespace Vespera.UI;

/// <summary>
/// Textos de interface da v2, verbatim (fonte em cada bloco). Falas de NPC e dados de jogo ficam nos JSON de
/// <c>src/Data</c>; aqui só o que é da casca das telas (título, história, ajuda, mural, morte…).
/// </summary>
public static class UiText
{
    // --- Título (extra.js:262-299) ---
    public const string Slogan = "Cada véspera, uma Brecha. Cada Brecha, uma memória.";
    public const string MenuNewGame = "Novo jogo";
    public const string MenuNewGameSub = "Escolha sua classe e comece a véspera";
    public const string MenuContinue = "Continuar";
    public const string MenuContinueEmpty = "Nenhum jogo salvo ainda";
    public const string MenuHelp = "Como jogar";
    public const string MenuHelpSub = "História, regras, combate e Brechas";
    public const string MenuControls = "Controles";
    public const string MenuControlsSub = "Teclado, mouse e gamepad";
    public const string MenuOptions = "Opções";
    public const string MenuOptionsSub = "Volume, tela cheia, tremor de tela, dicas";
    public const string MenuQuit = "Sair";
    public const string MenuQuitSub = "Fechar o jogo";
    public const string ConfirmTitle = "Começar do zero?";
    public const string ConfirmText = "Um novo jogo apaga o progresso salvo de {name} (Dia {day}, Selo {rank}).";
    public const string ConfirmErase = "Apagar e começar";
    public const string ConfirmCancel = "Cancelar";

    // --- História (STORY, main.js:144-149) ---
    public static readonly (string Title, string Text)[] Story =
    [
        ("Véspera", "Véspera é a hora antes do anoitecer. É quando as Brechas abrem. Todo fim de tarde, a cidade de Lumeira prende a respiração."),
        ("Lumeira", "Lumeira tem bairros, estações e gente com rotina: Brás na forja, Lúcia na estalagem, Faísca acendendo as lanternas da rua. É uma cidade bonita e frágil, e é compartilhada por todos os caçadores."),
        ("As Brechas", "Cada Brecha herda o bairro e a estação onde abriu. No Mercado de Peixes, no inverno, vira uma maré congelada. No Cemitério, no outono, se enche de espectros. Se ninguém a fecha a tempo, ela rompe: monstros na rua, lojas fechadas, preços altos. A cidade sente."),
        ("Os caçadores", "Não existe classe fixa: a arma define o estilo. Monstros têm partes, e o que você quebra é o que você leva. Alguns chefes deixam Ecos, habilidades que mudam quem os absorve. Seu Selo sobe de E até Alvor, o caçador que faz amanhecer."),
        ("A Lanterna", "Se você cair dentro de uma Brecha, sua Lanterna fica para trás, com tudo o que você carregava. Outro caçador pode trazê-la de volta. Em Lumeira, alguém sempre vai buscar."),
    ];

    public const string StoryChapter = "Capítulo {n} de {total}";
    public const string StoryNext = "Continuar";
    public const string StoryLast = "Escolher arma";
    public const string StorySkip = "Pular";

    // --- Classe (extra.js:300-324) ---
    public const string ClassTitle = "Escolha sua classe";
    public const string ClassSubtitle = "A classe é permanente. Só a arma e os equipamentos da sua classe poderão ser usados.";
    public const string ClassName = "Nome:";
    public const string ClassDifficulty = "Dificuldade: ";
    public const string ClassUltimate = "Supremo [F]";
    public const string ClassChoose = "Escolher";
    public const string ClassStart = "Começar";
    public const string Back = "Voltar";

    // --- Como jogar (HELP, extra.js:170-177) ---
    public static readonly (string Tab, string Title, string[] Paragraphs)[] Help =
    [
        ("História", "O Ano Sem Alvor",
        [
            "Há vinte anos o sol se pôs sobre Lumeira e só voltou um ano depois. Nesse tempo as primeiras Brechas se abriram: rasgos no ar que dão para o Avesso, uma Lumeira invertida, feita de tudo que a cidade perdeu, esqueceu ou enterrou.",
            "O que vive lá dentro não é exatamente monstro. É resíduo: ecos de coisas que já foram. A cidade sobreviveu mantendo as lanternas acesas, e descobriu que a luz de uma lanterna alimentada com Estilhaços fecha uma Brecha.",
            "Quem entrava para buscar Estilhaços virou caçador. Quem organizava os caçadores virou a Guilda dos Lanterneiros. E quem subisse alto o bastante, diz a lenda, faria o sol voltar de vez: o Selo Alvor.",
            "Você chegou a Lumeira com um Selo E queimado no pulso e uma lanterna velha herdada de alguém que não conheceu.",
        ]),
        ("Regras", "O Código da Véspera",
        [
            "• A classe é para sempre. Você só empunha a arma da classe escolhida no começo.",
            "• Equipamentos têm Selo mínimo e, alguns, classe. Itens que não servem para você aparecem com cadeado: guarde, venda ou desmonte.",
            "• Só é permitido entrar em Brechas de até um Selo acima do seu.",
            "• Uma Brecha que ninguém fecha a tempo rompe. Monstros invadem a Rua da Forja, Brás fecha a forja e os preços sobem 50% até a rua ser limpa.",
            "• Quem cai numa Brecha perde a Lanterna e tudo o que coletou lá dentro. A vida máxima cai 40% até alguém trazer a Lanterna de volta, ou até você mesmo buscá-la.",
            "• Partes quebradas são a única fonte de materiais raros.",
            "• O Golpe Supremo recarrega com o tempo e não funciona durante diálogos.",
        ]),
        ("Combate", "Partes, lados e fraquezas",
        [
            "Ataque com J ou com o clique. Espaço esquiva e deixa você invulnerável por um instante, mas gasta energia.",
            "Todo monstro tem partes. Algumas ficam na frente (mandíbula, isca, capuz), outras atrás (dorso, barbatanas, corrente). Quebrar uma parte solta um material específico e muda o comportamento do monstro.",
            "Golpes marcados em rosa no chão avisam o ataque: saia da área.",
            "Infusões elementais (Gaspar, na Armaria) fazem sua arma acertar fraquezas: \"Fraqueza!\" causa 50% a mais, \"Resiste\" causa 40% a menos.",
            "Cada acerto enche a Essência. Com Essência suficiente, Q (ou o botão direito) usa o Eco equipado.",
        ]),
        ("Brechas", "Bairros e estações",
        [
            "As Brechas se abrem na praça da Rua da Forja durante a véspera. O mural da Guilda mostra o bairro, a estação, o Selo e quanto tempo falta para romperem.",
            "Cada Brecha herda o bairro de onde veio: Mercado de Peixes, Cemitério das Velas, Minas da Forja, Estufa Silvestre e Catedral do Arauto, do Selo E ao S.",
            "A estação muda as regras: na primavera há baús extras, no verão os monstros queimam mais, no outono vêm espectros a mais e no inverno a água congela.",
            "Dentro, vença as ondas para abrir a passagem até o Núcleo, onde o chefe espera. Fugir pela saída é permitido: você leva o que coletou, mas a Brecha continua aberta.",
        ]),
        ("Classes", "Armas e Golpes Supremos", []), // montada a partir de ultimates.json (extra.js:175)
        ("Selos", "De E até Alvor",
        [
            "O Selo sobe com a experiência de monstros, chefes e missões: E, D, C, B, A, S, SS, SSS, SSSS e Alvor.",
            "Cada Selo aumenta a vida, libera Brechas mais difíceis e equipamentos melhores. Outros caçadores veem o seu Selo ao lado do nome.",
            "O Diário (Esc) guia a história principal. Cada missão cumprida dá recompensas e uma conversa nova.",
        ]),
        ("Cidade", "Lumeira e seus ofícios",
        [
            "Guilda: Odete, o mural de contratos e os bonecos de treino.",
            "Rua da Forja: Brás melhora sua arma e forja equipamento com partes de monstro. Lá ficam os portais das Brechas.",
            "Estalagem: Lúcia vende comida, conta boatos e deixa você dormir até a próxima véspera.",
            "Rua dos Ofícios: Gaspar (infusões elementais), Vesna (afinação de Ecos), Ivo (poções) e Abel (lanternas e relíquias).",
        ]),
    ];

    // --- Controles (CONTROLS, extra.js:178; coluna de gamepad = project.godot, DECISIONS D9) ---
    public static readonly (string Keys, string Gamepad, string Action)[] Controls =
    [
        ("WASD / Setas", "Stick esq. / D-pad", "Mover"),
        ("Mouse", "Direção do movimento", "Mirar"),
        ("J, K ou clique", "X / Y", "Atacar (Arco: segure)"),
        ("Espaço / Shift", "B", "Esquiva"),
        ("F ou V", "RB", "Golpe Supremo"),
        ("Q / botão direito", "", "Usar Eco"),
        ("R", "", "Beber poção"),
        ("E", "A", "Interagir, falar, abrir baús"),
        ("Tab / I", "Back", "Inventário"),
        ("Esc", "Start", "Menu de pausa"),
    ];

    public const string ControlsTip = "Dica: o Golpe Supremo e o Eco são independentes. Use os dois juntos nas lutas contra chefes.";

    // --- Pausa (extra.js:245-258) ---
    public const string PauseTitle = "Pausa";
    public const string PauseContinue = "Continuar";
    public const string PauseJournal = "Diário de missões";
    public const string PauseSave = "Salvar jogo";
    public const string PauseQuit = "Voltar ao menu principal";
    public const string Saved = "Jogo salvo.";

    // --- Opções (extra.js:202) ---
    public const string OptionMaster = "Volume geral";
    public const string OptionMusic = "Música";
    public const string OptionSfx = "Efeitos";
    public const string OptionUi = "Interface";
    public const string OptionFullscreen = "Tela cheia";
    public const string OptionShake = "Tremor de tela";
    public const string OptionHints = "Painel de missão no HUD";
    public const string On = "Ligado";
    public const string Off = "Desligado";
    public const string Visible = "Visível";
    public const string Hidden = "Oculto";

    // --- Diário (extra.js:236-241) ---
    public const string JournalDone = "Cumprida";
    public const string JournalUnknown = "???";

    // --- Mural (ui.js:55-79) ---
    public const string BoardTitle = "Mural de Contratos";
    public const string BoardSubtitle = "A Casa da Maré adotou a Rua da Forja: 10% de desconto em toda forja e oficina.";
    public const string BoardInvasion = " Preços +50% enquanto houver invasão.";
    public const string BoardEmpty = "Nenhuma Brecha aberta. A próxima véspera não deve demorar.";
    public const string BoardOpen = "Portal na Rua da Forja";
    public const string BoardTracked = " · rastreada";
    public const string BoardRuptured = "ROMPEU — monstros na Rua da Forja";
    public const string BoardClosed = "Fechada";
    public const string BoardClosedCount = "Brechas fechadas por você: {n}";
    public const string BoardTrack = "Rastrear";
    public const string BoardContract = "Contrato atual";
    public const string Close = "Fechar";

    // --- Morte (ui.js:252-260) ---
    public const string DeathBrechaTitle = "Sua Lanterna se apagou";
    public const string DeathCityTitle = "Você caiu na rua";
    public const string DeathBrechaText = "Ela ficou na Brecha do {bairro}, com tudo o que você coletou lá dentro. Outro caçador pode trazê-la de volta. Ou você mesmo, se voltar antes que a Brecha feche.";
    public const string DeathCityText = "Alguém vai te carregar até a estalagem.";
    public const string DeathBrechaPrompt = "Voltar à Guilda";
    public const string DeathCityPrompt = "Acordar";

    // --- HUD (render.js:233-311, extra.js:139-158) ---
    public const string HudNoLantern = "Sem Lanterna · vida máxima -40%";
    public const string HudDay = "DIA";
    public const string HudVespera = " · véspera";
    public const string HudInvasion = "Preços +50% · Forja fechada";
    public const string HudEssence = "Essência";
    public const string HudInventory = "Inventário";
    public const string HudMenu = "Menu";
    public const string HudUltimate = "Supremo";
    public const string HudBrecha = "Brecha · ";
    public const string HudRuptured = "ROMPEU — Rua da Forja";
    public const string HudInside = "você está dentro";
    public const string HudWave = "Onda {n} de {total}";
    public const string BossIntroHint = "Quebre suas partes. Cada uma muda a luta.";
    public const string RankUpTitle = "Novo Selo";
    public const string QuestDoneTitle = "Missão cumprida: ";
}
