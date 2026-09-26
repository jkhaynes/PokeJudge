<#
.SYNOPSIS
  Turns a scenario evaluation log into the shields.io endpoint badge shown on
  the GitHub profile (docs/badges/eval.json).

.DESCRIPTION
  The scenario evaluation calls a real model, so it runs locally rather than in
  CI. Run it, save the output, then run this script and commit the JSON:

    dotnet run --project PokeJudge -- evaluate | Tee-Object eval.log
    ./scripts/eval-badge.ps1 -Log eval.log
    git add docs/badges/eval.json

  The badge reads the harness's own "Result: X/Y" line, so it can only ever
  show what the evaluation printed. A run with infrastructure failures is
  refused by default: those scenarios were not scored, and a badge built
  from a partial run would overstate coverage.
#>
param(
    [string]$Log = "eval.log",
    [string]$Out = "docs/badges/eval.json",
    [switch]$AllowInfrastructureFailures
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Log)) {
    throw "No evaluation log at '$Log'. Run: dotnet run --project PokeJudge -- evaluate | Tee-Object $Log"
}
$text = Get-Content $Log -Raw

# Single run: "Result: 17/20 scenarios fully passed ..."
# With --repeat: "Result: 50/60 scenario-runs fully passed ..."
$result = [regex]::Matches($text, 'Result: (\d+)/(\d+) (scenarios|scenario-runs) fully passed') | Select-Object -Last 1
if (-not $result) {
    throw "No scenario evaluation 'Result: X/Y' line in '$Log'. Was this the output of 'evaluate'?"
}
$passed = [int]$result.Groups[1].Value
$total = [int]$result.Groups[2].Value
$unit = if ($result.Groups[3].Value -eq "scenario-runs") { "runs" } else { "scenarios" }
if ($total -eq 0) {
    throw "The evaluation scored 0 $unit; nothing to publish."
}

$infra = [regex]::Match($text, 'Infrastructure failures \(not counted above\): (\d+)')
if ($infra.Success -and -not $AllowInfrastructureFailures) {
    throw "$($infra.Groups[1].Value) scenario(s) hit infrastructure failures and were not scored. Re-run them, or pass -AllowInfrastructureFailures."
}

$percent = [math]::Floor(100 * $passed / $total)
# Catppuccin Mocha, matching the profile README.
$color = if ($percent -ge 80) { "A6E3A1" } elseif ($percent -ge 60) { "F9E2AF" } else { "F38BA8" }

# [char] keeps this file ASCII, so Windows PowerShell 5.1 reads it correctly.
$dot = [char]0x00B7

$badge = [ordered]@{
    schemaVersion = 1
    label         = "eval"
    message       = "$percent% $dot $passed/$total $unit pass"
    color         = $color
    labelColor    = "45475A"
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
# UTF-8 without a BOM: shields.io parses the file as JSON.
$full = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path (Get-Location) $Out }
[System.IO.File]::WriteAllText($full, ($badge | ConvertTo-Json) + "`n", (New-Object System.Text.UTF8Encoding $false))
Write-Host "Wrote $Out : $($badge.message)"
