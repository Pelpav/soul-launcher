# Compile SoulLauncher.exe (csc.exe de .NET Framework, intégré à Windows)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
python -I build_packs.py
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:icon.ico /out:SoulLauncher.exe `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll /r:System.Core.dll /r:System.IO.Compression.dll `
  /resource:packs.zip,packs.zip `
  Core.cs MainForm.cs SettingsForm.cs Updater.cs Onboarding.cs OnboardingForm.cs GameData.cs Logos.cs
if ($LASTEXITCODE -ne 0) { throw "compilation échouée" }
(Get-FileHash SoulLauncher.exe -Algorithm SHA256).Hash.ToLower() + "  SoulLauncher.exe" | Set-Content -Encoding ascii SoulLauncher.exe.sha256
Get-Content SoulLauncher.exe.sha256
