param([string]$Unity = 'E:\UnityHubs\6000.3.23f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$validationRoot = Join-Path $projectRoot 'Logs\CRTAudioRefinement'
$isolatedRoot = Join-Path $validationRoot 'UnityProject'
$sourceRoot = Join-Path $projectRoot 'Assets\CRT\AudioPreview'
New-Item -ItemType Directory -Path (Join-Path $isolatedRoot 'Assets\CRT'),(Join-Path $isolatedRoot 'Packages'),(Join-Path $isolatedRoot 'ProjectSettings') -Force | Out-Null
Copy-Item -LiteralPath $sourceRoot -Destination (Join-Path $isolatedRoot 'Assets\CRT') -Recurse -Force
# These three classes moved to the shared runtime assembly. Remove only stale copied files.
foreach ($className in @('CrtAudioPreset','CrtAudioControls','CrtAudioDsp')) {
    foreach ($suffix in @('.cs','.cs.meta')) {
        $obsoletePath = [IO.Path]::GetFullPath((Join-Path $isolatedRoot ('Assets\CRT\AudioPreview\Editor\' + $className + $suffix)))
        if (-not $obsoletePath.StartsWith($isolatedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid validation path' }
        if (Test-Path -LiteralPath $obsoletePath) { Remove-Item -LiteralPath $obsoletePath }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AudioRefinementHarness.cs.txt') -Destination (Join-Path $isolatedRoot 'Assets\CRT\AudioPreview\Editor\AudioRefinementHarness.cs')
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') -Destination (Join-Path $isolatedRoot 'ProjectSettings\ProjectVersion.txt')
[System.IO.File]::WriteAllText((Join-Path $isolatedRoot 'Packages\manifest.json'), '{"dependencies":{"com.unity.modules.audio":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.unitywebrequest":"1.0.0","com.unity.modules.unitywebrequestaudio":"1.0.0"}}')
$hashes = foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.asmdef' }) {
    $relativePath = $sourceFile.FullName.Substring($sourceRoot.Length + 1)
    $copyPath = Join-Path $isolatedRoot ('Assets\CRT\AudioPreview\' + $relativePath)
    $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName).Hash
    if ($sourceHash -ne (Get-FileHash -LiteralPath $copyPath).Hash) { throw "Validation copy differs: $($sourceFile.Name)" }
    [pscustomobject]@{ file = $relativePath; sha256 = $sourceHash }
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $validationRoot 'verified-source-hashes.json')
foreach ($stage in @('Run','Restart')) {
    $logPath = Join-Path $validationRoot ('editor-' + $stage.ToLowerInvariant() + '.log')
    $unityArgs = @('-batchmode','-projectPath',('"' + $isolatedRoot + '"'),'-executeMethod',('MediaPipeTest.CRT.AudioPreview.AudioRefinementHarness.' + $stage),'-logFile',('"' + $logPath + '"'))
    $testProcess = Start-Process -FilePath $Unity -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $testProcess.WaitForExit(1000)) {
        if ($timer.Elapsed.TotalSeconds -gt 240) { throw "Validation Unity timed out. Test process ID: $($testProcess.Id). Log: $logPath" }
    }
    if ($testProcess.ExitCode -ne 0) { throw "Unity $stage failed with exit code $($testProcess.ExitCode). Log: $logPath" }
}
$reportRoot = Join-Path $isolatedRoot 'Logs\CRTAudioPreview'
foreach ($reportName in @('validation.json','controls-validation.json','reload-validation.json','restart-validation.json')) {
    $report = Get-Content -LiteralPath (Join-Path $reportRoot $reportName) -Raw | ConvertFrom-Json
    if (-not $report.passed) { throw "$reportName failed." }
    Write-Output ($reportName + ': PASS')
}
$destination = Join-Path $projectRoot 'Logs\CRTAudioPreview'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $reportRoot -File | Where-Object { $_.Extension -in '.json','.wav' } | Copy-Item -Destination $destination -Force
