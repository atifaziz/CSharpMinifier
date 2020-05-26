#!/usr/bin/env sh
set -e
dotnet run --no-launch-profile -f netcoreapp3.0 -p "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"
