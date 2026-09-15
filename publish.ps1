# Publishes ClipVault as a self-contained single-file exe for win-x64.
#   .\publish.ps1                 -> .\publish\ClipVault.exe
#   .\publish.ps1 -Output C:\tmp  -> C:\tmp\ClipVault.exe
#   .\publish.ps1 -StopRunning    stop a ClipVault started from the output folder first
param(
    [string]$Output = (Join-Path $PSScriptRoot 'publish'),
    [switch]$StopRunning
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$target = Join-Path $Output 'ClipVault.exe'
$running = Get-Process ClipVault -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target }
if ($running) {
    if ($StopRunning) {
        $running | Stop-Process -Force
        Start-Sleep -Seconds 1
    } else {
        Write-Error "ClipVault is running from $target (pid $($running.Id -join ', ')). Exit it from the tray icon or rerun with -StopRunning."
    }
}

dotnet publish src\ClipVault\ClipVault.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -o $Output --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item (Join-Path $Output '*.pdb') -ErrorAction SilentlyContinue
$exe = Get-Item (Join-Path $Output 'ClipVault.exe')
"Published $($exe.FullName) ($([math]::Round($exe.Length / 1MB, 1)) MB)"
