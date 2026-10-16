param([string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $root ('build\tests-' + [guid]::NewGuid().ToString('N')) }
$testClasses = Join-Path $BuildDirectory 'test-classes'
New-Item -ItemType Directory -Force -Path $testClasses | Out-Null
$agent = Join-Path $root 'dist\hitfix-agent.jar'
$exe = Join-Path $root 'dist\LunarHitFixInstaller.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $root 'dist\InstallerTests.exe'
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll /reference:System.Core.dll "/reference:$exe" "/out:$testExe" (Join-Path $root 'tests\InstallerTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer tests did not compile' }
& $testExe (Join-Path $BuildDirectory 'fixtures')
if ($LASTEXITCODE -ne 0) { throw 'Installer tests failed' }
$sources = (Get-ChildItem -LiteralPath (Join-Path $root 'agent\tests') -Filter '*.java' -Recurse).FullName
& javac -source 8 -target 8 -encoding UTF-8 -cp $agent -d $testClasses $sources
if ($LASTEXITCODE -ne 0) { throw 'Agent tests did not compile' }
$javaExecutables = @((Get-Command java).Source)
$lunarJre = Join-Path $env:USERPROFILE '.lunarclient\jre'
if (Test-Path -LiteralPath $lunarJre) { $javaExecutables += (Get-ChildItem -LiteralPath $lunarJre -Filter java.exe -Recurse).FullName }
foreach ($javaExe in ($javaExecutables | Select-Object -Unique)) {
    & $javaExe -Xverify:all "-javaagent:$agent" -cp "$agent;$testClasses" dev.hitfix.lunar.IntegrationTest
    if ($LASTEXITCODE -ne 0) { throw "Agent integration failed: $javaExe" }
    & $javaExe -Xverify:all "-javaagent:$agent" -cp "$agent;$testClasses" dev.hitfix.lunar.IsolationTest $testClasses
    if ($LASTEXITCODE -ne 0) { throw "Agent classloader test failed: $javaExe" }
}
& $exe --version
if ($LASTEXITCODE -ne 0) { throw 'Executable smoke test failed' }
$menuOutput = (@('invalid', '3', '0') | & $exe) -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Credits menu smoke test failed' }
if ($menuOutput -notmatch 'Invalid choice' -or $menuOutput -notmatch '\[3\] Credits' -or $menuOutput -notmatch 'Packet-timing method: Incognitolol/HitFix-Mod') {
    throw 'Expected menu, validation and credits were not displayed'
}
if ($menuOutput.IndexOf('Packet-timing method:') -lt $menuOutput.IndexOf('Choose 1, 2, 3 or 0:')) {
    throw 'Credits appeared before the user selected them'
}
if (($menuOutput.Split(@('[3] Credits'), [StringSplitOptions]::None)).Count -lt 3) {
    throw 'Credits did not return to the menu'
}
Write-Output 'PASS: installer fixtures, Java integration/classloader tests, executable menu and credits'
