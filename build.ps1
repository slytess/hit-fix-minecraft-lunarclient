param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root ('build\' + [guid]::NewGuid().ToString('N'))
$classes = Join-Path $build 'classes'
$assembly = Join-Path $build 'assembly'
$dist = Join-Path $root 'dist'
$vendor = Join-Path $root 'vendor\asm-9.9.1.jar'
$expectedAsm = '6F3828A215C920059A5EFA2FB55C233D6C54EC5CADCA99CE1B1BDD10077C7DDD'
if ((Get-FileHash -LiteralPath $vendor -Algorithm SHA256).Hash -ne $expectedAsm) { throw 'ASM checksum mismatch' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found' }
foreach ($tool in @('javac', 'jar', 'java')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Missing build tool: $tool. Install a JDK (8 or later)." }
}
New-Item -ItemType Directory -Force -Path $classes,$assembly,$dist | Out-Null
$sources = (Get-ChildItem -LiteralPath (Join-Path $root 'agent\src') -Filter '*.java' -Recurse).FullName
& javac -source 8 -target 8 -encoding UTF-8 -cp $vendor -d $classes $sources
if ($LASTEXITCODE -ne 0) { throw 'Agent compilation failed' }
Copy-Item -LiteralPath (Join-Path $classes 'dev') -Destination $assembly -Recurse
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD_PARTY.txt') -Destination $assembly
Push-Location $assembly
try {
    & jar xf $vendor org/objectweb/asm
    if ($LASTEXITCODE -ne 0) { throw 'ASM extraction failed' }
} finally { Pop-Location }
$agent = Join-Path $dist 'hitfix-agent.jar'
& jar cfm $agent (Join-Path $root 'agent\MANIFEST.MF') -C $assembly .
if ($LASTEXITCODE -ne 0) { throw 'Agent packaging failed' }
$exe = Join-Path $dist 'LunarHitFixInstaller.exe'
$csharp = (Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Web.Extensions.dll /reference:System.Core.dll "/win32manifest:$(Join-Path $root 'src\app.manifest')" "/out:$exe" "/resource:$agent,hitfix-agent.jar" $csharp
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
if (-not $SkipTests) {
    & (Join-Path $root 'test.ps1') -BuildDirectory $build
    if (-not $?) { throw 'Tests failed' }
}
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD_PARTY.txt') -Destination $dist -Force
$checksums = foreach ($file in @('LunarHitFixInstaller.exe','hitfix-agent.jar')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $dist $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $file"
}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), $checksums, [Text.UTF8Encoding]::new($false))
Write-Output "Built: $exe"
