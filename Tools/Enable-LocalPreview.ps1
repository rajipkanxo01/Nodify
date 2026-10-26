# Expose the package sources to the Unity project in this checkout.
# Directory junctions keep edits in Assets and the package root in sync.
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $repositoryRoot 'Assets/_Project/Scripts'
New-Item -ItemType Directory -Path $scriptsRoot -Force | Out-Null

foreach ($folderName in @('Editor', 'Runtime')) {
    $sourcePath = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $folderName)).Path
    $linkPath = Join-Path $scriptsRoot $folderName
    $existing = Get-Item -LiteralPath $linkPath -Force -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        if ($existing.LinkType -eq 'Junction' -and $existing.Target -contains $sourcePath) {
            Write-Output "Already linked: $linkPath"
            continue
        }
        throw "Cannot create preview link because a different item already exists at $linkPath."
    }
    New-Item -ItemType Junction -Path $linkPath -Target $sourcePath | Out-Null
    Write-Output "Linked $linkPath -> $sourcePath"
}

Write-Output 'In Unity, refresh Assets and open Window > Dialogue Editor after compilation.'
