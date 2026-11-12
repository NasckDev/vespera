#!/usr/bin/env bash
# Prova do co-op online: sobe host e convidado headless em 127.0.0.1 (duas instâncias, mesma máquina) e confere os dois.
# Uso (de dentro da trava de comandos pesados, numa única chamada):
#   bash /c/ale/ferramentas/pesado.sh bash tools/online-bot.sh <godot_console.exe> [pasta_tmp]
# Sai 0 só se as duas instâncias saírem 0 (conexão, movimento do convidado no host, monstro morto sincronizado, troca de
# sala e desconexão limpa: host segue sozinho, convidado volta ao título com aviso).
set -u
GODOT="${1:?caminho do Godot (console)}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="${2:-$ROOT/tmp/online}"
mkdir -p "$TMP"
ROOT_WIN="$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")"
TMP_WIN="$(cygpath -m "$TMP" 2>/dev/null || echo "$TMP")"

"$GODOT" --headless --path "$ROOT_WIN" --fixed-fps 60 --user-dir "$TMP_WIN/host" -- --smoke-test --online-bot-host > "$TMP/host.log" 2>&1 &
HOST_PID=$!
sleep 3
"$GODOT" --headless --path "$ROOT_WIN" --fixed-fps 60 --user-dir "$TMP_WIN/guest" -- --smoke-test --online-bot-join > "$TMP/guest.log" 2>&1 &
GUEST_PID=$!

wait $GUEST_PID; GUEST_RC=$?
wait $HOST_PID; HOST_RC=$?
echo "--- host (rc=$HOST_RC)"; grep -E "online|smoke|FALHOU|ERROR|Exception" "$TMP/host.log" | grep -v "^\s*at " | tail -25
echo "--- convidado (rc=$GUEST_RC)"; grep -E "online|smoke|FALHOU|ERROR|Exception" "$TMP/guest.log" | grep -v "^\s*at " | tail -25
[ $HOST_RC -eq 0 ] && [ $GUEST_RC -eq 0 ] && echo "ONLINE OK" && exit 0
echo "ONLINE FALHOU"; exit 1
