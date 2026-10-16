param(
    [string]$SourceDirectory,
    [string]$ReleaseDirectory,
    [string]$OutputPath,
    [string]$PythonPath = 'python'
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
if (-not $SourceDirectory) { $SourceDirectory = $repository }
if (-not $ReleaseDirectory) { $ReleaseDirectory = Join-Path $repository 'release' }
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$ReleaseDirectory = [IO.Path]::GetFullPath($ReleaseDirectory)
$original = Join-Path $ReleaseDirectory 'LunarHitFixInstaller.exe'
$agent = Join-Path $ReleaseDirectory 'hitfix-agent.jar'
$expectedExe = 'A4ABF4760F7729460D7DB9D7226BA227E5EF11FB76E3D020E20A81A3CC9D1EAC'
if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $expectedExe) { throw 'This audit is for the exact published v0.1.1 EXE only' }
$build = Join-Path $repository ('build\audit-' + [guid]::NewGuid().ToString('N'))
$classes = Join-Path $build 'classes'
New-Item -ItemType Directory -Force -Path $classes | Out-Null
$vendor = Join-Path $SourceDirectory 'vendor\asm-9.9.1.jar'
if ((Get-FileHash -LiteralPath $vendor -Algorithm SHA256).Hash -ne '6F3828A215C920059A5EFA2FB55C233D6C54EC5CADCA99CE1B1BDD10077C7DDD') { throw 'ASM checksum mismatch' }
$javaSources = (Get-ChildItem -LiteralPath (Join-Path $SourceDirectory 'agent\src') -Filter '*.java' -Recurse).FullName
& javac -source 8 -target 8 -encoding UTF-8 -cp $vendor -d $classes $javaSources
if ($LASTEXITCODE -ne 0) { throw 'Independent Java compilation failed' }
$csharpSources = (Get-ChildItem -LiteralPath (Join-Path $SourceDirectory 'src') -Filter '*.cs').FullName
$rebuilt = Join-Path $build 'LunarHitFixInstaller.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Web.Extensions.dll /reference:System.Core.dll "/win32manifest:$(Join-Path $SourceDirectory 'src\app.manifest')" "/out:$rebuilt" "/resource:$agent,hitfix-agent.jar" $csharpSources
if ($LASTEXITCODE -ne 0) { throw 'Independent C# compilation failed' }
$report = & $PythonPath (Join-Path $PSScriptRoot 'verify_release.py') $original $rebuilt $agent $vendor $classes
if ($LASTEXITCODE -ne 0) { throw 'Binary/source correspondence verification failed' }
if ($OutputPath) {
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null
    [IO.File]::WriteAllText($OutputPath, ($report -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
}
$report
