$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$release = Join-Path $root 'release'
New-Item -ItemType Directory -Force -Path $release | Out-Null
# Explicit allowlist: test fixtures, local logs and personal Lunar files can never enter this ZIP.
$stage = Join-Path $root ('build\package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'docs') | Out-Null
foreach ($file in @('LunarHitFixInstaller.exe','hitfix-agent.jar','SHA256SUMS.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $dist $file))) { throw "Build first: missing $file" }
    Copy-Item -LiteralPath (Join-Path $dist $file) -Destination $stage
}
foreach ($file in @('README.md','LICENSE','THIRD_PARTY.txt')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $root 'docs\ERRORS.md'),(Join-Path $root 'docs\RELEASE-NOTES.md') -Destination (Join-Path $stage 'docs')
$zip = Join-Path $release 'LunarHitFixInstaller-v0.1.0-windows.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Copy-Item -LiteralPath (Join-Path $dist 'LunarHitFixInstaller.exe'),(Join-Path $dist 'hitfix-agent.jar'),(Join-Path $dist 'SHA256SUMS.txt') -Destination $release -Force
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$sums = [IO.File]::ReadAllText((Join-Path $dist 'SHA256SUMS.txt')).TrimEnd() + "`n$zipHash  LunarHitFixInstaller-v0.1.0-windows.zip`n"
[IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), $sums, [Text.UTF8Encoding]::new($false))
Write-Output "Release files: $release"
