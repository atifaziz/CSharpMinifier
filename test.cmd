@echo off
pushd "%~dp0"
call :main %*
popd
goto :EOF

:main
dotnet tool restore ^
 && call build ^
 && call :test Debug ^
 && call :test Release ^
 && dotnet reportgenerator -reports:.\tests\TestResults\*\coverage.cobertura.xml -targetdir:tmp -reporttypes:TextSummary ^
 && type tmp\Summary.txt
goto :EOF

:test
dotnet test --no-build --collect:"XPlat Code Coverage" -c %1 tests
goto :EOF
