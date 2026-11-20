#!/usr/bin/env bash
# Prova do co-op online: sobe host e convidado headless em 127.0.0.1 (duas instâncias, mesma máquina) e confere os dois.
# Uso (de dentro da trava de comandos pesados, numa única chamada):
#   bash /c/ale/ferramentas/pesado.sh bash tools/online-bot.sh <godot_console.exe> [pasta_tmp]
# Sai 0 só se as duas instâncias saírem 0 (código da sala errado recusado e certo aceito, convidado anda no host, troca de
# sala, monstro morto sincronizado e desconexão limpa: host segue sozinho, convidado volta ao título com aviso).
#
# Determinístico (issue #72): nada de "sleep" fixo.
#  - porta UDP livre escolhida a cada execução (--online-port=), sem colidir com outra frente ou com um jogo aberto;
#  - o convidado só sobe depois que o host.log registra "online: hospedando na porta" (evento, não tempo);
#  - host morto antes de hospedar derruba a prova na hora; teto de tempo REAL (HOST_READY_TIMEOUT / RUN_TIMEOUT) só para
#    não travar (o relógio do jogo com --fixed-fps anda em ritmo diferente em cada processo, por isso não serve de teto);
#  - os dois lados se sincronizam por arquivo marcador em <pasta_tmp>/sync (o convidado avisa que viu o monstro morrer).
set -u
GODOT="${1:?caminho do Godot (console)}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="${2:-$ROOT/tmp/online}"
HOST_READY_TIMEOUT="${HOST_READY_TIMEOUT:-420}"
RUN_TIMEOUT="${RUN_TIMEOUT:-1200}"
rm -rf "$TMP/sync" "$TMP/host.log" "$TMP/guest.log"
mkdir -p "$TMP/sync"
ROOT_WIN="$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")"
TMP_WIN="$(cygpath -m "$TMP" 2>/dev/null || echo "$TMP")"

pick_port() {
  local p tries
  for tries in $(seq 1 60); do
    p=$((20000 + (RANDOM % 20000)))
    if ! netstat -ano -p UDP 2>/dev/null | grep -q ":$p "; then
      echo "$p"
      return
    fi
  done
  echo 24680
}
PORT="$(pick_port)"
echo "online-bot: porta UDP $PORT, sincronia em $TMP_WIN/sync"

COMMON=(--headless --path "$ROOT_WIN" --fixed-fps 60)
ARGS=(--smoke-test)
SYNC=("--online-port=$PORT" "--online-sync=$TMP_WIN/sync")

kill_all() {
  [ -n "${GUEST_PID:-}" ] && kill -9 "$GUEST_PID" 2>/dev/null
  [ -n "${HOST_PID:-}" ] && kill -9 "$HOST_PID" 2>/dev/null
}
trap kill_all EXIT

"$GODOT" "${COMMON[@]}" --user-dir "$TMP_WIN/host" -- "${ARGS[@]}" --online-bot-host "${SYNC[@]}" > "$TMP/host.log" 2>&1 &
HOST_PID=$!

# 1) Espera o host abrir a porta (evento no log). Host que morreu ou estourou o teto derruba a prova.
START=$SECONDS
HOST_READY=0
while true; do
  if grep -q "online: hospedando na porta $PORT" "$TMP/host.log" 2>/dev/null; then
    HOST_READY=1
    break
  fi
  if ! kill -0 "$HOST_PID" 2>/dev/null; then
    echo "online-bot: o host terminou antes de hospedar"
    break
  fi
  if [ $((SECONDS - START)) -gt "$HOST_READY_TIMEOUT" ]; then
    echo "online-bot: o host não hospedou em ${HOST_READY_TIMEOUT}s (tempo real)"
    break
  fi
  sleep 1
done

GUEST_RC=99
GUEST_DONE=0
HOST_RC=99
if [ "$HOST_READY" -eq 1 ]; then
  echo "online-bot: host pronto em $((SECONDS - START))s, subindo o convidado"
  "$GODOT" "${COMMON[@]}" --user-dir "$TMP_WIN/guest" -- "${ARGS[@]}" --online-bot-join "${SYNC[@]}" > "$TMP/guest.log" 2>&1 &
  GUEST_PID=$!

  # 2) Espera os dois terminarem (teto de tempo real). Se um morre com erro, o outro não fica esperando em vão.
  START=$SECONDS
  while kill -0 "$HOST_PID" 2>/dev/null || kill -0 "$GUEST_PID" 2>/dev/null; do
    if [ $((SECONDS - START)) -gt "$RUN_TIMEOUT" ]; then
      echo "online-bot: teto de ${RUN_TIMEOUT}s (tempo real) estourado, encerrando os dois"
      kill_all
      break
    fi
    if [ "$GUEST_DONE" -eq 0 ] && ! kill -0 "$GUEST_PID" 2>/dev/null; then
      # Convidado já saiu: guarda o rc; se saiu mal, o host não tem por que esperar até o teto.
      wait "$GUEST_PID" 2>/dev/null; GUEST_RC=$?
      GUEST_DONE=1
      if [ "$GUEST_RC" -ne 0 ] && kill -0 "$HOST_PID" 2>/dev/null; then
        echo "online-bot: convidado saiu com rc=$GUEST_RC, encerrando o host"
        kill -9 "$HOST_PID" 2>/dev/null
        break
      fi
    fi
    sleep 1
  done
  if [ "$GUEST_DONE" -eq 0 ]; then
    wait "$GUEST_PID" 2>/dev/null; GUEST_RC=$?
  fi
else
  kill_all
fi
wait "$HOST_PID" 2>/dev/null; HOST_RC=$?

echo "--- host (rc=$HOST_RC)"; grep -E "online|smoke|FALHOU|ERROR|Exception" "$TMP/host.log" | grep -v "^\s*at " | tail -25
echo "--- convidado (rc=$GUEST_RC)"; grep -E "online|smoke|FALHOU|ERROR|Exception" "$TMP/guest.log" 2>/dev/null | grep -v "^\s*at " | tail -25
[ $HOST_RC -eq 0 ] && [ $GUEST_RC -eq 0 ] && echo "ONLINE OK" && exit 0
echo "ONLINE FALHOU"; exit 1
