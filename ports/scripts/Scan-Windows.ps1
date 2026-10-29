param([Parameter(Mandatory=$true)][string]$Target, [Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference = 'Stop'
$resolvedTarget = (Resolve-Path -LiteralPath $Target).Path
$reportParent = Split-Path -Parent $Report
New-Item -ItemType Directory -Path $reportParent -Force | Out-Null
"Status: INCOMPLETE`nCommit: $env:GITHUB_SHA`nTarget: $resolvedTarget`nUTC: $([DateTime]::UtcNow.ToString('o'))" | Set-Content -LiteralPath $Report -Encoding utf8
try {
    # This script is for disposable CI runners only. No sample uploads or cloud analysis.
    Set-MpPreference -SubmitSamplesConsent 2 -MAPSReporting 0
    $preferences = Get-MpPreference
    if ($preferences.SubmitSamplesConsent -ne 2 -or $preferences.MAPSReporting -ne 0) { throw 'Could not disable sample uploads/cloud analysis.' }
    $service = Get-Service -Name WinDefend
    if ($service.Status -ne 'Running') { Start-Service -Name WinDefend }
    Update-MpSignature
    $status = Get-MpComputerStatus
    if (-not $status.AntivirusEnabled -or -not $status.AMServiceEnabled) { throw 'Defender is not active.' }
    if ($status.AntivirusSignatureAge -gt 7) { throw 'Defender signatures are too old.' }
    $platform = Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform'
    $scanner = Get-ChildItem -LiteralPath $platform -Directory | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'MpCmdRun.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $scanner) { $scanner = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe' }
    "Engine: $($status.AMEngineVersion)`nSignatures: $($status.AntivirusSignatureVersion)`nUpdated: $($status.AntivirusSignatureLastUpdated)`nCloud/sample submission: disabled" | Add-Content -LiteralPath $Report
    # DisableRemediation ignores exclusions, includes archives, and preserves detections for review.
    & $scanner -Scan -ScanType 3 -File $resolvedTarget -DisableRemediation 2>&1 | Tee-Object -FilePath ($Report + '.scanner.txt')
    $scanExit = $LASTEXITCODE
    Get-Content -LiteralPath ($Report + '.scanner.txt') | Add-Content -LiteralPath $Report
    "Scanner exit code: $scanExit" | Add-Content -LiteralPath $Report
    if ($scanExit -ne 0) { throw "Scan did not pass; exit code $scanExit" }
    "Status: PASS — no malware detected by this scanner. This is not proof of safety." | Add-Content -LiteralPath $Report
} catch {
    "Status: FAILED_OR_INCOMPLETE`nReason: $($_.Exception.Message)" | Add-Content -LiteralPath $Report
    throw
}
