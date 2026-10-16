# Anything that is not -Dotnet goes to the app, e.g. --fixture FiveMinutes --theme dark.
[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Dotnet = (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'),
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$AppArguments = @())

$ErrorActionPreference = 'Stop'
if (!(Test-Path $Dotnet)) { $Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_ROOT = Split-Path $Dotnet
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
& $Dotnet build (Join-Path $PSScriptRoot 'Orchid.Windows.csproj') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# WinUI activation fails when this unpackaged executable runs from the WSL UNC share.
$source = Join-Path $PSScriptRoot 'bin\Debug\net10.0-windows10.0.19041.0\win-x64'
$runDirectory = Join-Path $env:TEMP ('Orchid.Windows-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
try {
    Copy-Item -Path (Join-Path $source '*') -Destination $runDirectory -Recurse
    $start = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $runDirectory 'Orchid.Windows.exe'))
    $start.WorkingDirectory = $runDirectory
    $start.UseShellExecute = $false
    # Windows PowerShell 5.1 has no ProcessStartInfo.ArgumentList.
    $start.Arguments = ($AppArguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $app = [System.Diagnostics.Process]::Start($start)
    $app.WaitForExit()
    if ($app.ExitCode -ne 0) {
        throw "Orchid exited with code $($app.ExitCode). Diagnostics: $env:LOCALAPPDATA\Orchid\Logs"
    }
}
finally {
    if ($app) { $app.Dispose() }
    Remove-Item -LiteralPath $runDirectory -Recurse -Force
}
