# VÉSPERA — Documento de Design (GDD v0.1)

Complementa o `CLAUDE.md`. Aqui vive o *porquê* do jogo: mundo, personagens, monstros e sistemas. Tudo é proposta inicial para o time discutir; o que for aprovado vira `Resource` em `src/Data`.

---

## 1. Premissa

Há vinte anos o sol se pôs sobre Lumeira e não voltou por um ano inteiro. Os registros chamam esse período de **o Ano Sem Alvor**. Foi quando as primeiras **Brechas** se abriram: rasgos no ar que dão para o **Avesso** — uma Lumeira invertida, feita de tudo que a cidade perdeu, esqueceu ou enterrou. O que vive lá não é exatamente monstro: é *resíduo*. Ecos de coisas que já foram.

A cidade só sobreviveu porque as pessoas mantiveram as lanternas acesas. Descobriram que a luz de uma lanterna alimentada com **Fragmentos** — pedaços cristalizados do Avesso — fecha uma Brecha. Quem entrava para buscar Fragmentos virou **caçador**. Quem organizava os caçadores virou a **Ordem da Lanterna**. E quem subisse alto o bastante, dizia a lenda, poderia fazer o sol voltar de vez: o rank **Alvor**.

O sol voltou sozinho depois de um ano. As Brechas, não. Elas abrem toda véspera, menores, previsíveis, quase rotina. Lumeira aprendeu a viver com isso. Mas ninguém jamais chegou ao Alvor, e a Ordem suspeita que o Avesso está crescendo.

**O jogador** é um recém-chegado com um selo E queimado no pulso e uma lanterna velha herdada de alguém que não conheceu.

### Os temas do jogo
- **Luz contra esquecimento.** O Avesso é feito de memória perdida; cada monstro é algo que alguém deixou para trás.
- **Comunidade.** A cidade é frágil e bonita; o jogador não é um herói solitário, é a razão de a padaria abrir amanhã.
- **Subir é um fardo.** Cada rank marca a pele e aproxima o caçador do Avesso. Os ranks mais altos começam a *ouvir* as Brechas.

---

## 2. Lumeira — os bairros

Cada bairro tem NPCs, funções no hub, um Guardião próprio e um tema de Brecha. Pensar cada bairro como uma "fase" de Stardew: um lugar que o jogador quer visitar todo dia por motivos além de combate.

| Bairro | O que é | Função no hub | Tema da Brecha | Guardião |
|---|---|---|---|---|
| **Praça da Lanterna** | Centro da cidade, a grande lanterna de bronze que nunca apaga | Spawn, quadro de contratos, ranking público | — (a praça nunca abre Brecha… até o final do jogo) | **O Reflexo** (final) |
| **Bairro das Forjas** | Ruas estreitas, fumaça, oficinas | Forja da Odete, armaduras, melhoria de arma | *Esgotos do Mercado* (galerias sob a cidade) | **Lodo-Mãe** |
| **Pomar Alto** | Colina de macieiras e hortas; o lado Stardew do jogo | Horta do jogador, herbalista, colheita sazonal | *Pomar Podre* | **A Macieira Oca** |
| **Mercado das Marés** | Cais, peixaria, barcos, névoa | Pesca, taverna do porto, comércio de materiais | *Cais Afogado* | **O Farol Afogado** |
| **Cemitério das Lanternas** | Colina de túmulos, cada um com uma lanterna | Memorial dos caçadores caídos, Ecos, o Vigia | *Cemitério das Lanternas* | **A Velória** |
| **Torre do Sino** | Sede da Ordem; o sino que marcava as vésperas e emudeceu no Ano Sem Alvor | Exames de rank, arquivo/bestiário, missões da Ordem | *Torre do Sino Mudo* | **O Sineiro Mudo** |
| **Ruas Baixas** | Bairro pobre, casas de madeira, varais, música | Taverna da Nilza, comida e buffs, rumores, Ravi mora aqui | *Ruas Invertidas* | **O Inquilino** |

A ordem acima é também a ordem natural de progressão (E → S). A praça abre a última Brecha quando o jogador atinge SSS.

---

## 3. Personagens

Cada NPC tem: função mecânica, arco de relação (0–5 lanternas, análogo aos corações de Stardew) e um segredo que se revela no nível 4–5. Os segredos ligam todos ao Ano Sem Alvor.

### Odete — a ferreira (Bairro das Forjas)
Cinquenta e poucos anos, braços de quem bate ferro há décadas, fala pouco e nunca elogia. Foi caçadora rank A. Parou no dia em que sua parceira de caça, **Marisa**, não voltou de uma Brecha no Cais. A lanterna de Marisa nunca foi recuperada.
- **Função:** melhora armas e forja armaduras a partir de partes de monstro. Quanto maior a relação, mais receitas e desconto.
- **Arco:** começa cobrando caro e desconfiando. Ao nível 3 pede que o jogador procure a lanterna de Marisa no *Cais Afogado*. Ao nível 5, forja a **Lâmina de Duas Vésperas**, única arma lendária feita à mão no jogo.
- **Segredo:** a lanterna que o jogador herdou era dela — Odete a deixou na Ordem para "quem precisasse" no dia em que pendurou a arma.

### Mestre Benício — Guardião do Sino (Torre do Sino)
Velho, formal, cego de um olho, memória prodigiosa. Dirige a Ordem da Lanterna e aplica os exames de rank. Sabe o nome de todos os caçadores que morreram.
- **Função:** exames de rank, contratos oficiais, bestiário (compra informação sobre monstros cujas partes o jogador quebrou).
- **Arco:** avalia o jogador friamente. Ao nível 4 revela que a Ordem esconde o tamanho real do Avesso para não causar pânico.
- **Segredo:** tocou o sino pela última vez no início do Ano Sem Alvor — e acredita que foi o som que abriu a primeira Brecha.

### Tia Nilza — a taverneira (Ruas Baixas)
Mãe de todo mundo. Cozinha com o que o bairro tem, sabe tudo antes de todos, canta enquanto frita.
- **Função:** comida que dá buffs temporários (HP, dano, velocidade, visão na escuridão). Ingredientes vêm da horta, da pesca e de drops. Quadro de rumores: dica de onde a próxima Brecha vai abrir.
- **Arco:** amizade rápida; ao nível 3, pede ingredientes do Avesso para um prato "que a mãe dela fazia e ninguém lembra a receita". Ao nível 5 ensina a **Sopa da Véspera**, que permite uma segunda vida em uma Brecha.
- **Segredo:** cozinhar com Fragmentos deixa a comida mais gostosa e ela sabe que não deveria.

### Ravi — o rival (Ruas Baixas)
Dezoito anos, rank E como o jogador, falastrão, otimista, péssimo em combate no começo. Quer ser caçador para tirar a família das Ruas Baixas.
- **Função:** rival e espelho de progresso. Sobe de rank junto com o jogador (sempre um passo atrás). Aparece em algumas Brechas como aliado controlado por IA. **Base do co-op na fase online.**
- **Arco:** cresce de alívio cômico a parceiro confiável. Ao nível 4 morre em uma Brecha B *se o jogador não a tiver limpado a tempo* (mecânica de ruptura, seção 5.4) — e o jogador precisa recuperar a lanterna dele.
- **Segredo:** ouve as Brechas antes de abrirem, como Lis. Não conta a ninguém porque acha que é defeito.

### Dona Celeste — a herbalista (Pomar Alto)
Idade indefinível, fala com as plantas, vende poções que funcionam e não explica por quê. Cuida da única macieira que dá frutos fora de estação.
- **Função:** poções, sementes, melhoria da horta do jogador. Identifica **Ecos**.
- **Arco:** cordial e distante. Ao nível 3 revela que o Pomar Podre é uma cópia do pomar como era antes do Ano Sem Alvor. Ao nível 5 confessa que *ela* é do Avesso: a Celeste original morreu no Ano Sem Alvor e o que saiu da Brecha foi um eco tão completo que nem a Ordem percebeu. Ela é a prova de que o Avesso pode devolver, não só tirar.

### Capitão Severo — mestre do cais (Mercado das Marés)
Barbudo, mal-humorado, cheira a sal. Único que ainda pesca na névoa.
- **Função:** minigame de pesca (peixes comuns de dia; peixes do Avesso na névoa da véspera, usados em receitas e armaduras), barco para ilhas futuras, comércio de cordas, redes e óleo de lanterna.
- **Arco:** respeita apenas quem volta do *Cais Afogado*. Ao nível 4 pede ajuda para enterrar os afogados que ainda sobem na maré.
- **Segredo:** era o faroleiro. O Farol Afogado é o eco do farol que ele deixou apagar na primeira noite.

### Lis — a batedora (Praça da Lanterna)
Onze anos, pés descalços, vende mapas desenhados à mão e sempre sabe onde a Brecha vai abrir antes da Ordem.
- **Função:** vende mapas que revelam o layout da próxima Brecha (uma vez por dia). Mini-quests de "achar coisas perdidas" na cidade.
- **Arco:** vai de pivete a aprendiz. Ao nível 5, o jogador pode escolher apresentá-la à Ordem ou escondê-la — e isso muda quem toca o sino no final.
- **Segredo:** nasceu no Ano Sem Alvor. É a única pessoa que nunca viu o sol *não* voltar — e a única que o Avesso parece ignorar.

### O Vigia (Cemitério das Lanternas)
Encapuzado, nunca mostra o rosto, só aparece ao anoitecer. Vende informação sobre Ecos e troca Fragmentos por memórias.
- **Função:** mercador de Ecos (fase 2); permite "reescrever" um Eco absorvido; revela a história de cada Guardião.
- **Arco:** não tem relação; tem *perguntas*. Cada visita ele faz uma pergunta sobre o jogador e lembra das respostas.
- **Segredo:** é a Marisa, parceira de Odete. Ou o eco dela. Nem ela sabe mais a diferença.

---

## 4. Bestiário

Princípios:
1. **Todo monstro é um eco de algo humano.** O design visual deve deixar isso legível: um Lodo tem fragmentos de louça; um Espectro tem a silhueta de um casaco.
2. **Toda criatura tem 1–3 partes quebráveis** que mudam seu comportamento e dão materiais específicos (DNA Dauntless). Quebrar é sempre melhor que só matar.
3. **Fraqueza à luz.** Habilidades de lanterna (fase 2) e o âmbar da forja afetam todos; monstros de rank alto resistem.
4. Cada monstro tem **um verbo**: Lodo *pula*, Espectro *persegue*, Rato *enxameia*, Sineiro *atordoa*. Um verbo por inimigo mantém o combate legível.

### Monstros comuns

| Nome | Rank | Eco de… | Comportamento (verbo) | Partes quebráveis → drop | Fraqueza |
|---|---|---|---|---|---|
| **Lodo** | E | Restos de comida e louça jogados nos esgotos | *Pula* em direção ao jogador a cada 1,2 s | Núcleo (1) → *Gel de Lodo* | Fica lento no fogo |
| **Espectro** | E | Vozes que ninguém mais chama | *Persegue* flutuando, atravessa obstáculos | Véu (1) → *Véu de Espectro* | Luz direta dissipa por 2 s |
| **Rato-Lanterna** | D | Ratos que comeram Fragmentos | *Enxameia*: 4–6 juntos, fogem da luz | Fragmento engolido (1) → *Fragmento Mordido* (Fragmento de qualidade menor) | Área de efeito |
| **Carrapicho** | D | Plantas arrancadas das hortas | *Agarra*: raízes prendem o jogador 1 s | Caule (1), Bulbo (1) → *Espinho*, *Semente Podre* | Fogo e corte |
| **Afogado** | C | Marinheiros que a maré não devolveu | *Avança* lento e pesado; dano alto | Capacete (1), Corda (1) → *Bronze Salgado*, *Corda Encharcada* | Lento: dê a volta |
| **Sineiro** | C | Gárgulas da Torre | *Atordoa*: badalada em área a cada 5 s | Sino (1), Asas (2) → *Bronze Mudo*, *Pedra de Gárgula* | Interromper a badalada quebrando o sino |
| **Velinha** | B | Velas acesas por quem ficou esperando | *Cura* outros monstros; foge | Pavio (1) → *Pavio Eterno* | Priorize-a |
| **Ceifador de Pomar** | B | Espantalhos | *Vigia*: só se move quando o jogador não olha | Cabeça (1), Foice (1) → *Palha Negra*, *Lâmina Enferrujada* | Encarar e recuar |
| **Casca** | A | Tartarugas do rio e portas trancadas | *Bloqueia*: imune de frente | Casco (3 placas) → *Placa de Casca* (armadura) | Costas |
| **Rasgo** | S | A própria Brecha | *Teleporta*: desaparece e reaparece atrás | Borda (2) → *Fio do Avesso* (material lendário) | Atacar o ponto de chegada, não o de saída |

### Guardiões (bosses)

Cada Guardião tem **três fases**, um **padrão de telegrafia** claro (0,6 s de aviso em âmbar antes de todo golpe forte), **partes quebráveis** que removem ataques, e deixa um **Eco**.

**Lodo-Mãe** (E · Esgotos do Mercado)
Massa gelatinosa com louça, talheres e brinquedos presos dentro. Eco de tudo que Lumeira jogou fora.
Fase 1 pula e esparrama Lodos; fase 2 engole o jogador por 2 s se não desviar; fase 3 se divide em três. Partes: 3 núcleos (cada um quebrado reduz o número de divisões).
*Eco Viscoso:* seu dash deixa um rastro que lentifica inimigos.

**A Macieira Oca** (D · Pomar Podre)
Macieira gigante, oca, com um rosto de casca. Eco do pomar antes do Ano Sem Alvor — ainda dá frutos, podres.
Fase 1 lança maçãs que explodem; fase 2 raízes emergem onde o jogador está; fase 3 a árvore "anda". Partes: 2 galhos (reduzem o número de maçãs), 1 rosto.
*Eco da Semente:* planta uma muda que atira espinhos por 10 s.

**O Farol Afogado** (C · Cais Afogado)
Faroleiro de bronze, três metros, a lanterna do farol no lugar da cabeça. Eco do farol que Severo apagou.
Fase 1 feixe de luz que *puxa* o jogador; fase 2 invoca Afogados da maré; fase 3 a arena inunda e a luz apaga — o jogador só vê o que a própria lanterna ilumina. Partes: lente (1, remove o puxão), ombreiras (2).
*Eco do Farol:* feixe que puxa inimigos para um ponto. Combina com Eco Viscoso.

**A Velória** (B · Cemitério das Lanternas)
Colosso feito de velas derretidas, centenas de chamas. Eco de todas as vigílias feitas por quem não voltou.
Fase 1 lança velas que viram Velinhas; fase 2 derrete e vira poça de cera que queima; fase 3 apaga todas as velas menos uma — acertar a certa encerra. Partes: 3 candelabros.
*Eco da Vela:* cura contínua por 6 s; seu rank fica um nível abaixo durante o efeito (a luz tem preço).

**O Sineiro Mudo** (A · Torre do Sino Mudo)
Gárgula do tamanho do sino, sem boca. Eco da última badalada.
Fase 1 badaladas em área que atordoam; fase 2 voa e mergulha; fase 3 o silêncio: todo som do jogo some e os golpes só são telegrafados visualmente. Partes: asas (2), sino (1).
*Eco do Sino:* atordoa tudo ao redor por 2 s. Cooldown longo.

**O Inquilino** (S · Ruas Invertidas)
Uma casa das Ruas Baixas que anda em pernas de madeira. Eco de todas as casas que foram demolidas para construir a Torre.
Fase 1 esmaga; fase 2 abre a porta e puxa o jogador para dentro (sub-arena); fase 3 pega fogo. Partes: pernas (4).
*Eco do Lar:* cria um abrigo de 3 s onde o jogador é invulnerável.

**O Reflexo** (SSS → Alvor · Praça da Lanterna)
O jogador. Mesma arma, mesmos Ecos, mesmo rank, movimentos espelhados. Eco de tudo que o jogador deixou para trás nas Brechas — cada Lanterna não recuperada no jogo inteiro dá a ele um buff.
Não tem fases: aprende. Usa contra o jogador os padrões que o jogador mais usou (registrar estatísticas de combate desde o início do jogo para isso).
*Eco do Avesso:* um clone seu luta ao seu lado por 8 s. Só pode ser absorvido uma vez.

Derrotar o Reflexo concede o rank Alvor e o final: o sino toca de novo — por Benício, por Lis ou pelo jogador, dependendo das escolhas.

---

## 5. Sistemas

### 5.1 Ciclo do dia e a véspera
Dia de 20 minutos reais: **manhã** (cidade, comércio, horta, pesca) → **tarde** → **véspera** (a Brecha do dia abre; luz âmbar; música muda) → **noite** (Brecha aberta, perigo nas ruas se houver ruptura). Dormir na casa do jogador pula para a manhã e salva. Estações de 7 dias cada mudam monstros, peixes, colheita e a paleta.

### 5.2 Combate
Top-down, ação direta. Jogador tem **ataque leve**, **ataque pesado** (carregado), **esquiva** com invencibilidade curta (0,25 s) e **lanterna** (fase 2: cone de luz que revela, dissipa Espectros e aplica fraquezas). Barra de fôlego pequena só para esquivas, não para ataques.

Armas definem o estilo (sem classes):

| Arma | Alcance | Ritmo | Pesado | Fantasia |
|---|---|---|---|---|
| Lâmina | médio | 3 golpes | corte giratório | o padrão, equilibrado |
| Martelo | curto | lento | quebra partes com bônus | o quebrador |
| Arco | longo | médio | flecha de luz perfurante | o caçador |
| Cajado | médio | lento | explosão de âmbar em área | o lanterneiro |
| Adagas | muito curto | rápido | teleporte-golpe | o rasgo |

Partes quebradas dão **material específico**; o Martelo quebra mais rápido, as Adagas acertam partes traseiras.

### 5.3 Progressão
- **XP de caçador** sobe o rank conforme a tabela (`RankTable`). Cada rank: +20 HP máximo, novo selo, acesso a Brechas do rank seguinte.
- **Exame de rank** (C, A, S e SSS): Brecha especial da Ordem sem loot, só objetivo — testa a habilidade, não o equipamento. DNA Solo Leveling.
- **Armas** melhoram na forja com Fragmentos + materiais; **armaduras** vêm de partes de Guardiões (conjunto por Guardião, com bônus de 2 e 4 peças).
- **Ecos** (até 2 equipados, fase 2): slots raros, habilidades ativas com cooldown.
- **Relações** com NPCs (0–5 lanternas) desbloqueiam receitas, descontos, quests e segredos.
- **Bestiário**: cada parte quebrada preenche uma página; completar um monstro dá bônus de dano permanente contra ele.

### 5.4 A cidade reage
- **Ruptura:** uma Brecha ignorada por 3 vésperas rompe. Monstros do tema saem para o bairro, o NPC dali fecha, preços sobem 30%. Limpar a ruptura reabre tudo e dá bônus de relação com o bairro inteiro.
- **Prosperidade:** bairros com Brechas limpas consecutivas ganham melhorias visíveis (lanternas novas, barracas, música). Ligar à relação coletiva do bairro.
- **Casas** (guildas, fase online): adotar um bairro, brasão nas ruas, desconto e responsabilidade.

### 5.5 A Lanterna (morte)
Morrer dentro da Brecha devolve o jogador à casa dele na manhã seguinte, sem os Fragmentos daquela caçada: eles ficam na **Lanterna**, no ponto onde caiu. A Lanterna aparece na próxima Brecha do mesmo bairro. Se outra Brecha romper antes, a Lanterna some para o Avesso — e alimenta O Reflexo. No co-op, qualquer caçador pode trazer a Lanterna de um amigo.

### 5.6 Lado Stardew
Horta (6 canteiros no Pomar Alto, cresce por dia; ingredientes para a Nilza e a Celeste), pesca (Severo), culinária (buffs para a caçada), decoração da casa com troféus de Guardiões, festivais sazonais na Praça (Festa das Lanternas, Colheita, Noite do Sino, Primeira Véspera).

### 5.7 Economia
Duas moedas: **Fragmentos** (vêm só do Avesso, usados na forja e nos Ecos) e **Lumes** (moeda da cidade, vêm de vender materiais, peixe e colheita, usados em comida, sementes, mapas e mobília). Impede que a cidade seja "só uma loja de upgrade".

### 5.8 Co-op (fase 2, decidir com calma)
Até 4 caçadores na mesma Lumeira; Brechas são salas instanciadas. Loot individual, XP compartilhado, Lanternas recuperáveis por qualquer um. Ravi vira o tutorial de co-op.

---

## 6. Direção de arte e som

- **Pixel art 32×32**, personagens 32×48, 4 direções, 6 frames de andar, 4 de ataque, 2 de idle. Bosses 96×96 a 192×192.
- **Iluminação 2D real**: cada lanterna da cidade é uma `PointLight2D`; a véspera é um `CanvasModulate` âmbar → índigo; dentro das Brechas, apenas a lanterna do jogador e a luz-de-brecha magenta.
- **Monstros legíveis**: silhueta primeiro, detalhe depois; partes quebráveis destacadas com borda creme.
- **Som**: cidade com som diegético (sino que não toca, mar, martelo da Odete); Brechas com reverb e a música da cidade tocada *ao contrário*, abafada. Cada Guardião tem um motivo que depois vira parte da trilha da cidade quando derrotado.
- **UI** sempre em creme sobre índigo translúcido, bordas de 2 px, sem gradientes.

---

## 7. O que falta decidir (perguntas para o time)
1. Dia de 20 min real é bom ou cansa? Alternativa: a véspera só avança quando o jogador quer (ir dormir).
2. Esquiva com fôlego ou ilimitada?
3. Permadeath de Lanterna (seção 5.5) é duro demais para o MVP? Pode começar sem o "some para o Avesso".
4. O final com escolha (Benício / Lis / jogador) exige rastrear decisões desde cedo — vale o custo?
5. Quantos Guardiões no lançamento: 3 (E, D, C) ou todos os 7?

---

## 8. Lista de conteúdo do MVP (v0.1), derivada deste documento
- Bairros: Praça, Forjas, Ruas Baixas (3 telas).
- NPCs: Odete, Nilza, Ravi (sem sistema de relação ainda; só diálogo e função).
- Monstros: Lodo, Espectro. Guardião: Lodo-Mãe com 3 fases simplificadas.
- Arma: Lâmina, 3 níveis. Esquiva. Sem Ecos.
- Ranks E → D → C. Lanterna simples (sem sumir para o Avesso).
- Ciclo de dia simples (manhã/véspera). Sem estações, horta ou pesca.
