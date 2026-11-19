# Avaliação de QA — Véspera v2 (19/11/2026)

Especialistas do Claude Code Game Studios avaliaram o jogo (branch v2). Um verificador cético conferiu cada achado no código, nos dados e nos logs. Este relatório só trata como **confirmado** o que o verificador conferiu.

## Nota geral: 6,7 de 10

| Área | Especialista | Nota |
|---|---|---|
| QA geral: bots, estabilidade, cobertura | qa-lead | 6,5 |
| Acessibilidade e controles | accessibility | 6,5 |
| Online | network | 6,0 |
| Game design (loop, ritmo, recompensa) | game-designer | 6,5 |
| Level design (mapas, arenas, travessia) | level-designer | 6,5 |
| Narrativa e texto | narrative | 7,5 |
| UX (menus, HUD, input) | ux-designer | 6,5 |
| Performance | performance | 7,0 |
| Resistência e save | qa-tester | 7,0 |
| Balanceamento numérico | systems-designer | **não entregou** |

A nota geral é a média simples das 9 notas entregues.

## Pontos fortes

- **O jogo é estável.** 15 de 17 modos de bot passaram sem erro nem exceção. Os bots longos (--final, --atos67) passaram duas vezes cada. O bot faz 528 sondas de colisão, sem nenhuma falha.
- **O save é seguro.** A gravação é atômica, com cópia de reserva (.bak). Um save ilegível cai para o .bak, e um save de versão nova é recusado sem travar o jogo.
- **A performance é boa para o PC-alvo.** O pico foi 628 MB de RAM, com 60 FPS nos trechos medidos. O renderizador é leve e sem shaders pesados.
- **O texto é bem escrito.** São 496 falas curtas, com média de 34 caracteres. Os personagens têm vozes distintas, e todo caminho de diálogo aponta para um nó que existe.
- **A escala está consistente.** As 794 medições de escala deram ok, e os pontos de nascimento caem em chão andável.
- **O remapeamento de controles é completo.** Tem 15 ações em 3 colunas e opções de conforto: tremor de tela, redução de flashes e HP em texto.
- **O protocolo online é defensivo.** Ele valida pacotes e o host decide o estado do jogo. A conexão funciona quando o host já está pronto.
- **A identidade visual é coerente.** As telas de decisão são claras: Forja, Mural, Bênçãos.

## Problemas confirmados, por prioridade

### Prioridade 1: quebram o jogo ou o modo de jogo (corrigir primeiro)

1. **No co-op local, o teclado do Jogador 2 não ataca, não esquiva e não usa poção nem Eco.** O código lê sempre a tabela de teclas do J1. Isso quebra o co-op no teclado. → #68
2. **Os controles padrão estão errados.** O Eco 2 do controle está no botão central (Guide), que o Windows captura. No J2, Eco 2 e Poção usam a mesma tecla (Num 4). → #69
3. **A arena da Velória às vezes não termina.** A sala fica "presa" e a saída não abre. Aconteceu 1 vez em 2. Se acontecer com um jogador, ele fica preso. → #73
4. **A tela de resultado da caçada sai da tela.** O botão "Voltar à Praça" fica invisível, e acontece em toda caçada. → #78

### Prioridade 2: online (não está pronto para jogar com amigos)

5. **O convidado vê só o chão, os caçadores e os monstros.** Não vê cenário, NPCs, golpes nem a própria vida. → #70
6. **O online não tem senha nem aprovação do host.** Quem descobrir o IP entra e vai para o save do host. As mensagens de erro também estão erradas: porta ocupada não mostra erro, e grupo cheio aparece como "firewall". → #71
7. **O teste automático do online falha sempre.** O roteiro é uma corrida de tempo e não prova se a rede funciona. → #72

### Prioridade 3: história e sensação de jogo

8. **No fim do Ato II sem rank C, o Ravi comemora um C que não aconteceu.** A cena também se repete a cada entrada na praça. → #76
9. **A revelação "Eu era a Marisa" pode ser perdida para sempre.** As perguntas 3 e 4 do Vigia só aparecem se o jogador morrer. → #77
10. **Os chefes ficam mais curtos a cada ato.** O chefe final morre em cerca de 8 s; o clímax é a luta mais curta do jogo. → #75
11. **Os Exames da Ordem não aplicam os desafios dos dados.** Para passar, basta matar o Guardião. → #79
12. **O caçador não ganha golpes novos em 7 atos.** O Golpe Supremo existe, mas não está ligado. As bênçãos só mexem em números. → #80
13. **Há falas que nunca tocam e falas que contradizem o ato atual.** Também há incoerências no Final: a mãe do Ravi, a promessa do Ravi e o peixe da Nilza. → #81

### Prioridade 4: acabamento e manutenção

14. **A interface tem problemas de legibilidade e cortes.** O HUD não tem fundo. O texto grande não vale no combate. O título corta o "Sair". O inventário corta os nomes. Os Ecos só se distinguem pela cor. A Ajuda está incompleta. → #82
15. **O CI só roda o teste do menu.** Os bots de jogo não rodam no CI. Os bots lutam com só 6 dos 8 Guardiões, e as capturas de escala estão repetidas. → #74
16. **O instalador leva 417 MB de imagens que o jogo não usa.** → #83
17. **A arena do Ato I é a menor do jogo.** Precisa de medição em jogo. → #84

**Por que nesta ordem:** primeiro o que impede de jogar (co-op no teclado, controle, arena presa, botão sumido). Depois o online, que hoje não está pronto para jogar com amigos. Por último, o que deixa o jogo pior mas não o impede.

### Achados que o verificador derrubou (não viraram issue)

- **DPS desigual entre as classes:** a diferença só existe no rank E. Depois disso fica entre −8% e +10%.
- **Fragmentos sobrando e Ato I repetitivo:** o log citado não existe, e as contas não batem.
- **Mapas grandes e travessias longas** (Ruas Baixas, Praça, Forjas, corredores, salas da dupla): os números não confirmam o problema, ou o limite usado foi inventado.
- **"Nenhum bot tem curva de memória"** (qa-tester): existem 12 CSVs de memória na pasta.
- **As 7 saídas "em breve"** existem, mas foram rebaixadas para gravidade baixa: o jogo avisa que estão trancadas.

## O que ficou sem teste

- **Balanceamento numérico:** o systems-designer não entregou a tempo. O game-designer cobriu só parte, com o DPS das classes, o tempo dos chefes e os Fragmentos.
- **Vazamento de memória em sessão longa:** a medição do qa-tester falhou com a máquina carregada. Não dá para afirmar que não há vazamento.
- **Save corrompido ou editado:** o qa-tester só analisou o código e não rodou o jogo com esse save. Ficaram duas hipóteses de gravidade baixa: o .bak pode ser sobrescrito por um save ruim, e valores absurdos no save podem não ser corrigidos.
- **Online pela internet:** só foi testado na mesma máquina (127.0.0.1).
- **Jogo com controle físico, co-op com 2 pessoas reais e daltonismo:** foi tudo análise de código e de capturas.
- **Ruas Baixas e Avesso:** as capturas desses mapas estão repetidas, então não foram vistos em imagem.

## Próximos passos

1. Corrigir #68, #69 e #78. São correções pequenas e destravam o co-op e o fim de caçada.
2. Investigar #73 (arena presa) com mais rodadas do --ato45 e um limite de segurança na saída.
3. Decidir se o online entra nesta versão. Se entrar: #70, #71 e #72. Se não entrar, esconder o botão Online.
4. Corrigir os diálogos (#76, #77). É mexida só em dados e tem baixo risco.
5. Rodar o systems-designer e refazer a medição de memória antes de mexer no HP dos chefes (#75).
6. Colocar os bots curtos no CI (#74).
