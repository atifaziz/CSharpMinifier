#!/usr/bin/env sh
set -e
dotnet run --no-launch-profile -p "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"


