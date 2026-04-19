#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/AAEmu.Login/bin/Release/net6.0"
[ -f Config.json ] || cp ../../../ExampleConfig.json Config.json
mkdir -p sql
export DOTNET_ROLL_FORWARD=LatestMajor
exec dotnet AAEmu.Login.dll "$@"
