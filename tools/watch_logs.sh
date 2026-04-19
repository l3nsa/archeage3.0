#!/usr/bin/env bash
# Live-tail server logs (login+game) and client logs (ArcheAge) side-by-side.
# Usage: ./tools/watch_logs.sh            # color-tagged multiplex to stdout
#        ./tools/watch_logs.sh sync       # one-shot copy of latest client logs into ./logs/client/
#        ./tools/watch_logs.sh clean      # remove old copied client logs

set -u

CLIENT_DIR="/mnt/c/Users/l3nsa/Documents/ArcheAge"
SERVER_LOGIN="/tmp/login.log"
SERVER_GAME="/tmp/game.log"
LOCAL_COPY="$(cd "$(dirname "$0")/.." && pwd)/logs/client"

mkdir -p "$LOCAL_COPY"

sync_client() {
    echo "[sync] copying client logs from $CLIENT_DIR -> $LOCAL_COPY"
    # Copy ArcheAge.log + any .log/.crash/.dmp newer than 1 day
    cp -u "$CLIENT_DIR"/ArcheAge.log "$LOCAL_COPY"/ 2>/dev/null || true
    find "$CLIENT_DIR" -maxdepth 1 -type f \( -iname "*.log" -o -iname "*.crash" -o -iname "*.dmp" \) -mtime -1 \
        -exec cp -u {} "$LOCAL_COPY"/ \; 2>/dev/null
    ls -lt "$LOCAL_COPY" | head -15
}

case "${1:-tail}" in
    sync)
        sync_client
        exit 0
        ;;
    clean)
        rm -f "$LOCAL_COPY"/*.log "$LOCAL_COPY"/*.crash "$LOCAL_COPY"/*.dmp
        echo "[clean] removed copies in $LOCAL_COPY"
        exit 0
        ;;
    tail|*)
        : # fall through
        ;;
esac

# Colored tags
R=$'\033[31m'; G=$'\033[32m'; Y=$'\033[33m'; B=$'\033[34m'; M=$'\033[35m'; C=$'\033[36m'; Z=$'\033[0m'

echo "[watch] server login:  $SERVER_LOGIN"
echo "[watch] server game:   $SERVER_GAME"
echo "[watch] client dir:    $CLIENT_DIR"
echo "[watch] Ctrl-C to stop"
echo

# Ensure files exist so tail -F doesn't whine
touch "$SERVER_LOGIN" "$SERVER_GAME"
touch "$CLIENT_DIR/ArcheAge.log" 2>/dev/null || true

PIDS=()
cleanup() { for p in "${PIDS[@]:-}"; do kill "$p" 2>/dev/null; done; exit 0; }
trap cleanup INT TERM

# 1) Server login
( stdbuf -oL tail -n 5 -F "$SERVER_LOGIN" 2>/dev/null \
    | sed -u "s/^/${G}[LOGIN]${Z} /" ) &
PIDS+=($!)

# 2) Server game
( stdbuf -oL tail -n 5 -F "$SERVER_GAME" 2>/dev/null \
    | sed -u "s/^/${C}[GAME ]${Z} /" ) &
PIDS+=($!)

# 3) Client ArcheAge.log
( stdbuf -oL tail -n 5 -F "$CLIENT_DIR/ArcheAge.log" 2>/dev/null \
    | sed -u "s/^/${Y}[CLIENT]${Z} /" ) &
PIDS+=($!)

# 4) New crash/session logs (polling every 2s — client writes GUID.log per session)
(
    seen_file="$LOCAL_COPY/.seen"
    touch "$seen_file"
    while true; do
        for f in "$CLIENT_DIR"/*.log "$CLIENT_DIR"/*.crash; do
            [ -f "$f" ] || continue
            base="$(basename "$f")"
            [ "$base" = "ArcheAge.log" ] && continue
            if ! grep -qxF "$base" "$seen_file" 2>/dev/null; then
                echo "$base" >> "$seen_file"
                echo "${M}[CRASH ]${Z} new file: $f  (copying to $LOCAL_COPY)"
                cp -u "$f" "$LOCAL_COPY/" 2>/dev/null || true
                # Print tail of the new file, tagged
                tail -n 80 "$f" 2>/dev/null | sed -u "s|^|${R}[$base]${Z} |"
            fi
        done
        sleep 2
    done
) &
PIDS+=($!)

wait
