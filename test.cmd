@echo off
pushd "%~dp0"
call :main %*
popd
goto :EOF

:main
dotnet tool restore ^
 && call build ^
 && call :test Debug --collect:"XPlat Code Coverage" ^
 && call :test Release ^
 && dotnet reportgenerator -reports:.\tests\TestResults\*\coverage.cobertura.xml -targetdir:tmp -reporttypes:TextSummary ^
 && type tmp\Summary.txt
goto :EOF

:test
dotnet test --no-build tests -c %*
goto :EOF
