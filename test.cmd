@echo off
pushd "%~dp0"
call :main %*
popd
goto :EOF

:main
    call build ^
 && call :test Debug --collect:"XPlat Code Coverage"
 && call :test Release
goto :EOF

:test
dotnet test --no-build tests -c %*
goto :EOF
