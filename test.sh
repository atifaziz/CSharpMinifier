#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
./build.sh
dotnet test --no-build tests -c Debug --collect:"XPlat Code Coverage"
dotnet test --no-build tests -c Release
