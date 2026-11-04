# Véspera — O roteiro do jogo, como ele acontece

Documento de leitura para o dono do projeto. Conta a história e o que o jogador faz, do início ao fim, **com base no que está implementado nos arquivos do jogo** (falas, objetivos, Brechas) e, quando preciso, no roteiro canônico (`docs/v2/ROTEIRO-V2.md`) e na campanha (`docs/v2/CAMPANHA.md`).

Como ler:

- Texto entre aspas e em itálico é **fala real do jogo**, copiada dos arquivos. Quando eu resumo, escrevo "(resumo)".
- "(só no roteiro)" marca o que o seu roteiro pede e que **não achei** no jogo. Tudo isso volta, listado, na última seção.
- Há spoilers de tudo, inclusive do final.
- Fontes lidas: a branch `v2` e, para o Ato VII, a branch `atos67` (worktree `C:\ale\vespera-wt\atos67`). O Ato VII está jogável lá, com teste automático, mas **ainda não foi fundido na `v2`** (marcado onde aparece).

---

## 1. Abertura do documento

### 1.1 A premissa em 5 linhas

1. Lumeira é uma cidade costeira cuja grande lanterna de bronze, na praça, mantém a cidade "deste lado".
2. Há vinte anos, numa Festa das Lanternas, a Ordem tocou o sino da Torre para chamar de volta o que a cidade perdeu. Deu errado: metade de Lumeira afundou no Avesso e o sol não nasceu por um ano.
3. Uma caçadora, Marisa, fechou a Brecha da praça por dentro e não voltou. O sol nasceu. Ficaram dezenas de rasgos pequenos que reabrem todo fim de tarde.
4. Você chega com uma lanterna velha que já atravessou, e o selo queima sozinho no seu pulso.
5. Você sobe de selo, descobre quem foi Marisa, quem tocou o sino e o que o Avesso devolve, até enfrentar o seu próprio Reflexo na primeira Brecha e escolher quem toca o sino de novo.

### 1.2 Glossário (as palavras exatas do jogo)

| Palavra | O que é |
|---|---|
| **Lumeira** | A cidade. |
| **O Ano Sem Sol** | O ano, há vinte anos, em que o sol não nasceu. |
| **Brecha** | O portal que abre todo fim de tarde e cospe monstros. Se você mata o que tem dentro, ela fecha. |
| **O Avesso** | O outro lado: "Lumeira de cabeça pra baixo", onde cai tudo que se perde. |
| **Fragmento** | O cristal que cai dos monstros. É a moeda da forja da Odete. |
| **Selo** | A marca do rank no pulso. "Subir de selo" é subir de rank (E, D, C, B, A, S, SS, SSS, Alvor). |
| **Guardião** | O chefe de uma Brecha. |
| **Eco** | Habilidade especial que um Guardião deixa. O Vigia guarda e explica. |
| **Lanterna** | A lanterna do jogador. Quando ele cai, ela fica no chão da Brecha com os Fragmentos da caçada. |
| **Alvor** | O rank máximo. Para a Ordem, "quem faz amanhecer". O povo chama de "Deus". |

### 1.3 Personagens principais

- **Benício** — velho mestre da Ordem da Lanterna, de bengala. Examina o jogador a cada selo. Foi ele quem puxou a corda do sino, há vinte anos, e escondeu isso.
- **Ravi** — rival e amigo, caçador mais novo. Aposta sopa contra o jogador. A mãe dele mora nas Ruas Baixas.
- **Nilza** — dona da taverna, onde o jogador dorme. Cozinha a comida que dá fôlego. O marido pescador não voltou do mar.
- **Odete** — ferreira da Forja. Olha demais a lanterna do jogador. Esperou alguém por vinte anos: Marisa.
- **Lis** — vende mapas na escada da praça. Os monstros não chegam perto dela. Diz a última frase do jogo.
- **Celeste** — herbalista do Pomar Alto. Não envelhece, nunca come. Mora com as macieiras.
- **Severo** — faroleiro que virou pescador. Deixou o farol apagado naquela noite e acha que a culpa é dele.
- **O Vigia** — encapuzado, só aparece à noite no Cemitério. Faz cinco perguntas ao longo do jogo e lembra as respostas.
- **Marisa** — nunca aparece. Era a parceira da Odete e a dona da lanterna do jogador.
- **A mãe do Ravi** — aparece a partir do Ato VI.

---

## 2. Abertura (antes do Ato I)

**Lugar:** tela de abertura e a estrada que chega a Lumeira.

1. Duas cartelas de 3 segundos, as únicas frases de narrador:
   - *"Há vinte anos, o sol não nasceu por um ano inteiro."*
   - *"Lumeira acende as luzes antes de escurecer. Todo dia."*
2. Corte para o jogo. O jogador anda pela **Estrada** com uma lanterna velha e uma Lâmina enferrujada. Um Lodo cruza o caminho e ele aprende a atacar.
3. Objetivo na tela: **"Siga até a praça"**. Ao chegar, a Brecha abre no meio da praça e todos correm para dentro de casa.

(Não há escolha de classe no começo: o jogador começa com a Lâmina e escolhe a arma definitiva depois.)

---

## 3. Ato I — "A Primeira Luz" (selo E → D)

**Lugares:** Estrada, Praça da Lanterna, Taverna da Nilza, quarto em cima da taverna, Forja da Odete, Bairro das Forjas. **Brecha:** Esgotos do Mercado (rank E).

### O que o jogador faz, passo a passo

1. **Chegada (Benício, na praça):**
   - *"Você. Com a lanterna. Isso é uma Brecha e vai cuspir bicho a noite toda."*
   - *"Fecha ela. Mata o que tem dentro e ela fecha sozinha."* Objetivo: **Feche a Brecha**.
2. **Ravi chega correndo:** *"Opa, era minha! Tá, vai você."* / *"Aposto a sopa que eu fecho mais salas que você."* Escolha: "Apostado." ou "Perguntar mais" (*"Minha mãe lava roupa nas Ruas Baixas."* / *"Caçador de selo C ganha em um mês o que ela ganha no ano."*).
3. **Primeira caçada nos Esgotos.** Dentro da Brecha há suportes de arma: um Arco na 1ª caçada e um Martelo na 2ª, valendo só naquela caçada, para o jogador testar as três armas.
4. **Ao sair, Ravi com o placar:** *"Duas salas. Você?"*. O jogador responde "Todas." e Ravi: *"…Sopa por minha conta. Vem."* Objetivo: **Coma com a Nilza**.
5. **Taverna (Nilza):** *"Primeira Brecha e já volta inteiro. Come."* / *"Depois vai na Odete, que essa lâmina tá chorando."* A sopa é a mecânica de comida: dá vida máxima a mais na caçada seguinte. Pergunta opcional: *"Meu marido pescava. O mar não devolveu."* / *"Abri isso aqui com o que a Ordem pagou. Come."*
6. **Forja (Odete):** se o jogador chegar sem Fragmento: *"Sem Fragmento, sem forja. Fecha a Brecha primeiro."* Com eles: *"Isso que caiu do bicho. Me dá."* / *"Amanhã abre outra. Volta com mais."*
7. **Noite e manhã.** O jogador dorme no quarto. De manhã a praça tem o **quadro de contratos** do Benício (*"Quadro de contratos. Pega um antes da véspera."*) e a **Lis** (*"Hoje abre no Bairro das Forjas."* / *"Quer ver por dentro antes? Dez Lumes."*). Pergunta opcional da Lis: *"Os bichos não chegam perto de mim. Não sei por quê."* / *"Não conta pra Ordem. Vai, pega um contrato."*
8. **Segunda caçada** (agora com Bênçãos da Lanterna, contrato e elite; veja a seção 12).
9. **O exame (Benício):** *"Hoje eu entro com você. Não pra ajudar."* Ele entra na Brecha atrás do jogador e comenta uma linha por sala, seca: *"Finge que eu não tô aqui."*, *"Quebrou o núcleo primeiro. Bom."*, *"Tomou dano bobo."*, *"Limpo. Segue."*, *"Rápido. Bom."* e, na sala do Guardião, *"Agora é ela. Eu só olho."*
10. **A Lodo-Mãe** (Guardiã) cai. A Brecha fecha. Na praça, Benício queima o selo: *"D."* Ravi: *"Isso! Selo D!"* / *"A Odete mandou te chamar. Vai lá."*
11. **Forja (fecho do ato).** Odete: *"Você já usou as três. Qual vai levar?"* O jogador **escolhe a arma definitiva** (Lâmina, Arco ou Martelo). Depois: *"A última pessoa que eu esperei voltar se chamava Marisa."* / *"Não voltou. Vai dormir."*
12. **Manhã seguinte:** a Lis cheira uma maçã roxa de um carroceiro: *"Isso veio do Pomar Alto. Maçã não nasce dessa cor."* O carroceiro: *"Desço do Pomar Alto toda semana. Essas vieram assim."* Objetivo: **Vá ao Pomar Alto**.

### Perguntar mais (Benício, no quadro)

- *"O Avesso? É Lumeira de cabeça pra baixo. Tudo que a gente joga fora cai lá."*
- *"Selo não escolhe à toa. O seu queimou sozinho. Nunca vi."*

### A Brecha: Esgotos do Mercado (rank E)

- **Salas (4 tipos, sorteadas 5 a 7 por caçada):** Corredor, Cruzamento, Sala do Tesouro, Arena da Lodo-Mãe.
- **Monstros:** **Lodo** (pula em direção ao jogador; só machuca ao pousar) e **Espectro** (persegue e atravessa paredes).
- **Guardiã — Lodo-Mãe**, que só aparece depois de duas caçadas completas. Três fases:
  1. Pula e esparrama Lodos.
  2. Alterna o pulo com uma investida de engolir (quem é pego fica 2 segundos dentro).
  3. Divide-se em até três; cada um dos 3 núcleos quebrados tira uma divisão.
  - Todo golpe forte tem aviso âmbar no chão de 0,6 s. Deixa o **Eco Viscoso** (o dash deixa rastro que lentifica) e o Conjunto da Lodo-Mãe.

### O que muda na cidade
- A praça ganha **quatro lampiões novos** e a barraca de lona da Lis vira uma **banca de verdade** (só depois do fecho do ato).

### Mistério
- **Plantado:** de quem era a lanterna (ela acendeu sozinha quando a Brecha abriu); por que o selo queimou sozinho; por que os monstros evitam a Lis.
- **Pago em parte:** Odete esperava alguém, Marisa. A página **Perguntas** do Diário ganha uma linha por mistério.

### Mortes (cada uma só na vez dela, depois sorteia)
Nilza: *"Sua lanterna ficou lá dentro. O Ravi disse que viu acesa."* · Ravi: *"Eu já morri três vezes. A primeira dói mais."* · Odete: *"Lâmina não quebra. Caçador quebra. Vai buscar."* · Benício: *"Caiu, levantou. A Ordem só anota a segunda parte."* · Lis: *"Sua lanterna ficou chamando a noite toda. Eu ouvi."* · Nilza: *"A Odete perguntou se você voltou. Ela não pergunta de ninguém."*

### Gancho
Maçãs roxas vindas do Pomar Alto.

---

## 4. Ato II — "O Pomar que Lembra" (selo D → C)

**Lugares:** Pomar Alto (escadaria ao norte da praça). **Brecha:** Pomar Podre (rank D).

### O que o jogador faz
1. **Celeste, na colina:** *"Você é quem fechou a da praça. As árvores me contaram."* / *"Elas estão apodrecendo de dentro. Ajuda."* Depois: *"Lá dentro é o pomar de antes. Não come nada."* / *"A Brecha abriu a oeste, perto da cerca. Entra e volta inteiro."* Objetivo: **Entre na Brecha do Pomar**.
2. **Conversa opcional (escolha "Perguntar mais"):** *"Por que eu moro sozinha? Porque as plantas não perguntam nada."* · *"A Lis? Eu achei ela. Faz tempo."* · *"Não. A gente está do mesmo lado, eles e eu."*
3. **Moradores do Pomar** (falas soltas pela cerca): o apicultor (*"As abelhas não saem mais da caixa. Nem de dia."*), um menino (*"Minha mãe diz que a Dona Celeste não envelhece."* / *"Eu nunca vi ela comer. Nem maçã."*), a dona da horta (*"Plantei abóbora e nasceu roxa por dentro."* / *"A Celeste mandou queimar. Queimei."*) e a última macieira (*"Essa ainda dá fruta vermelha. É a última."*).
4. **Primeira caçada no Pomar.** Quando termina, Lis aparece e Celeste a vê:
   - Celeste: *"Lis."* — Lis: *"Dona Celeste."* — Lis: *"Eu vim desenhar o pomar novo. Volta lá dentro, que eu espero aqui."*
   - (O jogo não explica o silêncio.)
5. **Segunda caçada** e então a **Macieira Oca**. Celeste avisa: *"A árvore grande acordou. Ela está te esperando no fundo da Brecha."* / *"Fica longe das raízes quando o chão brilhar. Vai."*
6. **Ao sair (Celeste):** *"Fechou. As raízes vão parar de doer."* / *"Você viu a árvore grande? Era minha."* / *"O Benício quer te ver na praça. Desce a escada, ao sul."*
7. **Fecho na praça.** Benício queima o selo, mas só se a progressão já deu o C: *"C. Agora você pode entrar onde a maré sobe."* (se faltar XP: *"A Macieira caiu. Falta pouco para o C: volta ao Pomar e caça mais um pouco."*). Ravi: *"C! Eu tô em D ainda."* / *"Tá, mas a sopa continua por sua conta."* Lis: *"O Severo tá procurando gente que não tem medo de água."* / *"Ele falou 'Marisa' sem querer e fechou a cara."*

### A Brecha: Pomar Podre (rank D)
- **Salas:** Corredor do Pomar Podre, Cruzamento, Clareira (descanso e tesouro), Covil da Macieira Oca.
- **Monstros:** **Carrapicho** (planta que chicoteia raízes numa faixa âmbar) e **Rato-Lanterna** (enxame de 3; salta e morde; um golpe derruba).
- **Guardiã — Macieira Oca** (depois de duas caçadas). Três fases:
  1. Lança maçãs podres: círculo âmbar, explodem em roxo.
  2. Arranca as raízes e anda; alterna maçãs com raízes que brotam numa faixa âmbar.
  3. Mais rápida: 3 maçãs, leque de 3 faixas de raízes e chama Ratos-Lanterna.
  - Não fala. Ao acordar, **cai uma maçã roxa aos pés do jogador**. Deixa o **Eco da Semente**.

### O que muda na cidade
- Só o que já existia (a carroça de maçãs roxas some na praça depois do ato). **Horta e feira da manhã: (só no roteiro).**

### Mistério
- **Plantado:** o que a Celeste é (olhos, nunca come, "do mesmo lado" que os monstros).
- **Pago em parte:** Celeste e Lis se conhecem de um jeito que ninguém explica.

### Mortes
Celeste: *"Você caiu entre as raízes. Elas te seguraram até de manhã."* · Nilza: *"Trouxeram você dormindo. Quem trouxe não quis dizer o nome."* · Ravi: *"A Lis ficou na porta da Brecha a noite inteira. Nenhum bicho saiu."* · Benício: *"Caiu no Pomar. Todo mundo cai no Pomar."* / *"Eu caí."* · Odete: *"Sua lanterna tava acesa quando te acharam. Lanterna de morto apaga."* · Celeste: *"Ouvi você chamar lá de baixo. Não foi com a boca."*

### Gancho
Lis: o Severo procura gente que não tem medo de água, e disse "Marisa".

---

## 5. Ato III — "O Farol que Não Acendeu" (selo C → B)

**Lugares:** Mercado das Marés (ponte a leste da praça), Forja. **Brecha:** Cais Afogado (rank C).

### O que o jogador faz
1. **Severo, no cais, sem olhar:** *"Sabe nadar? Lá dentro não adianta."* / *"Mas pelo menos você vai saber de que lado tá o ar."* Depois: *"Na véspera o mar traz coisa de lá. Peixe com olho de vidro."* / *"A Nilza paga bem. Não come cru."* Objetivo: **Pesque no fim do píer** (puxar a linha quando o olho de vidro brilha). O ponto de pesca: *"Puxa quando o olho de vidro brilhar."* (antes de falar com o Severo: *"A vara é do Severo. Fala com ele antes."*)
2. **Benício, exame C:** *"Hoje eu não entro. Hoje eu só espero na saída."* / *"Se demorar, eu anoto."*
3. **Caçada no Cais.** Na sala alagada (Mercado Submerso), sobre uma pedra seca, o jogador acha a **lanterna "M."**: *Lanterna de caçador, gravada: "M." Igual à sua.* (Se já a pegou: *"Só a marca da lanterna ficou na pedra molhada."*)
4. **Severo, ao ver a lanterna:** *"Onde achou isso."* / *"Marisa. Ela me pediu pra manter o farol aceso."* / *"Eu fui pra festa."* Opcionais: *"Marisa e Odete entravam juntas. Saíam juntas."* / *"Até o dia."* e, na porta do farol, *"Apagado desde aquela noite."* / *"Eu tentei. A chama não pega."*
5. **Odete recebe a lanterna** (a cena mais longa do ato, em 4 balões): *"Deixa aí em cima."* → *"Vai embora."* (o jogador pode tentar sair) → *"Espera."* → *"Obrigada."*
6. **Guardião: Farol Afogado.** Na fase 3, a luz do farol apaga e ele diz a única fala: *"Eu apaguei por uma noite."*
7. **Fecho (Benício, no Mercado):** *"B."* / *"Agora você entra no Cemitério. Leve a lanterna acesa. A sua."*
8. **Manhã:** Nilza: *"A Odete passou aqui às cinco da manhã e pediu sopa."* / *"Em vinte anos ela nunca pediu."* Lis: *"Tem um homem no Cemitério que só aparece de noite."* / *"Ele me perguntou seu nome. Eu não falei."*
- Extras: Ravi no Mercado (*"Passei no exame D! Sem medo dessa vez."* / *"Mentira. Com medo, mas passei."*); o muro do quebra-mar (*"Nomes riscados na pedra, um embaixo do outro. Um deles tem flor fresca."*); Nilza, em pergunta: *"Meu marido tá naquela Brecha. Não vai lá procurar."* / *"Eu já procurei."*

### A Brecha: Cais Afogado (rank C)
- O porto na noite da Festa das Lanternas, do lado de baixo.
- **Salas:** Píer das Bandeirolas, **Mercado Submerso** (alagada; entra em toda caçada, com a lanterna "M."), Barco Virado (descanso e tesouro), Quebra-mar dos Postes (elite e tesouro), Pé do Farol (arena).
- **Monstros:** **Afogado** (lento, pancada dupla à frente), **Ancoreiro** (arremessa a âncora e puxa), **Lanterna-Afogada** (derrama um círculo de água; chega em dupla).
- **Guardião — Farol Afogado** (depois de duas caçadas **e** de a Odete receber a lanterna). Três fases:
  1. **Feixe:** o feixe gira e puxa o jogador.
  2. **Maré:** chama Afogados e o anel externo alaga.
  3. **Escuro:** a arena inunda (a zona seca muda de lugar), a luz apaga; fala a frase única.
  - Deixa o **Eco do Farol** (feixe que puxa inimigos).

### O que muda na cidade
- A pesca passa a existir (ponto no píer). **Barco do Severo levando a uma ilhota: (só no roteiro).**

### Mistério
- **Pago em parte:** Marisa existiu e não voltou; a lanterna do jogador é "igual" à dela.

### Mortes
Severo: *"A maré te trouxe de volta. Nunca traz ninguém."* · Nilza: *"Você tava cheirando a sal e lanterna. Lanterna acesa."* · Odete: *"Duas lanternas lá dentro agora. A sua e a dela."* / *"Vai buscar a sua."* · Benício: *"Caiu na água. Eu anotei 'voltou'. Faz com que eu esteja certo."* · Ravi: *"O Severo ficou no cais a noite toda com uma corda. Pra que corda."* · Lis: *"A lanterna que você achou chamou a sua. Eu ouvi as duas."*

### Gancho
Um homem encapuzado no Cemitério quer saber o nome do jogador.

---

## 6. Ato IV — "As Perguntas do Vigia" (selo B → A)

**Lugares:** Cemitério das Lanternas (depois da ponte leste do Pomar Alto), Ruas Baixas, praça. **Brechas:** Cemitério das Lanternas (rank B) e a **Brecha B das Ruas Baixas** (rank B, a do "Ravi em perigo").

### O que o jogador faz
1. **Benício, no Cemitério:** *"Cemitério é diferente. Lá dentro você vai ver nomes que conhece."* / *"Não para pra ler."*
2. **O Vigia** só está no banco à noite (o jogador pode **esperar a noite** no banco). Primeira visita:
   - *"Você carrega uma lanterna que já atravessou."* / *"Pergunta um: você sabia disso?"* Respostas: "Sim.", "Não.", "Não entendi." (ele lembra).
   - *"Quando um Guardião cai, às vezes deixa um pedaço. Chama Eco."* / *"Eu guardo. Você usa. Depois a gente acerta."* Libera o **1º slot de Eco**.
3. **Segunda visita (outra noite):** *"Pergunta dois: se alguém que você perdeu voltasse diferente, você aceitava?"* Respostas: "Aceitava.", "Não sei.", "Não."
4. **Ravi em perigo.** Assim que o Vigia aparece, abre uma Brecha B nas Ruas Baixas. Se o jogador não for, o jogo avisa **uma vez por véspera**: Lis (*"Abriu uma nas Ruas Baixas. Ninguém foi."*), Nilza (*"Tá rompendo lá embaixo. A mãe do Ravi fechou a janela com tábua."*), Ravi (*"Se você não for, eu vou."*). Se a Brecha rompe, Ravi entra e cai (ele **não morre**).
5. **Brecha do Cemitério** e a **Velória** (veja abaixo).
6. **Depois da Velória (Vigia):** *"Essa lanterna. Eu dei pra Odete segurar um minuto."* / *"Faz vinte anos de minuto."* O jogador pode perguntar "Qual é o seu nome?": *"Eu era a Marisa."* / *"O que saiu de lá e ficou aqui no escuro, não sei o nome."*
7. **Fecho (Benício):** *"A."* / *"Agora só falta a Torre. Eu vou pedir uma coisa antes: não toca em nada lá."* Odete, na forja: *"Não me conta onde ele fica."* / *"Se eu for lá, eu não volto."*
- Opcionais do Vigia: *"Por que capuz? Porque o rosto que eu tenho não é o que eu lembro."* · *"A Lis? A única que o Avesso nunca tocou."* / *"Ele não leva o que devolveu."* Do Benício: *"O sino? Tiramos o badalo. Pergunta outra coisa."*

### As duas rotas do Ravi
- **Rota A (jogador fechou a tempo a Brecha B):** Ravi: *"Eu ia entrar. Juro que ia."* / *"Tá. Você ganhou essa. A sopa é minha."* A aposta continua até o final.
- **Rota B (a Brecha rompeu):** na sala 3 da Brecha B (a Cozinha Velha) fica a **lanterna do Ravi** (*"A lanterna do Ravi, caída entre as panelas. Ainda acesa."*). Ao recuperar, Ravi: *"Eu entrei."* / *"Não faz mais aposta comigo, tá?"* O **placar do Ravi some**. Nilza: *"O Ravi voltou diferente. Fala menos."* / *"Eu prefiro ele falando."*
- Nenhuma é "errada"; a B é mais triste.

### A Brecha: Cemitério das Lanternas (rank B)
- O cemitério de antes do Ano Sem Sol, de cabeça para baixo.
- **Salas:** Alameda dos Ciprestes, Ossuário das Velas, Capela Virada (descanso e tesouro), Fosso dos Nomes (elite e tesouro), Altar das Mil Velas (arena).
- **Monstros:** **Carpideira** (grito em cone que empurra), **Cão-de-Vigília** (investe em linha depois de uivar), **Velinha** (cura os outros e foge).
- **Guardiã — Velória** (depois de uma caçada). Três fases:
  1. **Vigília:** atira velas que viram Velinhas; 3 candelabros quebráveis reduzem as Velinhas.
  2. **Cera:** derrete numa poça que queima e anda; ressurge onde a poça parar.
  3. **Uma vela:** a arena apaga e restam 5 velas; a certa é a que não treme quando a lanterna passa. Fala única: *"Qual delas é você?"*
  - Deixa o **Eco da Vela** (cura contínua).

### A Brecha B das Ruas Baixas (rank B)
- Mesmas salas e monstros das Ruas Invertidas (Ato VI), versão curta de 4 salas, sem Guardião.

### O que muda na cidade
- Brecha B nas Ruas Baixas. **Memorial com os nomes no Cemitério: (só no roteiro).**

### Mistério
- **Pago:** a lanterna era da Marisa e **o Vigia é a Marisa**, ou o eco dela.

### Mortes
Vigia: *"Você caiu do meu lado. Eu te empurrei de volta."* / *"Pergunta três fica pra próxima."* · Nilza: *"Um homem de capuz te deixou na porta. Não entrou."* · Odete: *"Quem te trouxe deixou um recado: 'ela ainda carrega bem'."* / *"Eu não entendi."* · Benício: *"Caiu no Cemitério. Eu olhei a lista."* / *"Seu nome não entrou."* · Ravi: *"Eu fui buscar sua lanterna. Não consegui."* / *"Desculpa."* · Lis: *"O homem do capuz chorou. Eu não sabia que eco chorava."*

### Gancho
Benício: a Torre. *"Não toca em nada lá."*

---

## 7. Ato V — "O Sino Sem Badalo" (selo A → S)

**Lugares:** Torre do Sino (depois do Cemitério, pela ponte leste). **Brecha:** Sino Mudo (rank A).

### O que o jogador faz
1. **Benício, na porta (formal):** *"Esta é a última Brecha que a Ordem controla. Abre dentro da minha casa."* / *"Eu devia ter contado antes."*
2. **A confissão, subindo até o sino sem badalo:** *"Fui eu que toquei. Faz vinte anos."* / *"A Ordem queria chamar de volta quem a gente perdeu. Veio tudo junto."* / *"Lá dentro o sino toca. Aqui fora, não."* / *"Você não vai ouvir nada. Olha."* Objetivo: **Entre na Brecha da Torre** ("Na Sala do Silêncio, olhe o chão").
3. **Caçada e Guardião: Sineiro Mudo.** Ele não tem boca; uma palavra em pedra por fase: **FESTA**, **CORDA**, **CULPA**.
4. **Depois, Benício chorou:** *"Você viu o que ele escreveu."* / *"Eu puxei a corda. Mais ninguém."* / *"O selo queimou em você no dia em que a lanterna saiu daqui."* / *"Eu tava rezando pra alguém fechar a praça. O sino ouviu."*
5. **O nome Alvor:** *"Alvor é como chamavam o primeiro raio de sol. Ninguém mais usa."* / *"O povo diz 'Deus'. Eu digo Alvor. Era o nome que a Marisa escolheu."*
6. **Selo S:** *"S."* / *"Eu não posso mais te examinar. Você já passou de mim."* Objetivo: **Desça às Ruas Baixas**.
7. **Reações:** Odete: *"O Benício me contou hoje. Vinte anos."* / *"Eu não bati nele. Fica sabendo que eu quis."* Ravi, rota A: *"S! A Nilza falou que agora você é quase deus."* / *"Deus paga sopa?"* Ravi, rota B: *"S."* / *"Fecha a das Ruas Baixas por mim. Eu não entro mais."* Lis (manhã): *"A Nilza tá cozinhando uma coisa há três dias. Disse que é pra você."*
- Opcionais do Benício: *"Por que escondi? Porque a cidade precisava de uma Ordem. Não de um culpado."* · *"O badalo tá comigo. Guardado. Não pergunta onde."*
- Severo: existe a fala *"Então não foi o farol."* (**o arquivo existe, mas não achei onde ela é ligada** no jogo).

### A Brecha: Sino Mudo (rank A)
- A Torre na noite do ritual, do lado de baixo.
- **Salas:** Escada em Espiral, Arquivo, Sala das Cordas (descanso e tesouro), **Sala do Silêncio** (a mecânica nova: sem som nenhum, só avisos visuais; entra em toda caçada), Campanário (arena).
- **Monstros:** **Sineiro** (badalada em área que atordoa), **Casca** (blindada de frente, abre o casco antes de investir), **Traça-de-Arquivo** (enxame que encolhe o raio da lanterna).
- **Guardião — Sineiro Mudo** (depois de uma caçada). Três fases:
  1. **FESTA:** badaladas em área (anéis âmbar); 2 asas quebráveis.
  2. **CORDA:** voa e mergulha (sombra âmbar), cordas descem do teto e prendem quem fica embaixo.
  3. **CULPA:** silêncio total, sem música nem som de golpe, só avisos visuais.
  - Deixa o **Eco do Sino** (atordoa em volta).

### O que muda na cidade
- **O sino aparecer no HUD como relógio da véspera: (só no roteiro).**

### Mistério
- **Pago:** quem tocou o sino foi o Benício; foi ele quem, sem saber, "escolheu" o jogador. A Ordem causou as Brechas.

### Mortes
Benício: *"Caiu na minha Torre. Eu mesmo te carreguei."* / *"A escada é longa."* · Nilza: *"O velho da Ordem te trouxe no colo. Parecia pai."* · Vigia: *"Você caiu onde o sino bate. Eu ouvi daqui."* / *"Pergunta três: você toca?"* (respostas "Toco." / "Não sei.") · Odete: *"Duas vezes no mesmo lugar. Caçador aprende ou morre."* / *"Você tá aprendendo?"* · Ravi: *"Eu não entro na Torre. Mas eu fiquei na porta."* · Lis: *"O sino sem badalo mexeu. Eu vi daqui de baixo."*

### Gancho
A Nilza cozinha há três dias uma coisa para o jogador.

---

## 8. Ato VI — "A Sopa da Véspera" (selo S → SS)

**Lugares:** Ruas Baixas (a oeste das Forjas), Forja, praça. **Brecha:** Ruas Invertidas (rank S).

### O que o jogador faz
1. **Nilza, na porta da taverna, com a tigela:** *"Minha mãe fazia isso. Eu não lembrava a receita."* / *"Lembrei quando você trouxe o peixe de olho de vidro."* / *"Toma antes de entrar. Se cair lá dentro, levanta uma vez."* / *"Só uma. Não abusa da sopa."* Isso liga a **Sopa da Véspera**, uma segunda vida por caçada.
2. **Mãe do Ravi, na casa de janela com tábuas:** *"Você é o amigo dele."* / *"Traz ele inteiro. Ou traz você. Não quero duas lanternas."* Pergunta: *"Ele falava de você todo dia. Agora fala de você e da sopa."*
3. **Celeste aparece nas Ruas Baixas**, fora do Pomar pela primeira vez: *"Você já sabe. A Lis me contou que você sabe."* / *"Eu morri no Pomar. O que voltou fui eu também. Não sei explicar melhor."* / *"O Avesso leva. Mas às vezes devolve."* / *"Me devolveu. Devolveu a Lis. Pode devolver mais."* Pergunta: *"Se eu sou real? Eu planto, colho, cuido. Isso basta pra mim."*
4. **Brecha das Ruas Invertidas** e o **Inquilino** (uma casa que anda; uma frase pela janela por fase: *"Eu morava aqui."* → *"Derrubaram pra Torre."* → *"Entra."*).
5. **Odete forja a arma lendária:** *"Eu guardei o aço dela vinte anos."* / *"Duas vésperas: a dela e a sua. Pega."* (A arma muda de nome conforme a classe: Lâmina, Arco ou Martelo "de Duas Vésperas".) Pergunta: *"Se a Marisa voltar diferente? Eu aceito."* / *"Já aceitei no dia em que ela entrou."*
6. **Benício (selo SS):** *"SS. Eu nunca escrevi isso num livro."*
7. **Reações:** Ravi, rota A: *"As Ruas Baixas tão com lampião novo. Minha mãe chorou."* Rota B: *"Minha mãe parou de fechar a janela. Obrigado."* Lis (manhã): *"A grande lanterna da praça piscou hoje de madrugada."* / *"Ela nunca pisca."*
- Nilza, em pergunta (depois de a Celeste se revelar): *"Meu marido não voltou. Mas a Celeste voltou."* / *"Eu vou aceitar o que der."*

### A Brecha: Ruas Invertidas (rank S)
- As Ruas Baixas de antes das demolições, com casas penduradas do teto.
- **Salas:** Viela dos Varais, Largo do Chafariz (elite e tesouro), Cozinha Velha (descanso), Rua que Dá a Volta, O Terreno (arena).
- **Monstros:** **Varal** (enlaça quem passa embaixo), **Despejado** (arremessa móveis), **Rasgo** (teleporta para as costas do jogador).
- **Guardião — Inquilino** (depois de uma caçada **e** da visita à mãe do Ravi). Três fases, uma frase por fase:
  1. *"Eu morava aqui."* — esmaga; 4 pernas quebráveis (cada uma deixa a casa mais lenta).
  2. *"Derrubaram pra Torre."* — atira móveis pela janela.
  3. *"Entra."* — abre a porta e puxa o jogador; o fogo fecha a arena de fora para dentro.
  - Deixa o **Eco do Lar** (abrigo de 3 s).

### O que muda na cidade
- **Ruas Baixas reformadas e tábuas tiradas da janela: (só no roteiro).** (As falas citam "lampião novo", mas não achei o cenário trocando.)

### Mistério
- **Pago:** Celeste é um eco; o Avesso devolve, não só tira. Lis também foi devolvida (a Celeste diz).

### Mortes
Nilza: *"A sopa te levantou uma vez. A segunda fui eu, carregando."* · Celeste: *"Você caiu entre as casas penduradas. Eu segurei pelo pé."* · Odete: *"A lâmina nova voltou sem você. Eu levei ela de volta lá dentro. Vai buscar as duas."* · Mãe do Ravi: *"Eu rezei pra sua lanterna. Nunca rezei pra lanterna."* · Vigia: *"Pergunta quatro: se você pudesse fechar tudo e não voltar, fechava?"* ("Fechava." / "Não.") · Benício: *"Caiu com selo SS. A Ordem não tem página pra isso. Eu fiz uma."*

### Gancho
A grande lanterna piscou. *"Ela nunca pisca."*

---

## 9. Ato VII — "A Cidade de Baixo" (selo SS → SSS)

> Este ato está na branch **atos67**, jogável e com teste automático; **ainda não foi fundido na `v2`**.

**Lugares:** praça escura, Cemitério (Vigia), Pomar Alto (Celeste), Forja, praça. **Brecha:** Avesso profundo (rank SS).

### O que o jogador faz
1. **A praça escurece ao meio-dia.** Lis: *"Tá anoitecendo às duas da tarde. Eu não tô com medo."* / *"Devia estar?"*
2. **Benício:** *"O Avesso tá crescendo. Cada lanterna perdida lá vira luz de lá."* / *"Quantas você perdeu?"* — e o jogo mostra o **número real** de Lanternas que o jogador perdeu. Objetivo: **Entre na Brecha do Avesso**.
3. **Primeira caçada no Avesso profundo.** A fenda da praça agora desce para bairros misturados.
4. **O Vigia (à noite, banco do Cemitério), a última pergunta:** *"Pergunta cinco. A última."* / *"Quando fechar, você volta? Ou fica?"* ("Volto." / "Fico."). Depois: *"Vá. Eu seguro a porta mais uma noite."* Pergunta: *"Odete sabe onde eu fico. Ela não vem. Ela tem razão."*
5. **Celeste, no Pomar, com a Lis:** *"Ela nasceu lá embaixo, na primeira manhã. Eu trouxe."* / *"O Avesso não ataca o que ele devolveu. Por isso ela anda sozinha."* Pergunta: *"Se fechar tudo, eu sumo? Talvez. Eu plantei mesmo assim."*
6. **A dupla de Guardiões** (veja abaixo) e a **sala do Avesso**:
   - *"Fica. Aqui ninguém se perde."* / *"Aqui a Marisa volta inteira."* Escolha: **Ficar** ou **Voltar**.
   - **Ficar** é um game over suave: *"Tudo escurece. Uma colher bate numa tigela."* E a Nilza: *"Acorda. Você dormiu na sopa e sonhou alto. Vai, que a Brecha não fechou."* A caçada acaba sem fechar.
   - **Voltar:** *"A Brecha fecha atrás de você."* Essa recusa é o **exame do selo SSS**.
7. **Benício, na praça (o selo SSS queima sozinho):** *"Eu não fiz isso."* / *"Você fez."* **Escolha que decide o Final:** "Quer estar lá quando o sino tocar?" (leva o Benício à decisão) ou "Descanse, Mestre."
8. **Odete (Forja):** *"Amanhã é a da praça. Eu vou junto."* / *"Não discute."* Ravi, rota A: *"Amanhã eu vou junto. Eu sei que não devo. Vou."* Rota B: *"Amanhã eu fico na porta. É o mais perto que eu chego."*
9. **Severo, na praça, com o badalo de bronze:** *"O Benício me deu isso. Disse que eu ia saber a quem dar."* / *"Eu não sei. Pega."* O jogador fica com o **badalo**.
10. **Lis, à noite, na escada:** *"Se alguém tocar o sino, eu quero estar perto."* / *"Eu acho que eu lembro do som."* **Escolha que decide o Final:**
    - "Fica perto da Ordem, então." (Lis é apresentada à Ordem), ou
    - "Ninguém precisa saber. Vem comigo." (Lis fica escondida; ela entrará com o jogador na Brecha final).
    Essa fala libera o Final.
- Opcionais: Benício: *"Eu passei o badalo pro Severo porque, se fosse eu de novo, eu não puxava."* · Nilza (arquivo existe, **não achei onde é ligado**): *"Se o sol parar de nascer de novo, eu abro a taverna no escuro. Já fiz."*

### A Brecha: Avesso profundo (rank SS)
- Abre na praça e mistura bairros.
- **Salas:** três novas (Cais dentro do Pomar, Torre no Cemitério, Cozinha no Esgoto), salas dos baralhos anteriores (Cruzamento do esgoto, Corredor do pomar, Píer do cais, Largo das Ruas), as arenas das duplas e a sala do Avesso.
- **Monstros:** **Ceifador de Pomar** (espantalho que só se move quando a lanterna **não** aponta para ele), **Sombra de Caçador** (eco de caçador caído que usa o verbo da classe de quem caça: investe com lâmina, atira com arco, bate no chão com martelo), **Rasgo**.
- **Guardiões em dupla** (depois de uma caçada e da conversa com a Celeste; a dupla é sorteada). A sala só acaba com os dois no chão, e os avisos âmbar ficam mais curtos (0,4 s):
  - Lodo-Mãe + Macieira Oca
  - Farol Afogado + Velória
  - Sineiro Mudo + Inquilino
- Cada um luta como no ato dele (fases iguais às das seções anteriores), só que mais fraco por vir acompanhado.

### O que muda na cidade
- A praça escurece e a grande lanterna pisca (o chão da praça troca de imagem durante o ato).

### Mistério
- **Pago:** o Avesso não ataca a Lis porque ela foi **devolvida**, não perdida.

### Mortes
Vigia: *"Você caiu fundo. Eu te carreguei por três bairros."* / *"Não me agradece. Fecha."* · Celeste: *"Você morreu de verdade dessa vez. O Avesso te devolveu."* / *"Ele faz isso com quem quer."* · Nilza: *"A cidade inteira te esperou acordada. Até a Odete."* · Benício: *"Caiu. Levantou."* / *"Eu parei de anotar. Eu tô só olhando."* · Lis: *"Dessa vez eu entrei pra te buscar. Nenhum bicho me viu."* · Odete: *"Perdeu a lanterna de novo. Lá embaixo tá cheio de luz sua."* / *"Vai ser difícil amanhã."*

### Gancho
A primeira Brecha, a da praça, vai abrir amanhã.

---

## 10. Final — "A Primeira Brecha" (selo SSS → Alvor)

**Lugares:** praça, praça de baixo. **Brecha:** A Primeira Brecha (rank SSS), do tamanho da praça, direto na arena. Só aparece depois do Ato VII.

### O que o jogador faz
1. **Antes de entrar** (*"Fale com quem quiser antes de entrar."*), uma última fala de cada um: Odete: *"Se ela tiver lá embaixo, eu não vou olhar. Se eu olhar, eu fico."* · Severo: *"O farol acendeu sozinho hoje. Vinte anos."* · Celeste: *"Se eu sumir, cuida da horta. Tem semente pra três anos."* · Mãe do Ravi: *"Eu dei a lanterna dele pra ele de novo. Ele é homem, decide."* Depois: *"A grande lanterna apaga. Você entra."*
2. **Na praça, com a lanterna apagando:** Benício: *"É a mesma. A primeira."* / *"Eu não vou pedir desculpa de novo. Vou segurar a porta."* Odete: *"Eu entro até a praça de baixo. Depois é você."* / *"Eu prometi pra ela que eu não entrava mais. Prometi faz tempo."*
3. **O Reflexo** (praça de baixo). Veja abaixo.
4. **Ao cair, o Reflexo:** *"Fica comigo."* / *"...Tá. Vai."*
5. **O sino de baixo, inteiro, com badalo.** *"O sino de baixo está inteiro. Com badalo."* O jogador escolhe quem toca (seção 11).
6. **O selo:** *"O sol nasce. O selo Alvor queima no seu pulso."*
7. **Epílogo, na taverna lotada.** Nilza: *"Senta. Hoje a sopa é de graça."* / *"Pra deus, pelo menos."* Benício (se ele está vivo deste lado): *"Alvor."* Nilza: *"Deus."* Ravi: *"Você venceu a aposta. Todas."* / *"Eu vou tentar o exame C amanhã. Sem medo. Jura."* **Última fala do jogo (Lis):** *"Abriu uma no mar."* / *"Quer ver por dentro antes?"* Fim da Parte 1; volta ao título.

### O Guardião: O Reflexo
- É o próprio jogador do lado de baixo. Lê as estatísticas dele: luta com as armas que o jogador mais usou (Lâmina em investida, Arco em faixas, Martelo em salto e círculo), esquiva para o lado que o jogador mais esquiva e acende **uma lanterna magenta para cada Lanterna perdida** (isso o deixa mais forte).
- **Três fases de intensidade** (a cada fase soma mais uma arma e o aviso âmbar encurta). Não tem núcleos nem crias.
- **Fala uma frase por minuto**, puxada das suas estatísticas: *"Você esquiva pra {lado}."* · *"Você escolhe {família} quando tá com medo."* · *"Você me deixou aqui {número} vezes."* · *"Eu sou o que você perdeu. Eu sou bom nisso."*
- Deixa o **Eco do Avesso** (um clone seu luta ao seu lado por 8 s).

### Mortes no Final (o Reflexo fica mais forte a cada queda)
Vigia: *"Cada vez que cai, ele acende uma a mais. Para de cair."* · Odete: *"Eu te tirei de lá. Na próxima, eu entro junto até o fim. Não discute."* · Nilza: *"Ele tá te vendo cair da mesma forma. Muda a forma."* · Lis: *"Eu fiquei do lado dele. Ele não me viu. Ele só vê você."* · Benício: *"Caiu na primeira. Levanta na primeira. A Ordem anota: Alvor em andamento."*

---

## 11. Rotas

### 11.1 Ravi (A ou B), decidida no Ato IV
| | Rota A (fechou a Brecha B a tempo) | Rota B (a Brecha rompeu) |
|---|---|---|
| Ato IV | *"Eu ia entrar. Juro que ia."* / *"Tá. Você ganhou essa. A sopa é minha."* | Ravi caiu no Avesso; o jogador recupera a lanterna dele. *"Eu entrei."* / *"Não faz mais aposta comigo, tá?"* |
| Placar de salas | Continua até o fim. | **Some.** Ravi para de competir. |
| Ato V | *"S! A Nilza falou que agora você é quase deus."* | *"Fecha a das Ruas Baixas por mim. Eu não entro mais."* |
| Ato VI | *"As Ruas Baixas tão com lampião novo. Minha mãe chorou."* | *"Minha mãe parou de fechar a janela. Obrigado."* |
| Ato VII | *"Amanhã eu vou junto. Eu sei que não devo. Vou."* | *"Amanhã eu fico na porta. É o mais perto que eu chego."* |
| Epílogo | O mesmo texto nas duas rotas. | Mesmo texto nas duas rotas (o roteiro ainda pede variações). |

### 11.2 Quem toca o sino
Aparecem só as opções que o jogador **mereceu**; tocar com o jogador está sempre disponível.

| Quem | Condição (escolhas do Ato VII) | O que acontece (segundo a lógica do jogo) |
|---|---|---|
| **Benício** | Ele disse "Quer estar lá quando o sino tocar?" | *"Eu puxo. Fui eu que soltei."* O sol nasce, o selo Alvor queima, a primeira Brecha fecha. **Benício não volta deste lado.** O epílogo é o **sem Benício**. |
| **Lis** | Escondeu a Lis da Ordem ("Ninguém precisa saber. Vem comigo.") e ela entrou na Brecha final. | *"Eu lembro do som. Deixa eu."* O sol nasce. **As duas Lumeiras ficam ligadas:** as Brechas continuam, menores. Celeste e o Vigia ficam. Gancho mais aberto para a Parte 2. |
| **O jogador** (padrão) | Sempre disponível. | *"Você puxa a corda com a lanterna da Marisa acesa na outra mão."* O sol nasce, **a primeira Brecha fecha para sempre**, quem é eco (Celeste, Vigia) fica deste lado. |

### 11.3 Epílogos que existem hoje
- **Com Benício vivo** (Lis ou jogador tocam): Nilza, Benício (*"Alvor."* / *"Deus."*), Ravi, Lis.
- **Sem Benício** (ele tocou): o mesmo, sem a troca "Alvor."/"Deus.".
- A diferença entre "Lis tocou" e "jogador tocou" aparece na **ilustração do sino** e na lógica (Brechas continuam ou não), **mas o texto do epílogo é o mesmo** nas duas.

---

## 12. Sistemas em uma página

**Bênçãos da Lanterna.** Ao limpar uma sala de combate, a lanterna oferece 3 cartas e o jogador escolhe 1; duram só até o fim da caçada. São 20 bênçãos em 5 famílias, cada uma com a cor e o nome de um personagem:
- **Chama** (âmbar, Odete): dano, crítico, partes quebram mais rápido. Brasa, Têmpera, Forja Viva, Faísca.
- **Vigília** (creme, Benício): esquiva e invencibilidade. Passo Longo, Olho Aberto, Fôlego, Couro Grosso.
- **Sopa** (verde, Nilza): vida e cura. Caldo Forte, Prato Cheio, Pão Quente, Colherada.
- **Mapa** (azul, Lis): Fragmentos, Lumes, velocidade. Bolso Fundo, Faro, Rota Curta, Mapa da Lis.
- **Aposta** (magenta, Ravi): risco e recompensa. Tudo ou Nada, Mão Pesada, Sorte do Ravi, Aposta Alta.
- Existe ainda a bênção **Teimosia** (+10% de dano até fechar a Brecha), dada quando o jogador recupera a própria Lanterna.

**Morte e Lanterna.** O jogador cai, a Lanterna fica no chão da Brecha com os Fragmentos da caçada, e ele acorda no quarto. Os Fragmentos voltam quando entra de novo numa Brecha **do mesmo bairro**. Peças e materiais nunca se perdem; rank, arma e Lumes ficam. A cada queda, na volta, **uma fala de morte nova** (seis por ato, em ordem; depois sorteia). Cada Lanterna perdida é contada, e o número aparece no Benício (Ato VII) e no Reflexo (Final). A morte também carrega perguntas do Vigia (ver Ato V e VI).

**Sopa da Nilza e Sopa da Véspera.** A sopa comum dá +10% de vida máxima na caçada. A Sopa da Véspera (Ato VI) levanta o jogador **uma vez por caçada** com metade da vida.

**Contratos.** O quadro do Benício oferece 2 (Ato I) ou 3 (demais Brechas). No Ato I são 7 tipos: Quebradeira (5 barris, caixotes ou núcleos), Entrada limpa (sem dano na sala 1), Caça aos Espectros (3), Faro de tesouro (achar a sala do tesouro), Volta inteira (90% de vida ou mais), Relâmpago (Guardião em menos de 2 minutos), Desmonte (quebrar todos os núcleos do Guardião). Nas Brechas seguintes: Caçada (fechar sem cair), Coleta (trazer itens), Proteção (sobreviver a ondas), Sem dano (uma sala sem levar golpe). Pagam XP, Fragmentos e Lumes.

**Elites.** A partir da segunda sala de combate, 30% de chance de aparecer uma versão maior de um monstro com **um** modificador visível: Blindado (leva metade do dano), Veloz, Faminto (saque em dobro) ou Gêmeo (vem em dupla). Muito mais vida e dano; dão mais XP e material.

**Salas da Brecha.** Cada caçada sorteia 5 a 7 salas: combate, elite, tesouro (baú), descanso (lanterna que cura metade da vida) e, no fim, o Guardião. Nunca o Guardião antes do número de caçadas exigido pela Brecha.

**Forja e armas (Odete).** Três armas, cada uma com nome próprio por rank (exemplo da Lâmina: Enferrujada, da Forja, do Pomar, da Maré, de Vela, do Sino, de Duas Vésperas, do Avesso, do Alvor). Cada rank tem 2 níveis de melhoria, pagos com Fragmentos mais materiais do rank (e de Guardião na troca de degrau). Trocar de arma custa Fragmento. Os Guardiões deixam material garantido e 30% de chance de peça de conjunto (hoje só existe o **Conjunto da Lodo-Mãe**: Capuz de Louça Trincada, Peitoral de Talheres, Luvas de Gel Seco, Botas de Lodo Curtido). O saque de uma Brecha de rank R é 80% do rank R, 15% do seguinte e 5% de peça rara.

**Ecos.** Um por Guardião: Viscoso (Lodo-Mãe), da Semente (Macieira), do Farol, da Vela, do Sino, do Lar (Inquilino) e do Avesso (Reflexo). O Vigia libera o primeiro slot (Ato IV).

**Ranks (selos).** Nomes: E Aprendiz, D Vigia, C Lanterneiro, B Sentinela, A Caçador, S Arauto, SS Crepuscular, SSS Véspero, Alvor. Para subir: XP do rank seguinte **e** vencer o Guardião do rank atual **e**, nos selos C, B, S e SSS, passar o exame da Ordem. Cada rank soma vida máxima. O visual do personagem muda em 5 estágios (E-D, C-B, A-S, SS-SSS, Alvor).

**Outros.** Dia, véspera e noite (a Brecha abre à véspera; o Vigia só aparece à noite); página **Perguntas** no Diário (9 linhas, uma por mistério); tela de resultado ao fechar a Brecha com uma linha de personagem sorteada (ex.: Nilza: *"Chegou inteiro. Milagre."*, Benício: *"Voltou com a lanterna acesa. É o que importa."*) e o placar de salas contra o Ravi.

---

## 13. O que ainda falta / diferenças entre o roteiro e o jogo

Lista honesta. "Não achei" significa que procurei nos dados e no código e não encontrei; vale confirmar jogando.

**Mecânicas e sistemas do roteiro**
- **Horta** do Ato II (6 canteiros, sementes que a Celeste entrega) e **feira da manhã / Nilza vendendo receitas**: não achei. A Celeste não entrega sementes no jogo.
- **Modificadores de véspera** (Chuva, Lua cheia, Névoa, Silêncio) e a fala da Lis de manhã avisando: não achei.
- **Bênçãos:** o roteiro pede 6 por família no Ato I e **3 novas por ato**, mais **uma combinação entre famílias por ato**. O jogo tem 4 por família (20 no total), sem novas por ato e sem combinações. O Ato VII pede ainda "bênçãos combinadas de 3 famílias": não achei.
- **Conjuntos de Guardião:** o roteiro pede 4 peças por Guardião, com efeito em 2 e em 4 peças. Só a Lodo-Mãe tem conjunto.
- **2º slot de Eco** (Ato V): a configuração prevê 2 slots, mas só achei o evento que libera o 1º (Vigia, Ato IV).
- **Sino no HUD como relógio da véspera** (Ato V): não achei.
- **Barco do Severo levando a uma ilhota com Brecha opcional** (Ato III): não achei.
- **Memorial com os nomes** no Cemitério (Ato IV) e a **lanterna do Ravi no memorial**: não achei (a lanterna dele existe, mas na Brecha B).
- **Ruas Baixas reformadas, tábuas tiradas da janela e lampiões novos** (Ato VI): as falas dizem, o cenário não parece mudar. A mudança visível que existe é: lampiões e banca da Lis (Ato I), carroça de maçãs (Ato I-II) e praça escura (Ato VII).
- **Ataque combinado das duplas de Guardiões** (ex.: o feixe do Farol puxando para a poça da Velória): não achei; a dupla só luta junta, com avisos mais curtos.
- **Perguntas do Vigia citadas no Final**: as respostas ficam salvas, mas as falas do Reflexo não as usam.
- **Rosto do Vigia no Ato VII** (a mulher jovem com os olhos da Odete): o diálogo não descreve, e não confirmei a arte.
- **Cena silenciosa Odete × Vigia** (final do jogador): não achei uma cena própria.
- **Epílogo diferente para Lis e para o jogador**: o texto é o mesmo (muda só a ilustração do sino).
- **Odete entrando até a praça de baixo**: só há a fala; não achei acompanhante em combate.
- **Ravi "controlado pela IA" como tutorial do online** e todo o **modo online / Vigília de 4 / Casas** (Parte 3 do roteiro): nada disso existe.
- **A palavra "Deus"** aparece só em poucas falas (Nilza e Ravi); o resto do povo e das Ruas Baixas não a usa.

**Falas e arquivos**
- **Falas do roteiro que não estão no jogo:** Benício no Ato II (*"Pomar é selo D. Você tem D."*) e a fala da Celeste que entrega as sementes.
- **Arquivos de fala que existem mas não achei ligados a nada:** Severo no Ato V (*"Então não foi o farol."*) e Nilza no Ato VII (*"Se o sol parar de nascer de novo..."*).
- **Falas de morte:** as seis de cada ato seguem o roteiro, uma por queda e cada uma só uma vez. O "depois da sexta, sorteia" do roteiro eu não verifiquei.
- **Exames da Ordem:** o jogo tem duas descrições. O arquivo de progressão descreve exames objetivos (C: sobreviver 90 s; B: atravessar o Cemitério; S: atravessar as Ruas Invertidas com no máximo 3 golpes; SSS: derrotar 40 ecos com no máximo 6 golpes), enquanto a história usa o Benício esperando na saída (C, B) e a recusa da oferta do Avesso (SSS). Não confirmei como as duas camadas se encaixam na prática.
- **Selo D e outros:** o Benício "queima o selo" por evento nos Atos III a VII; no Ato I e II o selo depende só da progressão (XP). Se o jogador chegar ao fecho sem XP suficiente, o Benício pede para caçar mais.

**Estado das branches**
- O **Ato VII** está pronto na branch `atos67` e **precisa ser fundido na `v2`**; sem a fusão, o Final (que depende da flag que o Ato VII liga) não é alcançável pela história na `v2`.
