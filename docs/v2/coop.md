# Co-op local (camada 8) e o caminho para o online

Escrito em 23/10/2026 junto com a branch `coop-local`. Complementa o [PLANO-V2](PLANO-V2.md) §3/§4 (camada 8) e o
GDD §5.8 (até 4 caçadores, saque individual, XP compartilhado, Lanternas recuperáveis por qualquer um).

## 1. O que existe hoje (2 jogadores, mesmo PC)

| Tema | Como funciona | Onde |
|---|---|---|
| Entrar | Título → **Co-op local**: cada jogador aperta um botão no próprio dispositivo (teclado, metade direita do teclado, controle 1, controle 2), escolhe a classe com a prévia e confirma. Depois escolhe o espaço de save (o do J1). No meio do jogo: pausa → **Chamar J2 (co-op)** / **J2 sai do co-op** (só fora da Brecha). | `UI/Coop/CoopJoinScreen.cs`, `JoinCard.cs`, `PauseScreen.cs`, `SlotScreen.cs` |
| Teclas | J1 e J2 têm tabelas próprias em `user://bindings.json` (`actions` e `player2`), remapeáveis em Controles (botão "Teclas do Jogador 1/2"). Teclado dividido: J1 lê só a coluna Principal (WASD, J, K, Espaço, E, Q, F, L), J2 lê setas + teclado numérico (Num 1 ataque, Num 2 pesado, Num 0 esquiva, Num 3 habilidade, Num Enter/Enter interagir, Num * pausa). Controles: cada um lê só o próprio índice. | `Boot/BindingTable.cs`, `InputSetup.cs`, `LocalInputSource.cs`, `Coop/CoopSession.cs` |
| Comando | O caçador nunca lê `Input`: lê uma `IInputSource`. No co-op ela é uma `CommandInputSource`, que pede **um `PlayerCommand` por passo de física** a um `ICommandProvider` (local: `LocalCommandProvider` amostra o dispositivo). | `Core/Coop/PlayerCommand.cs`, `Coop/CommandInputSource.cs` |
| Caçadores | J2 nasce ao lado do J1, contorno na cor do jogador (J1 âmbar, J2 ciano), etiqueta "J1/J2"; mesma classe → capa do J2 recolorida. Sem colisão entre eles, só um empurrão leve. | `Actors/Player.cs`, `Combat/HitFlash.cs`, `World/LevelWorld.cs`, `Coop/CoopOverlay.cs` |
| Cair e reanimar | Na Brecha, cair vira **Caído**. O aliado de pé segura interagir perto por 2 s (anel de progresso) e ele levanta com 40% da vida. Todos caídos → regra da Lanterna para a caçada inteira (os Fragmentos de cada um ficam na Lanterna dele) e todos acordam na Praça. | `Core/Coop/ReviveRules.cs`, `Coop/PartyDirector.cs`, `World/Level.Coop.cs` |
| Câmera | Compartilhada: centra no grupo e abre o zoom só o necessário, até o piso que mantém o caçador com **≥ 85 px** na tela 1080p (regra solo: 110 px). Passado o piso, a **coleira** segura quem tenta sair (borda invisível relativa aos outros; puxão suave e limitado se alguém já estiver fora). Respeita os limites do mapa e a apresentação do Guardião. | `Core/Coop/CoopFraming.cs`, `World/CameraRig.cs` |
| Escala | `data/coop.json`: vida dos monstros ×1,5 (2 jogadores), ×2 (3), ×2,5 (4); +1 monstro por onda por jogador extra; dano do Guardião ×1,15 (não linear); XP cheio para cada um; saque individual (um sorteio por jogador, cristais só voam para o dono e brilham na cor dele). | `Core/Coop/CoopRules.cs`, `CoopLoot.cs`, `Actors/Enemies/Enemy.cs`, `World/Rift/*` |
| HUD | Painel do J1 à esquerda, do J2 à direita (espelhado): vida, fôlego, classe, rank e Fragmentos de cada um; barra do Guardião igual. O quadro do objetivo desce para baixo do painel do J2. | `UI/CombatHud.cs`, `Story/StoryHud.cs` |
| Save | J1 é o dono do espaço (história + progressão). J2 é o **convidado** (`SaveData.Guest`: nome, classe, `HunterProgress` próprio com rank, arma, peças, Fragmentos, Lanterna), no mesmo arquivo. As flags da história só existem no save do J1. Campo opcional da versão 3 (saves antigos carregam com `guest` nulo). | `Core/Coop/GuestHunter.cs`, `Core/Save/SaveData.cs`, `Coop/PartyProgression.cs` |
| Conversa | Qualquer jogador puxa conversa; todos ficam parados; a caixa mostra "J2 · conversando" e o botão de interagir de cada um avança. | `World/Level.Story.cs`, `UI/Dialogue/DialogueBox.cs` |

Testes: `tests/Core/Coop/*` (escala, saque/XP/Lanterna por jogador, reanimação, entrada, comando serializado, câmera e
coleira, convidado no save) e o bot `--coop-bot` (entra pela pausa com o controle 2, caça a Brecha E com duas fontes
simuladas, cai e é reanimado, vence a Lodo-Mãe, confere saque e save, cai em grupo e recupera as Lanternas, sai pela pausa).

### 1.1 Depois do merge na v2 (12/11/2026)

- Classe: o J1 usa a arma equipada do save (na v2 a classe é escolhida uma vez, na Odete); a escolha do cartão do J1
  na tela de entrada vinda do título não muda a arma dele. O J2 usa a classe da entrada.
- Caçada sorteada (`HuntService`): uma caçada para o grupo; contratos, poções e bônus da tela de resultado são do J1.
  Bênçãos da lanterna valem para todos (todos recalculam a vida máxima). Descanso e "Prato Cheio" curam todos de pé.
  A segunda vida da Sopa levanta quem cair primeiro (no lugar da reanimação).
- Suporte de arma da 1ª sala: quem interage pega; a troca do J2 vale até a volta (`CoopSession.SetRunHunter`).
- Brechas novas: a água do Farol, a corda do Sineiro Mudo, o atordoamento do Sineiro e a luz da lanterna no escuro
  (Traça-de-Arquivo, Velória) valem para cada caçador; elites mantêm o ×vida do elite em cima da escala do co-op.
- Portal da Praça em grupo: entra na Brecha escolhida pela zona (Esgotos ou Avesso profundo); a Primeira Brecha do Final
  entra pela fala `final/antes`, com o grupo.

## 2. Online (host autoritativo com ENet)

**Modelo:** um jogador é o **host** e roda a simulação inteira (é o mesmo jogo de hoje). Os clientes só mandam
comandos e desenham o que o host manda. Nada de lógica de jogo no cliente além de previsão do próprio movimento.

### 2.1 Entrada (cliente → host)

- O cliente amostra o próprio dispositivo com o `LocalCommandProvider` (o mesmo de hoje) e envia o `PlayerCommand`
  serializado (`PlayerCommand.Write`, 13 bytes: tick, slot, direção quantizada, botões segurados e recém-apertados)
  por `ENetMultiplayerPeer`, canal não confiável e sequenciado, a cada passo de física (60/s).
- No host, cada cliente vira um `NetworkCommandProvider : ICommandProvider` com uma fila por tick. `Next(tick)` devolve o
  comando daquele tick; se não chegou, repete o último com `Pressed` vazio. Comandos atrasados que chegam juntos são
  combinados com `PlayerCommand.Merge` (nenhum aperto se perde).
- Esse provedor alimenta uma `CommandInputSource` igual à do co-op local: **`Player`, `HunterCombat`, `WeaponMoves` e o
  `PartyDirector` não mudam**.

### 2.2 Estado (host → clientes)

Replicado por snapshot (não confiável, ~20/s, com interpolação de 100 ms no cliente) e eventos (confiáveis):

| O quê | Como | Classe de origem |
|---|---|---|
| Caçadores: posição, velocidade, direção, animação/quadro, estado de combate (livre, esquiva, caído...), vida, fôlego | snapshot | `Player`, `HunterCombat` |
| Monstros: id, tipo, posição, estado da IA, vida, aviso ativo | snapshot | `Enemy` (+ `LodoMae` fase e núcleos) |
| Projéteis | evento de criação + snapshot | `Arrow` |
| Golpe acertou / número de dano / tremor / pausa no impacto | evento | `CombatFeel` (o host dispara; o cliente só toca o efeito) |
| Sala: troca de sala, ondas, barreira aberta, baú aberto, fenda de saída | evento | `RoomDirector`, `Level.Rift` |
| Reanimação: progresso do anel, levantou, queda do grupo | snapshot + evento | `PartyDirector` |
| Saque: o sorteio acontece no host, **por dono**; o cliente recebe só o próprio saque (`OwnedLoot`) e os cristais com dono | evento por jogador | `PartyProgression`, `RiftLoot`, `FragmentPickup` |
| História e falas | só o dono do save (host); caixa de diálogo replicada como evento (fala, quem conversa) | `StoryService`, `Level.Story` |

### 2.3 Progressão e save no online

- Cada jogador guarda o **próprio** save localmente. Ao entrar no grupo, o cliente manda ao host um resumo do caçador
  (classe, rank, nível da arma, peças equipadas → atributos), validado pelo host contra o catálogo (sem confiar em números).
- O host aplica XP e saque por jogador (as regras de `CoopRules`/`CoopLoot` já separam por dono) e manda ao cliente o
  que ele ganhou; o cliente grava no save dele. No co-op local o convidado mora no save do J1 porque é a mesma máquina.
- Lanterna: a caçada é uma só; na queda geral cada um deixa a própria Lanterna (já é assim). "Lanterna recuperável por
  qualquer um" (GDD §5.8) entra como evento do host: quem tocar a Lanterna de outro a devolve ao dono na próxima sincronia.

### 2.4 O que muda no código quando o online entrar

1. `src/Game/Net/` (novo): `NetSession` (ENet: hospedar, entrar, lista de pares → slots), `NetworkCommandProvider`,
   `SnapshotWriter/Reader`, `RemoteHunterView` (o caçador de outro jogador no cliente: só desenha e interpola).
2. `CoopSession.CreateInput` passa a escolher o provedor: local (`LocalCommandProvider`) ou remoto (`NetworkCommandProvider`).
3. `CoopRules.MaxPlayers` já é 4; a tela de entrada ganha o modo "online" (lista de pares em vez de dispositivos).
4. Câmera: no online cada cliente tem a própria câmera (a coleira continua valendo no host para o grupo não se perder
   na sala; o piso de 85 px deixa de ser compartilhado).
5. Testes: o `--coop-bot` vira o modelo do bot online — host e cliente no mesmo processo trocando `PlayerCommand`
   serializados por um transporte em memória antes de usar o ENet de verdade.

### 2.5 Núcleo do protocolo (12/11/2026, branch `f/online`)

**Pronto (núcleo puro, testado):** `src/Core/Net/` + `tests/Core/Net/NetProtocolTest.cs`.

| Peça | O que faz |
|---|---|
| `NetProtocol` | Mensagens `Hello` (versão + classe), `Welcome` (slot), `Reject` (versão, cheio, classe), `Command` (byte de tipo + `PlayerCommand` de 13 bytes, com o slot **forçado** ao do par que mandou). `Validate` decide a entrada no host. Leitura nunca lança: pacote curto, longo, com NaN, id inválido → `false`. Valida porta (1024..65535) e endereço digitado. |
| `WorldSnapshot` | Host → convidado: tick, id da sala (troca de nível = mudança do id), caçadores (slot, posição, direção, caído, vida/máx), monstros vivos (id, posição, vida/máx) e a fala aberta. Limites: 8 caçadores, 512 monstros, 600 bytes de fala; vida fora de 0..máx e slot repetido derrubam o pacote. |
| `CommandQueue` | O `NetworkCommandProvider` do host sem Godot: junta rajadas com `Merge`, repete a direção sem os apertos quando nada chega, descarta tick repetido/atrasado e fica limitada (30) sem perder aperto. |
| `SnapshotInterpolator` | Convidado: desenha em agora − `interpolationMs`, interpolando caçadores e monstros por id; ignora snapshot velho; troca de sala zera o histórico. |
| `data/coop.json` → `online` | `port` 24680, `snapshotHz` 20, `interpolationMs` 100, `timeoutSeconds` 5 (`CoopRules.Online`, validado). |

### 2.6 Rede ligada (12/11/2026, branch `f/online-rede`)

Online para **2 jogadores** (host + 1 convidado; a HUD, as cores e a entrada são de dois). Protocolo na versão 2: o
snapshot passou a levar a classe de cada caçador (`NetHunter.Kind`) e o tipo de cada monstro (`NetEnemy.Type`).

| Peça | O que faz | Onde |
|---|---|---|
| Sessão | Nó fixo `/root/NetLink` com `ENetMultiplayerPeer`; bytes do `NetProtocol` por `SceneMultiplayer.SendBytes`. Canal 0 confiável (Hello/Welcome/Reject), canal 1 não confiável e ordenado (comandos e snapshots). Timeout do ENet = `timeoutSeconds`, mais o `LinkWatch` (silêncio no relógio de parede). | `Game/Net/NetSession*.cs`, `Core/Net/NetLink.cs` |
| Entrada | Hello → `NetProtocol.Validate` (versão, grupo cheio — inclui co-op local já em curso —, classe). Aceito: Welcome com o slot; o convidado nasce ao lado do J1 assim que a sala deixar (`Level.CanChangeParty`, fora da Brecha) via `Level.ApplyOnlineGuest` → `ApplyParty` com `DeviceKind.Network`. | `NetSession.Host.cs`, `Level.Coop.cs` |
| Comandos | O convidado amostra teclado/controle (`LocalCommandProvider` sobre `MergedInput.LocalSolo`) e manda um `PlayerCommand` por passo de física (parado com menu aberto). No host, `NetworkCommandProvider` (embrulha `CommandQueue`) alimenta a `CommandInputSource` do J2; `RemoteCommands.Sanitize` tira Pausa/Inventário/Diário (o convidado não pausa o host). O J1 do host lê todos os dispositivos locais. | `NetworkCommandProvider.cs`, `CoopSession.CreateInput` |
| Snapshot | `SnapshotBuilder` monta da sala atual (caçadores, monstros vivos com id estável do `NetIdMap`, fala aberta) a `snapshotHz` (`SnapshotPacer`, no relógio do jogo). Fora do jogo (título/saves) manda a sala `espera`. | `SnapshotBuilder.cs` |
| Convidado | `OnlineGuestView`: carrega o chão pintado pelo id da sala, desenha caçadores (sprite da classe, andar/parado pela direção e pelo deslocamento, etiqueta J1/J2) e monstros (folha do monstro + barra de vida) pelo `SnapshotInterpolator` (100 ms), câmera no próprio caçador, fala e vida. Esc → "Sair da partida online?". | `OnlineGuestView.cs` |
| Queda | Host: par saiu/silêncio/"Parar de hospedar" → `Level.LeaveOnline` (host segue sozinho, inclusive na Brecha, com aviso na HUD). Convidado: host caiu/silêncio/recusa → título com aviso em pt-BR no rodapé. | `NetSession*.cs`, `TitleScreen.cs` |
| UI | Título → **Online** (Hospedar: IP local + porta; Escolher o espaço de save; Entrar: IP, porta, classe). Pausa → **Online** (só Hospedar / Parar de hospedar). Com o online ativo, os botões do co-op local somem da pausa. | `UI/OnlineScreen.cs`, `PauseScreen.cs` |
| Prova | `tools/online-bot.sh` sobe host (`--online-bot-host`) e convidado (`--online-bot-join`) headless em 127.0.0.1 numa única chamada da trava: hospedar pela pausa, entrar pelo título com o Martelo, convidado anda (visto no host), grupo entra na Brecha (troca de sala no convidado), o convidado mata um monstro (morre no host e some do snapshot), host para de hospedar (segue sozinho na Brecha) e o convidado volta ao título com o aviso. | `Testing/SmokeTest.Online.cs` |

Relógio: com `--fixed-fps` os dois processos rodam sem sincronia com o tempo real; por isso o ritmo dos snapshots e a
interpolação usam o relógio do jogo (`NetSession.GameNow`) e só a queda por silêncio usa o relógio de parede.

### 2.7 Entrada protegida, cenário e golpes no convidado (20/11/2026, branch `f/online`, issues #70, #71, #72)

Protocolo na versão **4**. Tudo abaixo é validado na leitura (pacote ruim vira `false`, nunca exceção).

| Tema | Como funciona | Onde |
|---|---|---|
| Código da sala (#71) | "Hospedar" mostra um código de 4 dígitos sorteado (o host pode trocar por 4 a 6 números ou apagar para deixar a sala aberta). O convidado digita em "Entrar". O Hello leva o código; o `JoinGate` confere versão → código (tempo constante) → vaga e classe. 5 códigos errados seguidos travam a sala por 30 s (`Locked`). Quem erra o código não descobre se o grupo está cheio. A conexão **não é criptografada**: o código barra curiosos e tentativas às cegas, não quem escuta a rede. | `Core/Net/JoinGate.cs`, `NetSession.Host.cs`, `UI/OnlineScreen.cs` |
| Vaga presa (#71) | O ENet aceita 3 conexões além do grupo (o 2º convidado recebe "grupo cheio" em vez do erro de conexão) e uma conexão sem Hello válido é derrubada em 3 s (`NetProtocol.HelloTimeoutSeconds`). | `NetSession.Host.cs` |
| Ids e tamanhos (#71) | Ids de rede só aceitam letras ASCII, dígitos, `_` e `-` (nada de `/`, `.` ou `..`: o id da sala e o tipo do monstro viram nome de arquivo no convidado). Cada tipo de pacote tem teto de bytes (`NetProtocol.MaxPacketBytes`), checado antes de ler. | `NetProtocol.cs` |
| Mensagens (#71) | Porta ocupada aparece na tela Online (`HostError`); código errado, versão diferente, grupo cheio, trancado e "host fechou antes de aceitar" têm texto próprio em pt-BR; IP começa vazio com dica; o convidado que entra na Brecha vê "Esperando o host sair da Brecha...". Porta e código do host são campos da tela. | `OnlineScreen.cs`, `NetProtocol.Describe` |
| Cenário (#70) | Mensagem confiável `Scene`: id da sala, variante do chão e os objetos que **existem agora** no host (props do JSON, o que a sala montou em código e quebráveis ainda inteiros), mandada só quando muda (conferida 4×/s) e quando um convidado entra. O convidado carrega o chão e o catálogo pelo id e desenha os objetos com sombra de contato. | `Core/Net/SceneSnapshot.cs`, `Net/SceneBuilder.cs`, `OnlineGuestView.ApplyScene` |
| Golpes e dano (#70) | `CombatFeel` ganhou um gancho estático (`EffectEmitted`): faíscas, anéis, números de dano, tremor e o rastro do corte do host viram `NetEffect` e saem em lote confiável a cada snapshot. O convidado só toca o efeito (`CombatFeel` e `FloatingNumbers` próprios, câmera treme respeitando a opção). Sem convidado o gancho não tem ouvinte e não custa nada. | `Core/Net/NetEvents.cs`, `Combat/CombatFeel.cs`, `Combat/WeaponMoves.cs` |
| Pose (#70) | O snapshot leva a animação (índice na lista ordenada de animações da folha, 1 byte) e o quadro de cada caçador e monstro, o espelhamento do monstro, as flechas em voo e os NPCs (id, posição, direção, andando). O convidado troca de animação junto com o host e corrige deriva de mais de 1 quadro. | `WorldSnapshot.cs`, `Net/AnimTable.cs`, `ArrowGhost.cs` |
| HUD (#70) | Barra e texto de vida, barra de fôlego, "Caído!" e o aviso "O host pausou o jogo." (bandeira do snapshot). | `OnlineGuestView.cs` |
| Bot determinístico (#72) | `tools/online-bot.sh`: porta UDP livre por execução, convidado só sobe quando o `host.log` registra "online: hospedando na porta"; as esperas do bot são por estado (tempo REAL só como teto, porque com `--fixed-fps` o relógio do jogo dos dois processos anda em ritmos diferentes); host e convidado se sincronizam por arquivos marcadores; o convidado erra o código antes de acertar e confere cenário, NPCs, números de dano, HUD e o aviso de pausa. | `tools/online-bot.sh`, `Testing/SmokeTest.Online.cs` |

**Ainda falta (próximas fatias):**

1. Efeitos no convidado que ainda faltam: anel de reanimação, avisos de ataque dos monstros (telegrafias no chão), brilho de
   dano nos monstros, portais/barreiras/baú/fenda de saída da Brecha, luzes e o clima da sala (a cena do convidado não
   tem iluminação), poções e Fragmentos na HUD, objetos que piscam por condição da história depois que a sala abriu.
2. Saque e progresso do convidado: hoje o J2 online usa o `SaveData.Guest` do save do host (como no co-op local); o
   resumo do caçador do convidado (§2.3) e o saque por dono mandado como evento ainda não existem.
3. Convidado entrando no meio de uma Brecha: espera o grupo voltar à cidade (vê a sala, mas o caçador só nasce fora dela).
4. 3–4 jogadores online, previsão do próprio movimento no convidado, chat/lista de pares, descoberta na LAN.
5. Sem QA visual com janela (só headless): conferir escala dos sprites, etiquetas e barras na tela do convidado.

## 3. Lacunas conhecidas

- 3 e 4 jogadores locais: as regras e a câmera já suportam, mas a tela de entrada, a HUD e as cores foram feitas para 2.
- Dano do Guardião com 3–4 jogadores: provisório (×1,15, igual ao de 2) até o balanceamento do online.
- Inventário, forja e ficha do caçador continuam do J1; o convidado vê o rank e os Fragmentos dele só na HUD.
- Ravi como tutorial de co-op (GDD §5.8) fica para depois, com a fala dele.
- Depois do merge na v2: sem QA visual das HUDs novas (caçada, poções, Eco) junto com o painel do J2; o bot só caça a
  Brecha E. As Brechas dos Atos III–VII, o Final e os Guardiões em dupla em co-op foram adaptados no código, mas não têm
  bot de co-op próprio.
