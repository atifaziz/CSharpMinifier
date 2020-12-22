#!/usr/bin/env sh
set -e
dotnet run --no-launch-profile -f net5.0 -p "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"
