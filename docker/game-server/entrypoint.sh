#!/usr/bin/env sh
set -eu

executable="/app/LH.Main.GameServer.x86_64"

if [ ! -x "$executable" ]; then
  echo "Game server executable not found or not executable: $executable" >&2
  exit 1
fi

exec "$executable" -batchmode -nographics -logfile -
