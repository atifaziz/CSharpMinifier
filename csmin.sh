#!/usr/bin/env sh
set -e
dotnet run --no-launch-profile -f net10.0 --project "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"
