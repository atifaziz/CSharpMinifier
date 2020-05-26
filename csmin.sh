#!/usr/bin/env sh
set -e
dotnet run --no-launch-profile -f netcoreapp3.1 -p "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"
