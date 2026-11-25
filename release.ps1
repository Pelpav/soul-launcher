# Publie une nouvelle version : .\release.ps1 1.0.1 "Ce qui change"
# Met à jour le numéro de version, compile, commit + tag, puis crée la release GitHub avec l'exe.
# Les lanceurs déjà installés proposeront la mise à jour au prochain démarrage.
param([Parameter(Mandatory = $true)][string]$Version, [string]$Notes = "")
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version attendue au format 1.2.3" }
$core = Get-Content Core.cs -Raw -Encoding UTF8
$core = $core -replace 'AssemblyVersion\("[\d.]+"\)', "AssemblyVersion(""$Version.0"")" -replace 'AssemblyFileVersion\("[\d.]+"\)', "AssemblyFileVersion(""$Version.0"")"
[IO.File]::WriteAllText("$PSScriptRoot\Core.cs", $core, (New-Object Text.UTF8Encoding $false))
& "$PSScriptRoot\build.ps1"
git add -A
git commit -m "Version $Version"
git tag "v$Version"
git push origin HEAD --tags
if (-not $Notes) { $Notes = "Version $Version" }
gh release create "v$Version" SoulLauncher.exe SoulLauncher.exe.sha256 --title "Soul Launcher $Version" --notes $Notes
Write-Host "Release v$Version publiée."
