#!/usr/bin/env sh
set -e
cd "$(dirname "$0")"
dotnet tool restore
./build.sh
dotnet test --no-build --collect:"XPlat Code Coverage" -c Debug tests
dotnet test --no-build --collect:"XPlat Code Coverage" -c Release tests
dotnet reportgenerator "-reports:tests/TestResults/*/coverage.cobertura.xml" -targetdir:tmp -reporttypes:TextSummary
cat tmp/Summary.txt
