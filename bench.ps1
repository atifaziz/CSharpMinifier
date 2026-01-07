[CmdletBinding()]
param (
    [string]$Name = '',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

try {
    Push-Location $PSScriptRoot
    dotnet run -c Release -f net10.0 --project bench -- `
        -a (Join-Path BenchmarkDotNet.Artifacts $name) @args
}
finally {
    Pop-Location
}
