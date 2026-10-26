# Migrate older checkouts to the embedded package layout.
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $repositoryRoot 'Assets/_Project/Scripts'
$packageRoot = Join-Path $repositoryRoot 'Packages/com.rajipkanxo01.nodify'
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'package.json'))) {
    throw "Embedded package not found: $packageRoot"
}

foreach ($folderName in @('Editor', 'Runtime')) {
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $folderName))
    $linkPath = Join-Path $scriptsRoot $folderName
    $existing = Get-Item -LiteralPath $linkPath -Force -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        if ($existing.LinkType -eq 'Junction' -and $existing.Target -contains $sourcePath) {
            # Delete only the junction itself, never its target directory.
            [IO.Directory]::Delete($existing.FullName)
            $metaPath = $linkPath + '.meta'
            if (Test-Path -LiteralPath $metaPath) { Remove-Item -LiteralPath $metaPath }
            Write-Output "Removed legacy preview link: $linkPath"
            continue
        }
        throw "A different item exists at $linkPath. Move it out of Assets to avoid duplicate scripts."
    }
}

Write-Output 'The embedded package compiles automatically. In Unity, refresh and open Window > Nodify.'
