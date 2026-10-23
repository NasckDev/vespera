# Véspera — Bíblia da Campanha, Parte 1 ("até o Alvor")

Escrito em 23/10/2026 a pedido do dono: *"planeja ato 3 4 5 6 quantos precisar, bota fazer esse jogo pra valer"*.
Este documento é a fonte da **história** da Parte 1. Complementa o `PLANO-V2.md` (§5 campanha, §6 progressão, §7 classes)
e o `docs/design/GDD.md` (mundo, bestiário). Onde a história daqui conflitar com o GDD, **esta bíblia vence**; onde
conflitar com regra técnica do PLANO, o PLANO vence.

O que já existe e foi respeitado: o tom e as falas do Prólogo e do Ato I (branch `jogo-ato1`: a lanterna era da Marisa,
pisca três vezes e "alguém do outro lado está chamando"; a Sopa da Véspera da avó da Nilza; o exame E do Benício), o Ato II
das issues #26–#28 (Dona Celeste, Pomar Podre, Carrapicho, Rato-Lanterna, Macieira Oca) e os segredos do GDD §3.

---

## 0. Como usar este documento

- **Roteirista:** leia §1 a §4 antes de escrever qualquer fala. A §2 (A Verdade) nunca aparece inteira no jogo antes do Final.
- **Game designer:** cada ato em §6 traz salas, monstros (verbo + aviso), Guardião em 2 fases, recompensas e duração.
- **Artista:** cada ato lista hub, salas e arena com o que desenhar; as issues `arte-v2` saem de `tools/art/prompts_ato*.py`.
- **Dev:** o Ato III está roteirizado em `data/dialogue/ato3/` (ver `data/dialogue/ato3/LEIA-ME.md`).

---

## 1. A história em uma página

Há vinte anos, na noite da **Festa das Lanternas**, uma tempestade que Lumeira chama de **Maré Grande** engoliu o porto.
O farol estava apagado. Quarenta e três pessoas não voltaram do mar. O sineiro da cidade tocou o sino a noite inteira
chamando os barcos. Quando amanheceu, ele não teve coragem de tocar a alvorada — tocar a alvorada era dizer que a noite
tinha acabado e que os barcos não vinham mais. **O sino ficou mudo. E o dia não veio.** Foi o **Ano Sem Alvor**.

Lumeira acredita que o sol voltou sozinho. Não voltou: alguém pagou por ele. E a conta nunca foi fechada. Toda véspera,
o "ontem" que a cidade não soltou se rasga no ar — as **Brechas** — e lá dentro está o **Avesso**: cada bairro **do jeito
que era antes do Ano Sem Alvor**, apodrecido de tanto ser guardado. Os monstros são ecos de coisas e pessoas que a cidade
se recusa a deixar ir.

O jogador chega de fora, com uma lanterna velha "herdada de alguém que nunca conheceu" e um selo E queimado no pulso.
Subindo de rank, Brecha por Brecha, ele conhece as pessoas que seguram esse ontem — uma ferreira que perdeu a parceira,
um faroleiro que deixou a luz apagar, um mestre que calou o sino — e descobre por que a lanterna dele pisca três vezes
na direção do Avesso. No fim, na Brecha que se abre no meio da praça, ele enfrenta **o Reflexo**: o próprio ontem que
construiu em Lumeira. Alguém toca o sino de novo. Amanhece de verdade. **Rank Alvor.**

**Tema:** luto. O que acontece com uma cidade (e com uma pessoa) que não deixa o dia acabar.
**Pergunta da Parte 1:** *quanto custa deixar amanhecer?*
**Resposta que o jogo dá:** soltar o ontem não é esquecer; é parar de viver dentro dele.

---

## 2. A Verdade (só para quem escreve)

Tudo o que o jogador descobre aponta para estes fatos. Nenhuma fala pode contradizê-los.

1. **O sino vira o dia.** Lumeira foi fundada em volta de um sino fundido com uma pedra que caiu do céu. Desde sempre, a
   badalada da véspera fecha o dia e a da alvorada abre o seguinte. Ninguém leva isso a sério; é "coisa de avó".
2. **A noite da Maré Grande** (Festa das Lanternas, há 20 anos). **Severo**, o faroleiro, deixou o farol para ouvir a filha,
   **Iara**, de 12 anos, cantar na festa pela primeira vez. A tempestade chegou rápido. Sem farol, 43 pessoas morreram no
   mar — entre elas **Tomé**, 16 anos, filho do sineiro. O sineiro era **Benício**. Ele tocou o sino a noite toda para guiar
   os barcos pelo som. Ao amanhecer, não tocou a alvorada. O sino emudeceu. Começou o Ano Sem Alvor.
3. **O Avesso é o ontem.** No escuro, o luto da cidade inteira guardou a véspera da tragédia. É por isso que cada Brecha
   copia o bairro **como era antes**: o pomar ainda dando frutos, o cais em festa, as casas que ainda não tinham sido
   demolidas. Coisa guardada demais apodrece: daí os monstros.
4. **Marisa Duarte**, caçadora rank A, parceira de caça e de vida da **Odete**, descobriu isso no fim do Ano Sem Alvor. Entrou
   de propósito na grande Brecha que se abriu sobre o mar, no barco emprestado de Severo, e **ficou do lado de lá
   segurando a porta**. Na manhã seguinte o sol nasceu. A lanterna dela voltou sozinha, acesa, encalhada no cais. Severo a
   entregou à Odete, que nunca soube que Marisa foi por escolha. Odete deixou a lanterna na Ordem "para quem precisasse".
5. **A porta foi segurada, não fechada.** Por isso as Brechas abrem toda véspera. Marisa virou **o Vigia** do Cemitério das
   Lanternas: metade gente, metade eco. Depois de 20 anos ela está cansando — e **o Avesso está crescendo**.
6. **Os Fragmentos são ontem cristalizado.** As lanternas da cidade queimam Fragmentos: Lumeira literalmente vive do próprio
   luto. A Ordem sabe. O tesoureiro **Aurélio Lacerda** acha que fechar o Avesso de vez mata a cidade de frio e fome (sem
   Fragmento, sem luz, sem comércio). Ele não é vilão de capa: é o medo do futuro com cargo e assinatura.
7. **Só quem não tem ontem em Lumeira atravessa o Avesso sem ser copiado.** Benício leu isso no arquivo da Ordem. Há anos
   ele manda a lanterna da Marisa, pelo correio, para pessoas de fora da cidade, com a mesma carta: *"Herança de Marisa D.,
   para quem precisar."* Dezessete vezes ela voltou apagada. Na décima oitava, **o jogador disse sim** — e o selo queimou.
   O jogador não foi escolhido pelo destino; foi o primeiro que veio.
8. **A Lis é um eco.** Ela "nasceu no ano em que o sol sumiu" (o pano de festa bordado em que foi achada traz a data), mas
   foi encontrada pela Nilza, recém-nascida, debaixo da grande lanterna **há onze anos**. É o eco de um bebê perdido no Ano
   Sem Alvor — por isso o Avesso a ignora e ela sente as Brechas. A **Dona Celeste** também é eco (a original morreu de frio
   no Ano Sem Alvor). A regra, que o jogo diz com estas palavras: **"O Avesso só devolve quem é amado hoje."**
9. **O Reflexo** é o ontem do próprio jogador. Quando chega, o jogador não tinha passado em Lumeira; ao fim da Parte 1 tem
   amigos, dívidas, Lanternas caídas. Agora o Avesso consegue copiá-lo. É a última defesa do ontem.
10. **O final.** Derrotado o Reflexo, alguém precisa tocar a alvorada com o Badalo recuperado: **Benício** (expiação),
    **Lis** (o eco que escolhe o hoje — e volta a crescer) ou **o jogador** (o forasteiro que fica). Marisa solta a porta e
    descansa. Celeste e Lis ficam, porque são amadas hoje. Amanhece. Do outro lado do mar, **outro sino responde**: Lumeira
    não foi a única cidade a ter um Ano Sem Alvor (gancho da Parte 2).

---

## 3. O mistério, ato a ato (o que o jogador descobre)

Regra de ouro: **cada ato responde uma pergunta antiga e abre uma nova**, e toda resposta vem de uma pessoa, não de um
texto solto. O jogador nunca deve sentir que "ficou faltando explicação" no fim do Final.

| Ato | O que o jogador descobre | Quem conta / como | Nova pergunta | Paga em |
|---|---|---|---|---|
| Prólogo | Houve um Ano Sem Alvor; Brechas abrem toda véspera; você tem selo E e uma lanterna herdada | Narração, Benício | Por que o selo queimou em mim? | Ato V |
| I | A lanterna era da **Marisa**, parceira da Odete, que nunca voltou. Ela pisca três vezes na direção do próximo bairro | Odete, cena do fim | Quem está chamando do outro lado? | Ato IV |
| II | A Brecha é o bairro **como era antes**; o Avesso guarda o ontem. Celeste conhece coisas de 20 anos atrás "como se fosse ontem" | Celeste, a própria Brecha | O que aconteceu na véspera do Ano Sem Alvor? | Ato III |
| III | A **Maré Grande**: o farol apagado, 43 mortos, o sino tocando a noite toda. Marisa entrou na Brecha do mar **de propósito**, e o sol voltou na manhã seguinte | Severo, Iara, Odete, o Farol | Para onde Marisa foi? Por que o sol voltou? | Ato IV |
| IV | O Vigia é **Marisa**. Ela segura a porta do Avesso há 20 anos e está cansando: por isso o Avesso cresce. "Pergunte ao homem do sino por que ele parou" | Vigia, Velória, resgate de Ravi | Por que o sino parou? | Ato V |
| V | **Benício** calou o sino: não tocou a alvorada porque Tomé estava no mar. E foi ele quem mandou a lanterna ao jogador (18ª carta) | Benício, Sineiro Mudo, Lacerda | Dá para tocar de novo? O que a cidade perde se o Avesso fechar? | Ato VI |
| VI | A cidade **vive** do luto: Fragmentos são ontem, as lanternas queimam memória, e os pobres pagaram a conta (demolições). Lacerda quer o Avesso aberto | Nilza, Ravi, Inquilino, Lacerda | Se o Avesso fechar, o que some com ele? | Ato VII |
| VII | **Lis e Celeste são ecos.** "O Avesso só devolve quem é amado hoje." O coração do Avesso é a Praça na noite da festa | Celeste, Lis, Marisa | Quem toca o sino? | Final |
| Final | O Reflexo é o seu próprio ontem. O sino toca. Amanhece. Do outro lado do mar, outro sino responde | Todos | (Parte 2) | — |

**Sementes que precisam ser plantadas cedo** (para o pagamento parecer justo):
- Prólogo/I: o sino da Torre aparece mudo no fundo da praça; Benício nunca olha para ele. A lanterna pisca três vezes.
- I: Nilza diz que a avó fazia a Sopa da Véspera "no ano em que o sol sumiu". Lis diz que nasceu nesse ano (e tem 11).
- II: no Pomar Podre há bandeirolas da Festa das Lanternas presas nas macieiras (a festa de 20 anos atrás).
- III: o muro do quebra-mar tem 43 nomes; um deles é **Tomé B.**; Benício para diante dele.
- IV: Lis não aparece no reflexo das poças do cemitério (só quem presta atenção vê).

---

## 4. Elenco

Cada personagem tem uma **ferida** (o ontem que não solta), um **desejo** (o que pede ao jogador) e uma **virada** (o
momento em que solta). Ninguém é infantilizado: até a Lis tem razões de adulto para mentir.

| Personagem | Quem é | Ferida | Desejo | Virada (ato) |
|---|---|---|---|---|
| **Jogador** ({nome}) | forasteiro, selo E, lanterna da Marisa | não tem passado em Lumeira | ver até onde aguenta | Final: decide se fica |
| **Mestre Benício** (~70) | chefe da Ordem, cego de um olho, sabe o nome de todos os mortos | calou o sino; Tomé morreu no mar | que alguém faça o que ele não fez | V: confessa; Final: talvez toque |
| **Odete** (50 e poucos) | ferreira, ex-caçadora rank A | Marisa "morreu à toa" | não perder mais ninguém pela porta | III: sabe que Marisa escolheu; VII: volta a lutar |
| **Ravi** (18) | rival selo E, Ruas Baixas | família despejada; vergonha de ser pobre | chegar ao Alvor e tirar a família de lá | IV: quase morre; VI: solta a casa velha |
| **Lis** (parece 11) | batedora, vende mapas | não sabe de onde veio | que ninguém a mande embora | VII: descobre que é eco; Final: escolhe o hoje |
| **Tia Nilza** | taverneira, Ruas Baixas, cuida de todos | cozinha com Fragmentos e sabe que não deveria | que a sopa nunca falte | VI: para de usar Fragmento; ensina a Sopa de verdade |
| **Dona Celeste** | herbalista do Pomar Alto | é eco; a original morreu de frio | ser amada sem precisar explicar | II: semeia; VII: confessa |
| **Capitão Severo** | mestre do cais, ex-faroleiro | deixou o farol apagado | que ninguém mais entre no "cais dele" | III: confessa e reacende o farol |
| **Iara** (~32) | filha de Severo, peixeira | acha que a culpa foi dela (cantou) | que o pai fale com ela | III: canta de novo no farol |
| **O Vigia / Marisa** | encapuzado do cemitério, faz perguntas | segura a porta há 20 anos | que alguém termine o que ela começou | IV: se revela; Final: descansa |
| **Aurélio Lacerda** | tesoureiro da Ordem, controla o comércio de Fragmentos | perdeu a fé no futuro, não pessoas | manter a cidade aquecida, custe o que custar | VII: entrega o Badalo (ou não) |

**Vozes (para manter consistência):**
- Benício: frases completas, formais, nunca contrações. Diz "{nome}". Quando mente, fica curto.
- Odete: frases de 3–6 palavras. Nunca elogia direto. Ironia seca. Usa "inteiro" para "vivo".
- Ravi: rápido, exagera números, faz apostas, chama o jogador de "parceiro" a partir do Ato III.
- Lis: perguntas, observações literais, "sabia?". Mente mal. Desenha tudo.
- Nilza: "menino/menina" vira "meu bem" (neutro), comida como resposta para tudo.
- Celeste: fala com as plantas, responde pergunta com pergunta, nunca diz "eu lembro"; diz "eu sei".
- Severo: marinheiro bruto, metáforas de mar, xinga sem palavrão ("raios", "maré podre"). Não diz o nome do Tomé até o fim do Ato III.
- Iara: direta, irônica, cansada. Chama o pai de "Capitão", nunca de "pai" — até o fim do Ato III.
- Vigia: só perguntas. Lembra as respostas do jogador. A partir do Ato IV, às vezes responde com uma frase da Odete.
- Lacerda: educado, números, "a cidade", "responsabilidade". Nunca grita.

---

## 5. Estrutura da Parte 1

Duração-alvo total: **~4h a 5h** de campanha (sem contar caçadas livres). Cada ato tem: hub (bairro de Lumeira) →
conversa que dá o motivo → 1–2 caçadas na Brecha com um pedido do NPC → exame quando houver → Guardião → cena de fim
com resposta + gancho.

| Ato | Nome | Rank (entra → sai) | Bairro → Brecha | Guardião | Eco | Exame | Estágio / habilidade nova | Duração |
|---|---|---|---|---|---|---|---|---|
| Prólogo | A Lanterna Herdada | E | Praça | — | — | — | Estágio 1 (aprendiz) | 5–8 min |
| I | O Que a Cidade Jogou Fora | E → D | Forjas → Esgotos do Mercado | Lodo-Mãe | Viscoso | Exame E (do Benício) | habilidade 1: investida / rolar e atirar / tremor | 25–35 min |
| II | Frutos de Ontem | D → (XP de C) | Pomar Alto → Pomar Podre | Macieira Oca | Semente | — | conjunto Casca de Macieira | 20–30 min |
| III | A Maré que Não Voltou | C → B | Mercado das Marés → Cais Afogado | Farol Afogado | Farol | **Exame C** | **Estágio 2 (caçador)**, habilidade 2: aparar / flecha de luz / escudo de bronze | 25–35 min |
| IV | Quem Vela os Vivos | B → (XP de A) | Cemitério das Lanternas → Cemitério Invertido | Velória | Vela | — | **Lanterna ativa** (cone de luz, dado pelo Vigia) | 25–35 min |
| V | O Sino que Calou | A → S | Torre do Sino → Torre do Sino Mudo | Sineiro Mudo | Sino | **Exame A** | **Estágio 3 (veterano)**, habilidade 3: lâmina de luz / chuva de flechas / martelada de âmbar | 25–35 min |
| VI | A Casa que Andava | S → SS | Ruas Baixas → Ruas Invertidas | Inquilino | Lar | **Exame S** | Sopa da Véspera (2ª vida); **Estágio 4 (elite)** ao virar SS, habilidade 4 | 25–35 min |
| VII | A Noite Mais Longa | SS → SSS | Avesso Profundo (bairros misturados) | Ecos Maiores (3 Guardiões fundidos) | — | **Exame SSS** | arma lendária **de Duas Vésperas** (Odete) | 30–40 min |
| Final | Alvorada | SSS → **Alvor** | Praça da Lanterna → a Praça na noite da festa | **O Reflexo** | Avesso | — | **Estágio 5 (radiante)**, habilidade 5: Lâmina de Duas Vésperas / feixe do Alvor / queda do sino | 20–30 min |

**Portão do Alvor** (PLANO §5): para abrir a Brecha da praça é preciso SSS, um conjunto de armadura de Guardião com 4
peças e 2 Ecos equipados.

**Ritmo de cada ato (molde):**
1. *Chegada* (2–4 min): o hub mostra o problema sem explicar (narrativa ambiental, à la Hyper Light Drifter).
2. *O pedido* (2–3 min): o NPC central pede algo pessoal, não "mate o chefe".
3. *Caçadas* (10–20 min): 1–2 descidas à Brecha; entre elas, a conversa **muda** conforme o que aconteceu (morreu, quebrou
   parte, achou Memória) — à la Hades.
4. *O portão* (1–2 min): exame ou condição do NPC; a Brecha fica vermelha.
5. *Guardião* (4–8 min): 2 fases; a fase 2 sempre conta um pedaço da história com mecânica, não com texto.
6. *Fim* (2–4 min): resposta + gancho; a lanterna aponta o próximo bairro.

**Memórias** (colecionáveis de história, 3 por Brecha): objetos do ontem que o jogador acha nas salas (brilho creme, como
partes quebráveis). Cada uma abre uma fala curta e extra com o NPC do ato. Inspiração: os vislumbres de família de
Children of Morta. Dão Lumes e +1 de relação; nunca são obrigatórias.

---

## 6. Os atos

### Prólogo — A Lanterna Herdada (E · Praça · 5–8 min) — *já implementado*

- **Tema:** chegar num lugar que não é seu. **Pergunta:** *por que eu?*
- **Cenas:** a praça na véspera; a Brecha se rasga; Benício chama "você de lanterna na mão"; Ravi propõe a aposta; Odete dá
  a arma. O sino da Torre aparece mudo ao fundo (semente).
- **Gancho:** a Brecha da praça leva aos Esgotos.

### Ato I — O Que a Cidade Jogou Fora (E → D · Forjas → Esgotos do Mercado · 25–35 min) — *já implementado*

- **Tema:** o que a gente descarta não some. **Pergunta:** *dá para confiar em quem não confia em ninguém?* (Odete)
- **Brecha:** os esgotos de 20 anos atrás, cheios de louça e brinquedos da festa. Salas: corredor, cruzamento, tesouro, arena.
- **Monstros:** Lodo (*pula*), Espectro (*persegue*). **Guardião:** Lodo-Mãe (núcleos, engolir, dividir).
- **Exame E** (Benício): duas caçadas, arma forjada, XP.
- **Cenas-chave:** Odete reconhece a lanterna da Marisa; a lanterna pisca três vezes para o leste; Benício: *"Lanternas não
  fazem isso. A não ser que alguém do outro lado esteja chamando."*
- **Recompensa:** rank D, Eco Viscoso, conjunto Gel de Lodo.
- **Gancho:** carta da Dona Celeste — as macieiras apodrecem por dentro.

### Ato II — Frutos de Ontem (D · Pomar Alto → Pomar Podre · 20–30 min) — *arte nas issues #26–#28*

- **Tema:** guardar algo bom até apodrecer. **Pergunta dramática:** *o que a Celeste está escondendo?*
- **Hub — Pomar Alto:** colina de macieiras e hortas; a estufa da Celeste; a única macieira que dá fruto fora de estação.
  Os moradores colhem maçãs que, por dentro, estão roxas.
- **Brecha — Pomar Podre:** o pomar **como era antes** — cheio, em flor, e podre. Bandeirolas da Festa das Lanternas presas
  nos galhos (semente do Ato III). Salas: corredor, cruzamento com poço seco, clareira de descanso, arena de raízes.
- **Monstros:**
  - **Carrapicho** (D) — *agarra*: raízes prendem o jogador 1 s. Aviso: o botão se abre e brilha âmbar 0,6 s. Partes: caule, bulbo.
  - **Rato-Lanterna** (D) — *enxameia*: 4–6 juntos, fogem do cone da lanterna. Aviso: agacham e a barriga acende 0,4 s antes do bote.
- **Guardião — Macieira Oca** (2 fases):
  1. *A Colheita:* lança maçãs que explodem (sombra no chão 0,8 s) e bate os galhos. Partes: 2 galhos (cada um quebrado = menos maçãs).
  2. *Raiz Funda* (abaixo de 50%): arranca-se do chão e anda; raízes brotam onde o jogador estava há 1 s (círculo âmbar). O rosto de casca fica exposto quando ela tropeça.
- **NPC central — Dona Celeste:** cordial e distante. Pede "três sementes do pomar de antes" (Memórias da Brecha) para
  replantar. Ao recebê-las: *"Estas são do ano em que eu… do ano em que o pomar ainda era bom."* Ela corrige a frase.
- **Cenas-chave:** (1) a maçã roxa partida ao meio; (2) Celeste reconhece a luz da lanterna: *"Essa luz já passou por aqui"*;
  (3) a Macieira, ao cair, solta uma bandeirola da Festa das Lanternas com uma data de 20 anos atrás; (4) o Eco da Semente
  mostra um lampejo: chuva, um sino tocando sem parar, um farol escuro.
- **Recompensa:** Eco da Semente, conjunto Casca de Macieira, XP para C.
- **Gancho:** a lanterna se inclina para o mar; Lis sente "o frio vindo da água"; Benício anuncia o **Exame C**, no cais.

### Ato III — A Maré que Não Voltou (C → B · Mercado das Marés → Cais Afogado · 25–35 min) — *roteirizado*

> Roteiro jogável completo em `data/dialogue/ato3/`. Arte em `tools/art/prompts_ato3.py` → `docs/v2/prompts/ato3_*.md`.

- **Tema:** culpa. **Pergunta dramática:** *dá para acender de novo o que se deixou apagar?*
- **Hub — Mercado das Marés:** cais de madeira na névoa da véspera, barcos amarrados, a banca de peixe da **Iara**, a
  taverna do porto, o galpão de barcos do Severo. No alto da ponta de pedra, **o farol apagado há 20 anos**. No quebra-mar,
  o **Muro dos Nomes**: 43 nomes riscados na pedra (um deles, *Tomé B.*).
- **Brecha — Cais Afogado:** o porto **na noite da Festa das Lanternas, há 20 anos, no minuto em que a maré subiu**.
  Bandeirolas e lanternas de papel boiando apagadas, barcos de cabeça para baixo pendurados no céu, chuva que cai para
  cima, o farol sempre escuro ao fundo.
- **Salas:**
  1. **Píer das Bandeirolas** (corredor): tábuas alagadas, bandeirolas da festa, poças que sobem e descem. Ensina o Afogado.
  2. **Mercado Submerso** (cruzamento): bancas de peixe tombadas, peixes parados no ar, redes; Lanternas-Afogadas acima.
  3. **Barco Virado** (descanso): dentro do casco virado de um barco de pesca; poste de lanterna para descansar; Memória "a faca do Tomé".
  4. **Quebra-mar dos Postes** (tesouro/elite): o quebra-mar com 43 postes de lanterna apagados (onde no hoje estão os 43
     nomes); baú no fim; elite **Afogado-Capitão**.
  5. **Arena — Pé do Farol:** plataforma de pedra redonda na base do farol, mar revolto em volta, o farol enorme ao fundo.
- **Monstros:**
  - **Afogado** (C) — *avança*: lento, pesado, dano alto, agarra quem fica na frente. Aviso: para de pingar, ergue os dois
    braços, o capacete de escafandro brilha âmbar 0,7 s → pancada dupla com espirro de água à frente. Partes: capacete
    (sem ele, fica cego e anda em linha reta), corda (sem ela, não agarra) → *Bronze Salgado*, *Corda Encharcada*.
    Fraqueza: dê a volta; costas levam dano dobrado. Eco de: marinheiros que a maré não devolveu.
  - **Lanterna-Afogada** (C) — *derrama*: lanterna de papel da festa, cheia de água do mar, flutua acima do jogador e
    despeja um círculo de água. Aviso: a sombra circular no chão escurece e a vela de dentro brilha âmbar 0,8 s. Frágil
    (2 golpes), mas foge e se esconde atrás dos Afogados. Parte: casca de papel → *Papel de Festa*. Eco de: as lanternas
    que soltaram para guiar os barcos.
  - **Ancoreiro** (C) — *ancora*: caranguejo grande com uma âncora no lugar de uma garra. Arremessa a âncora presa numa
    corrente em linha reta; se acerta, **puxa** o jogador até ele. Aviso: a corrente chacoalha e uma linha âmbar marca a
    trajetória 0,6 s. Parte: garra-âncora → *Ferro de Âncora* (sem ela, só belisca). Eco de: as âncoras que não seguraram.
- **Exame C — "Os Postes do Quebra-mar"** (Benício, Brecha da Ordem sem loot): três salas escuras; acender os três postes de
  sinal enquanto Afogados avançam. Não é preciso matar todos: testa movimento e prioridade, não equipamento. Ravi faz o
  exame junto (aliado de IA). Na terceira sala ele trava na água (não sabe nadar). **Escolha:** voltar para buscá-lo (ele
  passa junto) ou seguir (ele reprova e fica um rank atrás). Flag `ravi_exame_c_ajudado` muda o motivo do Ato IV.
- **Guardião — O Farol Afogado** (2 fases):
  1. **O Feixe:** faroleiro de bronze de 3 m, a lanterna do farol no lugar da cabeça. O feixe gira pela arena como um
     farol; quem é tocado é **puxado** até ele e leva uma pisada. Aviso: a lente brilha âmbar e uma linha fina mostra o
     caminho do feixe 0,6 s antes. Também chama 2 Afogados por vez. Partes: **lente** (quebrada, acaba o puxão) e **2
     ombreiras** (cada uma quebrada = menos Afogados).
  2. **A Maré Grande** (abaixo de 50%): a arena alaga (anel externo deixa lento), **a luz apaga**. Só a lanterna do jogador
     ilumina. O Farol ataca do escuro; o aviso vira **som** (buzina de névoa) + o brilho fraco da lente. Na escuridão,
     **uma segunda lanterna** acende longe e pisca três vezes no ponto onde o Farol vai surgir — alguém está ajudando
     (Marisa; o jogador só entende no Ato IV).
- **Morte do Guardião (cena sem texto):** o Farol ajoelha; a cabeça-lanterna acende de novo; os Afogados param, viram para
  a luz e caminham até ela, sumindo um a um. Na lente quebrada fica presa uma **placa de selo: "Marisa D. — A"**.
- **NPC central — Capitão Severo:** começa expulsando o jogador do cais. Respeita quem volta da Brecha. Pede, sem dizer
  por quê, que o jogador traga o **livro de bordo do farol** (Memória). Ao lê-lo, confessa: deixou o farol para ouvir a
  filha cantar. No fim, **reacende o farol de verdade** pela primeira vez em 20 anos.
- **NPC de apoio — Iara:** peixeira, filha de Severo, mora a dez metros do pai e não fala com ele. Acha que a culpa foi
  dela: "Eu pedi pra ele ir." Memória dela: o **programa da festa** com o nome dela na lista de cantoras.
- **Cenas-chave:**
  1. *O Muro dos Nomes* — o jogador lê os nomes; Benício chega para o exame e para diante de "Tomé B." sem dizer nada.
  2. *Exame C* — Ravi na água; a escolha.
  3. *A lanterna da Marisa* — Severo vê a lanterna do jogador: "Fui eu que tirei essa lanterna da água."
  4. *O livro de bordo* — Severo lê a última linha que escreveu ("Festa. Volto às oito.") e confessa.
  5. *O farol aceso* — no fim, no hoje: Severo sobe o farol, acende; Iara canta a canção que não cantou inteira há 20 anos;
     Odete chega e ouve de Severo que Marisa **pediu o barco** e foi de propósito. Odete conta o código das duas: *"Três
     piscadas queria dizer 'estou voltando'."*
- **Recompensa:** passou no Exame C → **Estágio 2 (caçador)**: visual novo (casaco de caçador, ombreira de couro) e
  habilidade 2 da classe. Derrotar o Farol → **rank B**, **Eco do Farol** (feixe que puxa inimigos para um ponto; combina
  com o Viscoso), conjunto **Bronze Salgado** (30% por peça).
- **Gancho:** a lanterna pisca três vezes para o morro do **Cemitério das Lanternas**. Lá em cima, uma luz violeta pisca
  três vezes de volta.

### Ato IV — Quem Vela os Vivos (B · Cemitério das Lanternas → Cemitério Invertido · 25–35 min)

- **Tema:** esperar por quem não volta. **Pergunta dramática:** *quem é o Vigia — e o Ravi volta?*
- **Hub — Cemitério das Lanternas:** colina de túmulos, cada um com uma lanterna acesa (os lumeirenses pagam à Ordem para
  manter a lanterna dos seus mortos — a primeira aparição de **Lacerda**, vendendo "perpetuidade de chama"). No topo, a
  capela e o banco onde **o Vigia** aparece ao anoitecer.
- **Brecha — Cemitério Invertido:** o cemitério **antes** do Ano Sem Alvor: pequeno, quase vazio, gramado — e de cabeça para
  baixo: túmulos pendurados no céu, raízes de cipreste para cima, velas que pingam cera para o alto. Quanto mais fundo,
  mais túmulos aparecem: o cemitério crescendo com os anos (o Avesso crescendo).
- **Salas:** (1) **Alameda dos Ciprestes** (corredor); (2) **Ossuário das Velas** (cruzamento, poças de cera); (3) **Capela
  Virada** (descanso, bancos no teto); (4) **Fosso dos Sinos de Defunto** (evento: o resgate de Ravi, ver abaixo);
  (5) **Arena — Altar das Mil Velas**.
- **Monstros:**
  - **Velinha** (B) — *cura*: vela viva que cura outros monstros e foge. Aviso: a chama cresce âmbar 0,8 s antes de soltar
    o pulso de cura (interrompível com um golpe). Parte: pavio → *Pavio Eterno*. Eco de: velas acesas por quem esperava.
  - **Carpideira** (B) — *lamenta*: figura de véu longo; grito em cone que empurra e silencia (sem habilidade por 2 s).
    Aviso: o véu sobe e o rosto vazio brilha âmbar 0,7 s; o cone aparece no chão. Parte: véu → *Renda de Luto*.
    Eco de: quem chorou por encomenda.
  - **Cão-de-Vigília** (B) — *investe*: cão magro de pelagem de cinza. Uiva (aviso âmbar 0,6 s, linha no chão) e corre em
    linha reta; se bate em túmulo, fica atordoado 1,5 s. Parte: coleira → *Coleira com Nome*. Eco de: cães que esperaram
    os donos no túmulo.
- **Evento — Ravi:** se o jogador demorar a descer (ou se Ravi reprovou no Exame C), Ravi entra sozinho na Brecha B para
  "alcançar" o jogador. Na sala 4 o jogador acha a **lanterna caída do Ravi** e o segue até uma sala-cerco (ondas de
  Velinhas e Cães) onde ele está ferido, preso numa cova. Sobreviver 60 s e quebrar as correntes. Versão do GDD
  ("Ravi morre se a Brecha não for limpa a tempo") fica como **modo Difícil**: no Normal, o resgate sempre acontece.
- **Guardião — A Velória** (2 fases):
  1. *A Vigília:* colosso de cera com centenas de chamas. Atira velas que viram Velinhas. Aviso: os 3 candelabros dos
     ombros brilham âmbar. Partes: **3 candelabros** (cada um quebrado = uma Velinha a menos por onda).
  2. *Todas Menos Uma* (abaixo de 50%): derrete numa poça de cera que queima e apaga **todas as velas da arena menos
     cinco**; uma delas é o coração. Acertar a certa encerra. A certa é a que **não treme** quando a lanterna do jogador
     passa (a única vela que não está esperando ninguém).
- **NPC central — O Vigia (Marisa):** cada visita faz **uma pergunta** ("O que você deixou para trás quando veio?", "Se
  pudesse trazer alguém de volta, traria?", "Você ainda volta inteiro?" — esta última, frase da Odete). Lembra as
  respostas (flags). Depois da Velória, abaixa o capuz: rosto de mulher de 40 anos, metade dele translúcido como vidro.
  Revela: segurou a porta por 20 anos e está cansando — por isso o Avesso cresce. Dá ao jogador o **acender da lanterna**
  (cone de luz: revela, dissipa Espectros, aplica fraquezas).
- **Cenas-chave:**
  1. Lacerda vendendo "chama perpétua" a uma viúva; o jogador pode pagar a chama dela (Lumes) — flag `pagou_chama_viuva`.
  2. A lanterna caída do Ravi no chão da Brecha (a mecânica de Lanterna vira história).
  3. Ravi ferido conta seu segredo: ele **ouve** as Brechas, como a Lis. "Achei que era defeito."
  4. O Vigia abaixa o capuz. *"Diga à Odete que eu ainda volto. Só não sei se inteira."*
  5. Marisa: *"Pergunte ao homem do sino por que ele parou."*
- **Recompensa:** Eco da Vela (cura 6 s, rank um nível abaixo durante o efeito), conjunto **Cera Velada**, lanterna ativa,
  XP para A.
- **Gancho:** Odete, ao saber, não diz nada; à noite sobe ao cemitério e não encontra ninguém. Benício convoca o **Exame A**
  na Torre do Sino.

### Ato V — O Sino que Calou (A → S · Torre do Sino → Torre do Sino Mudo · 25–35 min)

- **Tema:** quem decide quando o luto acaba. **Pergunta dramática:** *Benício merece perdão?*
- **Hub — Torre do Sino:** sede da Ordem: pátio de treino, arquivo com 20 anos de nomes, o bestiário, o campanário com o
  sino enorme e mudo (sem badalo). Lacerda tem sala com cofre de Fragmentos.
- **Brecha — Torre do Sino Mudo:** a torre **na madrugada depois da Maré Grande**: a escada em espiral infinita, cordas de
  sino que sobem até o escuro, os papéis do arquivo voando sem gravidade, e lá em cima um sino que balança sem som.
- **Salas:** (1) **Escada em Espiral** (corredor vertical); (2) **Arquivo dos Nomes** (cruzamento, estantes); (3) **Sala
  das Cordas** (descanso); (4) **Galeria das Gárgulas** (elite); (5) **Arena — O Campanário**.
- **Monstros:**
  - **Sineiro** (A) — *atordoa*: gárgula com um sino no peito; badalada em área a cada 5 s. Aviso: o sino balança e um
    anel âmbar cresce no chão 0,8 s. Partes: sino (quebrado, não badala), 2 asas → *Bronze Mudo*, *Pedra de Gárgula*.
  - **Casca** (A) — *bloqueia*: tartaruga-porta, imune de frente. Aviso: abre o casco (âmbar) antes de investir. Partes:
    3 placas → *Placa de Casca*. Eco de: portas trancadas por quem não queria notícia.
  - **Traça-de-Arquivo** (A) — *apaga*: enxame de traças de papel que gruda na lanterna e encolhe o raio de luz. Aviso:
    o enxame se junta numa bola âmbar 0,6 s antes de investir. Parte: asa de papel → *Papel de Registro*.
- **Exame A — "A Escada"** (aplicado por Odete, não por Benício — ver cena 2): subir a escada da Brecha da Ordem em
  tempo, sem poção, com Sineiros atordoando. Testa leitura de aviso e esquiva.
- **Guardião — O Sineiro Mudo** (2 fases):
  1. *A Última Badalada:* gárgula do tamanho do sino, sem boca. Badaladas em área (anéis âmbar), voo e mergulho (sombra no
     chão 0,8 s). Partes: **2 asas** (quebradas, não voa), **o sino** (quebrado, abre a fase 2).
  2. *O Silêncio* (abaixo de 50%): **todo som do jogo some** — música, passos, golpes. Os ataques só são avisados pela
     imagem. Ao fundo, em legenda, a voz de Benício chama "Tomé!" a cada badalada que não soa.
- **Morte do Guardião:** o Sineiro deixa cair **o Badalo** — a peça que falta no sino de verdade. Item-chave do Final.
- **NPC central — Mestre Benício:** frio, formal, adiando o exame. Lacerda tenta depô-lo revelando ao conselho que Benício
  "esconde o tamanho real do Avesso". Benício não nega.
- **Cenas-chave:**
  1. O jogador encontra no arquivo **17 cartas devolvidas**, todas iguais: *"Herança de Marisa D., para quem precisar."* A
     18ª é a do jogador.
  2. Benício se recusa a aplicar o exame ("Não sou mais digno de julgar ninguém"); **Odete** sobe a Torre pela primeira vez
     em 20 anos e aplica no lugar dele.
  3. A confissão no campanário: *"Eu não toquei a alvorada. Se tocasse, o Tomé estava morto. Não toquei. E o dia não veio."*
  4. **Escolha:** "Você fez o que pôde." / "Você fez a cidade inteira esperar com você." (flag `perdoou_benicio`; pesa no Final).
  5. Benício explica a carta: *"Você não foi escolhido, {nome}. Foi o primeiro que veio."*
- **Recompensa:** passou no Exame A → **Estágio 3 (veterano)** e habilidade 3. Sineiro → **rank S**, **Eco do Sino**,
  conjunto **Bronze Mudo**, o **Badalo**.
- **Gancho:** quando o Badalo entra na Torre, todas as Brechas da cidade tremem ao mesmo tempo. Nas Ruas Baixas, as casas
  começam a **mudar de lugar** à noite.

### Ato VI — A Casa que Andava (S → SS · Ruas Baixas → Ruas Invertidas · 25–35 min)

- **Tema:** quem paga a conta do luto dos outros. **Pergunta dramática:** *a cidade sobrevive sem o Avesso?*
- **Hub — Ruas Baixas:** bairro pobre de casas de madeira sobre palafitas, varais cruzando as vielas, música, a **Taverna
  da Nilza**. Num terreno vazio, marcas no chão de casas demolidas há 18 anos para erguer a ala nova da Torre da Ordem —
  entre elas, a casa da família do Ravi.
- **Brecha — Ruas Invertidas:** as Ruas Baixas **antes das demolições**: as casas que não existem mais, de pé e habitadas
  por ecos — vielas que dão a volta e voltam ao mesmo lugar, portas que abrem para o céu, varais em todas as direções.
- **Salas:** (1) **Viela dos Varais** (corredor); (2) **Largo do Chafariz** (cruzamento); (3) **Cozinha da Avó** (descanso: a
  taverna antes de ser da Nilza); (4) **Rua que Dá a Volta** (quebra-cabeça leve: seguir os varais com roupa de criança);
  (5) **Arena — O Terreno**.
- **Monstros:**
  - **Varal** (S) — *enlaça*: lençóis vivos estendidos entre postes; prendem quem passa por baixo. Aviso: o lençol infla e
    a borda brilha âmbar 0,6 s. Parte: prendedor → *Prendedor de Bronze*.
  - **Despejado** (S) — *arremessa*: figura curvada carregando móveis nas costas; atira cadeira, gaveta, berço (sombra no
    chão 0,8 s). Parte: carga nas costas → *Mobília do Avesso*. Eco de: quem foi tirado de casa.
  - **Rasgo** (S) — *teleporta*: pedaço da própria Brecha; some e reaparece atrás do jogador. Aviso: o ponto de chegada
    brilha âmbar 0,5 s antes. Partes: 2 bordas → *Fio do Avesso* (lendário).
- **Exame S — "A Cozinha"** (Benício volta a aplicar, se perdoado; Odete, se não): sobreviver 3 minutos protegendo a
  cozinha da Nilza numa Brecha da Ordem, com ondas mistas. Testa controle de multidão.
- **Guardião — O Inquilino** (2 fases):
  1. *O Despejo:* uma casa das Ruas Baixas andando em 4 pernas de madeira; esmaga (sombra 0,8 s) e atira móveis pelas
     janelas. Partes: **4 pernas** (cada uma quebrada = mais lento; com 2 quebradas, ajoelha e expõe a porta).
  2. *Lá Dentro* (abaixo de 50%): abre a porta e **puxa o jogador para dentro** (sub-arena: a sala da casa do Ravi, de 18
     anos atrás, com a mesa posta). Lá dentro, quebrar as 3 vigas enquanto a casa pega fogo de fora para dentro.
- **NPC central — Tia Nilza:** confessa que cozinha com Fragmentos ("Deixa tudo mais gostoso. A gente lembra de coisa boa
  quando come.") e que a cidade inteira depende disso. Pede ajuda para fazer a **Sopa da Véspera de verdade**, sem
  Fragmento: os ingredientes estão nas Memórias da Brecha (a receita da avó, escrita na parede da cozinha antiga).
- **Arco do Ravi:** na sub-arena, Ravi reconhece a mesa da mãe. Ele precisa escolher derrubar a casa (sair) ou ficar. Ele
  derruba. *"Eu queria tirar minha família das Ruas Baixas. Acho que eu queria era voltar pra essa casa."*
- **Lacerda:** mostra ao jogador o livro de contas: sem Fragmentos, o inverno mata as Ruas Baixas primeiro. Não mente.
- **Cenas-chave:** (1) as casas mudando de lugar; (2) a confissão da Nilza; (3) a mesa posta dentro do Inquilino; (4) a
  primeira Sopa da Véspera de verdade, servida no terreno vazio para o bairro todo (cena de comunidade, como uma festa);
  (5) Lacerda tranca o Badalo no cofre da Ordem "até o conselho decidir".
- **Recompensa:** passou no Exame S → Sopa da Véspera (2ª vida uma vez por Brecha). Inquilino → **rank SS** → **Estágio 4
  (elite)** e habilidade 4; **Eco do Lar**; conjunto **Madeira de Palafita**.
- **Gancho:** na noite seguinte, as Brechas de todos os bairros abrem **juntas** e se costuram umas nas outras. É o
  aniversário de 20 anos da Maré Grande: a Festa das Lanternas.

### Ato VII — A Noite Mais Longa (SS → SSS · Avesso Profundo · 30–40 min)

- **Tema:** o que a gente é sem o ontem. **Pergunta dramática:** *quem toca o sino — e quem some quando ele tocar?*
- **Hub — Lumeira em festa e em cerco:** a Festa das Lanternas acontece apesar de tudo. Cada bairro tem uma Brecha aberta
  e um NPC defendendo (Odete nas Forjas, Celeste no Pomar, Severo e Iara no cais, Nilza nas Ruas Baixas, Benício na Torre).
  O jogador escolhe a ordem (como os mundos de Death's Door antes da porta final).
- **Brecha — Avesso Profundo:** Brechas que misturam bairros: o esgoto que desemboca no pomar, o cais que sobe a escada da
  torre, a viela que termina no cemitério. Quanto mais fundo, mais tudo se parece com **a Praça na noite da festa**.
- **Salas (geradas, temas misturados):** Esgoto-Pomar, Cais-Torre, Viela-Cemitério, Descanso (a forja da Odete de 20 anos
  atrás, com duas lanternas na bancada), e a **Arena dos Ecos Maiores**.
- **Monstros:** todos os anteriores em versão SS (padrões novos, não só números) +
  - **Ceifador de Pomar** (SS) — *vigia*: espantalho que só se move quando o jogador não olha (cone da lanterna congela).
  - **Sombra de Caçador** (SSS) — *imita*: ecos de caçadores caídos, com Lâmina, Arco ou Martelo, usando o verbo da classe.
    Aviso: o mesmo do jogador (brilho âmbar na arma). Parte: selo → *Selo Apagado* (devolver ao Benício dá a história de
    cada um: 20 anos de nomes).
- **Exame SSS — "A Porta"** (aplicado pela **Marisa**, dentro do Avesso): segurar a porta da Praça por 90 s enquanto ela
  descansa — o jogador sente o que ela sente há 20 anos (a tela vai perdendo a cor). Ninguém da Ordem pode aplicar o SSS
  porque ninguém da Ordem chegou lá.
- **Guardião — Ecos Maiores** (2 fases): a Lodo-Mãe, a Macieira Oca e o Farol Afogado fundidos numa massa só (ou outra
  combinação conforme a ordem dos bairros).
  1. *O Que a Cidade Guardou:* os três alternam ataques conhecidos, mais rápidos, com avisos encurtados (0,4 s).
  2. *O Que a Cidade Soltou* (abaixo de 50%): cada Eco que o jogador tem equipado "acorda" o Guardião de origem, que se
     separa e **luta ao lado do jogador** por 10 s (os Guardiões vencidos devolvem o favor).
- **NPCs centrais:** **Celeste** e **Lis**.
  - Celeste confessa que é eco, e diz a regra: *"O Avesso só devolve quem é amado hoje."* Ela tem medo de sumir quando o sino
    tocar. Pede que o jogador leve a Lis até a Nilza "para ela ouvir de alguém que a ama".
  - Lis descobre que é eco (o pano de festa bordado). Foge para a Brecha. O jogador a encontra na Praça-de-ontem, sentada
    debaixo da grande lanterna, onde a Nilza a achou. **Escolha:** apresentá-la à Ordem como aprendiz (flag
    `lis_ordem`) ou escondê-la ("Ninguém precisa saber.") — muda quem pode tocar o sino.
- **Odete:** volta a lutar (rank A, aliada de IA em uma Brecha) e forja, com a placa de selo da Marisa e um Fragmento de
  cada Guardião, a **arma de Duas Vésperas** da classe do jogador (Lâmina, Arco ou Martelo de Duas Vésperas).
- **Lacerda:** com a cidade em cerco, o jogador decide: **convencer** (mostrar as Memórias juntadas, ou ter a relação certa
  com Nilza/Ravi) e ele entrega o Badalo; ou **tomar** o Badalo do cofre (luta curta com a guarda da Ordem, sem morte).
- **Cenas-chave:** (1) a festa em cerco; (2) Odete forjando com a placa da Marisa; (3) Lis debaixo da lanterna; (4) o
  Exame SSS segurando a porta; (5) a Brecha da Praça se abre — a primeira em 20 anos.
- **Recompensa:** **rank SSS**, arma de Duas Vésperas, conjunto **Vigília** (do Avesso Profundo).
- **Gancho:** a Brecha da praça. Lá dentro é véspera, há 20 anos, e alguém espera usando a lanterna do jogador.

### Final — Alvorada (SSS → Alvor · Praça da Lanterna → a Praça da noite da festa · 20–30 min)

- **Tema:** soltar. **Pergunta dramática:** *o jogador consegue deixar o próprio ontem para trás?*
- **Portão:** SSS + conjunto de Guardião com 4 peças + 2 Ecos (PLANO §5).
- **Brecha — A Praça da Noite da Festa:** a praça há 20 anos, minutos antes da Maré Grande: bandeirolas, barracas, música
  (a trilha da cidade tocada ao contrário), ecos de todos os NPCs mais jovens — Odete e Marisa dançando, Iara criança
  ensaiando, Benício subindo para tocar a véspera. Ninguém vê o jogador. Três salas curtas de "passeio" (sem combate; o
  jogador anda entre os ecos, como em um museu de memória) e a arena.
- **Guardião — O Reflexo** (2 fases):
  1. *Espelho:* o jogador — mesma arma, mesmos Ecos, mesmo rank, movimentos espelhados. Usa os padrões que o jogador mais
     usou desde o Prólogo (estatísticas registradas). Aviso: o mesmo brilho âmbar que o jogador vê na própria arma.
  2. *O Seu Ontem* (abaixo de 50%): a arena vira os lugares por onde o jogador passou. Cada **Lanterna não recuperada** na
     campanha aparece acesa no chão e dá um buff ao Reflexo; o jogador pode apagá-las (interagir, 1 s, exposto). O Reflexo
     fala com as respostas que o jogador deu ao Vigia.
- **Depois do Reflexo:** com o Badalo, alguém toca a alvorada:
  - **Benício** (se `perdoou_benicio`): toca, e chora pela primeira vez no jogo. Depois deixa a Ordem e vai pescar com Severo.
  - **Lis** (se `lis_ordem`): toca como aprendiz da Ordem. Na cena final ela mede a altura na porta da taverna: cresceu.
  - **O jogador** (sempre disponível): toca, e fica em Lumeira como guardião do sino.
- **Despedidas:** Marisa solta a porta; o eco dela some no nascer do sol depois de dizer a Odete *"Voltei."* (a única vez
  que Odete sorri). Celeste e Lis ficam. As Brechas param de abrir. Os Fragmentos começam a perder o brilho (Lacerda e a
  cidade terão de aprender outra luz — semente da Parte 2).
- **Recompensa:** **rank Alvor**, **Estágio 5 (radiante)** e habilidade 5, **Eco do Avesso** (um clone luta ao seu lado 8 s).
- **Gancho da Parte 2:** amanhece. Muito longe, do outro lado do mar, **outro sino responde**. Na praia, uma lanterna
  desconhecida encalha acesa, piscando três vezes. Lis: *"O friozinho… agora vem do mar."*

---

## 7. Escolhas e flags que pesam no Final

Poucas, claras, sempre com consequência visível (nada de escolha "secreta" que o jogador não percebe).

| Flag | Onde nasce | O que muda |
|---|---|---|
| `ravi_exame_c_ajudado` | Ato III, Exame C | Ato IV: Ravi entra na Brecha B por coragem (ajudado) ou por vergonha (reprovado); falas diferentes |
| `pagou_chama_viuva` | Ato IV, Lacerda | Ato VII: a viúva convence Lacerda junto com o jogador |
| `perdoou_benicio` | Ato V, campanário | Benício aplica o Exame S e pode tocar o sino no Final |
| `nilza_sopa_verdadeira` | Ato VI | Ato VII: as Ruas Baixas aguentam o cerco sem Fragmento |
| `lis_ordem` | Ato VII | Lis pode tocar o sino; sem ela, Lis fica com Nilza e o epílogo muda |
| `lacerda_convencido` | Ato VII | Badalo entregue sem luta; Lacerda aparece no epílogo planejando "a cidade sem Fragmento" |
| respostas ao Vigia (`vigia_r1..r4`) | Atos IV–VII | o Reflexo repete as respostas do jogador na fase 2 |
| Lanternas não recuperadas | toda a campanha | buffs do Reflexo (GDD §5.5) |

---

## 8. Regras de escrita

1. **Uma ideia por caixa.** Até ~150 caracteres. Se precisa de "e" duas vezes, são duas caixas.
2. **Palavras simples, sentimentos adultos.** O texto é fácil; o assunto não é bobo. Ninguém fala "o mal", "trevas",
   "profecia". As pessoas falam de culpa, dinheiro, saudade, medo de envelhecer.
3. **Mostrar antes de explicar.** O hub mostra (muro de nomes, casa demolida, farol escuro); a fala explica depois, e
   pouco. Inspiração: Hyper Light Drifter conta quase tudo com cenário.
4. **Conversas que mudam a cada volta.** Cada NPC central tem fala nova depois de: primeira caçada, morte na Brecha,
   Memória achada, parte de Guardião quebrada. Inspiração: Hades — o mundo comenta o que o jogador acabou de fazer.
5. **Humor como respiro, nunca no lugar da dor.** Ravi e Lis fazem rir; ninguém faz piada do luto de outro.
6. **Todo monstro é eco de algo humano** e o nome diz isso (Afogado, Lanterna-Afogada, Despejado, Cão-de-Vigília).
7. **Guardião conta a história na fase 2** com mecânica (a luz apaga, o som some, a casa te engole), não com texto.
   Inspiração: Death's Door — chefes que são pessoas que não aceitaram o fim.
8. **Sem "escolha moral" de botão bom/mau.** As escolhas são entre duas coisas defensáveis.
9. **Nome do jogador** só com `{nome}`; arma com `{arma}`/`{este}`/`{armaNome}`. Nunca gênero gramatical do jogador
   ("cansado/cansada"): reescreva a frase ("cara de cansaço").
10. **Os ecos dos NPCs no Avesso** usam a mesma fala de hoje, mas 20 anos mais jovens: é o jeito mais barato e mais
    triste de contar quem eles eram.

---

## 9. Glossário (para o jogador e para o time)

- **Lumeira:** cidade costeira iluminada por lanternas. **Praça da Lanterna:** o centro, com a grande lanterna de bronze.
- **Ano Sem Alvor:** o ano, há 20 anos, em que o sol não nasceu. Começou na noite da **Maré Grande**.
- **Véspera:** o fim da tarde, quando as Brechas abrem. Também o nome do jogo: a cidade vive numa véspera sem fim.
- **Alvor:** o primeiro clarão do dia. O rank mais alto, que ninguém nunca alcançou.
- **Brecha:** rasgo no ar que leva ao **Avesso**, a cópia do bairro como era antes do Ano Sem Alvor.
- **Fragmento:** cristal do Avesso; alimenta lanternas e forja. **Lume:** moeda da cidade.
- **Eco:** poder deixado por um Guardião; também o nome de quem "volta" do Avesso (Celeste, Lis, Marisa).
- **Memória:** objeto do ontem achado na Brecha; destrava falas e histórias.
- **Lanterna (morte):** ao cair, os Fragmentos ficam na Lanterna, dentro da Brecha, até você buscar.
- **Selo:** a marca de rank queimada no pulso: E, D, C, B, A, S, SS, SSS, Alvor.

---

## 10. Decisões tomadas aqui (para o dono revisar)

1. **Causa do Ano Sem Alvor:** a Maré Grande + o sino que não tocou a alvorada. Amarra Severo (farol), Benício (sino),
   Marisa (porta), Iara e Tomé numa noite só.
2. **Idade da Lis:** o GDD dizia "11 anos" e "nasceu no Ano Sem Alvor" (20 anos atrás) — contradição. Resolvido: ela é eco
   de um bebê daquele ano, achada há 11 anos. A fala atual dela ("Eu nasci no ano em que o sol sumiu") continua valendo.
3. **Guardiões com 2 fases** (o GDD tinha 3): a terceira virou parte da segunda ou foi cortada, para caber em 4–8 min.
4. **Novo personagem: Iara**, filha de Severo (a dor do Ato III fica humana e de duas pessoas).
5. **Novo personagem: Aurélio Lacerda**, tesoureiro da Ordem: dá um conflito humano aos Atos IV–VII sem vilão caricato.
6. **Novos monstros:** Lanterna-Afogada, Ancoreiro (III); Carpideira, Cão-de-Vigília (IV); Traça-de-Arquivo (V); Varal,
   Despejado (VI); Sombra de Caçador (VII). O **Sineiro** saiu do rank C e foi para o Ato V (Torre).
7. **Ravi não morre** no modo Normal (no Difícil, sim, como no GDD): o arco dele precisa dele vivo no Ato VI.
8. **Exame A aplicado pela Odete** e **Exame SSS pela Marisa**: os exames viram cenas, não só provas.
9. **O jogador não é "o escolhido":** é o 18º destinatário de uma carta. O selo queimou porque ele veio.
