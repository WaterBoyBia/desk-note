param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [string]$StateRoot = (Join-Path $env:TEMP ("desk-note-performance-" + [Guid]::NewGuid().ToString("N")))
)

$resolvedExecutable = (Resolve-Path -LiteralPath $Executable).Path
$dataDirectory = Join-Path $StateRoot "data"
New-Item -ItemType Directory -Force -Path $dataDirectory | Out-Null

$existingProcesses = @(Get-Process -Name "desk-note" -ErrorAction SilentlyContinue)
if ($existingProcesses.Count -ne 0) {
    throw "Close the running desk-note instance before measuring performance."
}

dotnet run --project tools/DeskNote.PerformanceData/DeskNote.PerformanceData.csproj -- $dataDirectory 1000
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create performance data."
}

$process = $null
try {
    $startedAt = Get-Date
    $previousStateRoot = $env:DESKNOTE_STATE_ROOT
    try {
        $env:DESKNOTE_STATE_ROOT = $StateRoot
        $process = Start-Process -FilePath $resolvedExecutable -PassThru -WindowStyle Hidden
    }
    finally {
        if ($null -eq $previousStateRoot) {
            Remove-Item Env:DESKNOTE_STATE_ROOT -ErrorAction SilentlyContinue
        }
        else {
            $env:DESKNOTE_STATE_ROOT = $previousStateRoot
        }
    }

    $ready = $false
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        $process.Refresh()
        if ($process.HasExited) {
            throw "desk-note exited before its main window became ready."
        }

        if ($process.MainWindowHandle -ne 0) {
            $ready = $true
            break
        }

        Start-Sleep -Milliseconds 50
    }

    if (-not $ready) {
        throw "desk-note did not expose a main window within 5 seconds."
    }

    $readyAt = Get-Date
    $process.Refresh()
    $cpuBefore = $process.TotalProcessorTime.TotalSeconds
    $sampleStartedAt = Get-Date
    Start-Sleep -Seconds 60
    $process.Refresh()
    if ($process.HasExited) {
        throw "desk-note exited during the idle performance sample."
    }

    $sampleEndedAt = Get-Date
    $cpuAfter = $process.TotalProcessorTime.TotalSeconds
    $elapsedSeconds = ($sampleEndedAt - $sampleStartedAt).TotalSeconds
    $cpuPercent = (($cpuAfter - $cpuBefore) / ($elapsedSeconds * [Environment]::ProcessorCount)) * 100
    $memorySample = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process |
        Where-Object { $_.IDProcess -eq $process.Id } |
        Select-Object -First 1
    if ($null -eq $memorySample) {
        throw "Failed to read the private working set for desk-note."
    }

    [pscustomobject]@{
        StartupMilliseconds = [math]::Round(($readyAt - $startedAt).TotalMilliseconds, 0)
        IdleCpuPercent = [math]::Round($cpuPercent, 3)
        PrivateWorkingSetMb = [math]::Round($memorySample.WorkingSetPrivate / 1MB, 1)
        ProcessId = $process.Id
        StateRoot = $StateRoot
    }
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }
}
