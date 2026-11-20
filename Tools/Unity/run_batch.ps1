# Same Shore - run Unity headless (Unity Editor must be closed).
#   .\run_batch.ps1 -Method SameShore.EditorTools.SameShoreSceneBuilder.BuildBatch
#   .\run_batch.ps1 -Method SameShore.EditorTools.SameShoreSmokeTest.Run -NoQuit -Mode desktop
param([string]$Method, [string]$LogName = "unity", [string[]]$Extra = @(), [switch]$NoQuit, [int]$TimeoutSec = 1500,
      [string]$Mode = "", [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
$proj = (Resolve-Path "$PSScriptRoot\..\..").Path
$logDir = Join-Path $env:TEMP "sameshore\logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "$LogName.log"
$modeFile = Join-Path $proj "Logs\SameShoreSmokeMode.txt"
if ($Mode) { New-Item -ItemType Directory -Force (Join-Path $proj "Logs") | Out-Null; Set-Content $modeFile $Mode }
$argList = @("-batchmode", "-projectPath", "`"$proj`"", "-logFile", "`"$log`"")
if (-not $NoQuit) { $argList += "-quit" }
if ($Method) { $argList += @("-executeMethod", $Method) }
foreach ($e in $Extra) { $argList += "`"$e`"" }
$p = Start-Process -FilePath $Unity -ArgumentList ($argList -join " ") -PassThru -NoNewWindow
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); "TIMEOUT - killed" }
if ($Mode) { Remove-Item $modeFile -ErrorAction SilentlyContinue }
"LOG $log"
Select-String -Path $log -Pattern "SameShoreTest\] (FAIL|ALL|\d+ FAILED)|error CS|Exception:|VALIDATION|Scripts have compiler errors|Aborting batchmode" |
    Select-Object -First 80 | ForEach-Object { $_.Line }
