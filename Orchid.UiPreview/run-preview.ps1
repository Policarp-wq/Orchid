param([string]$Dotnet = (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'))

$ErrorActionPreference = 'Stop'
if (!(Test-Path $Dotnet)) { $Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_ROOT = Split-Path $Dotnet
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
& $Dotnet build (Join-Path $PSScriptRoot 'Orchid.UiPreview.csproj') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# WinUI activation fails when this unpackaged executable runs from the WSL UNC share.
$source = Join-Path $PSScriptRoot 'bin\Debug\net10.0-windows10.0.19041.0\win-x64'
$runDirectory = Join-Path $env:TEMP ('Orchid.UiPreview-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
try {
    Copy-Item -Path (Join-Path $source '*') -Destination $runDirectory -Recurse
    $start = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $runDirectory 'Orchid.UiPreview.exe'))
    $start.WorkingDirectory = $runDirectory
    $start.UseShellExecute = $false
    $preview = [System.Diagnostics.Process]::Start($start)
    $preview.WaitForExit()
    if ($preview.ExitCode -ne 0) {
        throw "Preview exited with code $($preview.ExitCode). Diagnostics: $env:LOCALAPPDATA\Orchid\UiPreview\Logs"
    }
}
finally {
    if ($preview) { $preview.Dispose() }
    Remove-Item -LiteralPath $runDirectory -Recurse -Force
}
