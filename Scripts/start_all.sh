#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Starting AAEmu ArcheAge 3.0 Server..."
echo "Starting Login Server..."
cd "$SCRIPT_DIR/../AAEmu.Login"
dotnet run &
LOGIN_PID=$!

sleep 5

echo "Starting Game Server..."
cd "$SCRIPT_DIR/../AAEmu.Game"
dotnet run &
GAME_PID=$!

echo "Login Server PID: $LOGIN_PID"
echo "Game Server PID: $GAME_PID"
echo "Press Ctrl+C to stop both servers"

trap "kill $LOGIN_PID $GAME_PID 2>/dev/null; exit" INT TERM
wait
