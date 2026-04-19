#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/AAEmu.Game/bin/Release/net6.0"
[ -f Config.json ] || cp ../../../ExampleConfig.json Config.json
mkdir -p sql Data
if [ ! -f Data/compact.sqlite3 ]; then
  echo "[ERROR] Data/compact.sqlite3 не найден."
  echo "        Извлеките его из клиента ArcheAge 3.0.3.0 и положите в:"
  echo "        $(pwd)/Data/compact.sqlite3"
  exit 1
fi
export DOTNET_ROLL_FORWARD=LatestMajor
exec dotnet AAEmu.Game.dll "$@"
