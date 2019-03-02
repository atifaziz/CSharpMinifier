#!/usr/bin/env bash
set -e
dotnet run --no-launch-profile -p "$(dirname "$0")/src/CSharpMinifierConsole" -- "$@"


