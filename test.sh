#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
dotnet tool restore
./build.sh
dotnet test --no-build tests -c Debug --collect:"XPlat Code Coverage"
dotnet test --no-build tests -c Release
dotnet reportgenerator "-reports:tests/TestResults/*/coverage.cobertura.xml" -targetdir:tmp -reporttypes:TextSummary
cat tmp/Summary.txt
