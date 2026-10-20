$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$front = Join-Path $root 'frontend'

Write-Host 'PatientFiles: building local browser viewer' -ForegroundColor Cyan

if (-not (Get-Command node.exe -ErrorAction SilentlyContinue)) {
    throw 'Node.js was not found. Install Node.js 22.13 or newer.'
}
if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw 'npm.cmd was not found.'
}

$ver = (& node.exe -p "process.versions.node").Trim()
Write-Host "Node.js $ver"

Push-Location $front
try {
    & npm.cmd install --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }

    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build failed.' }

    $lic = Join-Path $root 'ViewerDist\licenses'
    New-Item -ItemType Directory -Force -Path $lic | Out-Null

    $copy = @(
        @('node_modules\@zrimo\viewer\LICENSE-MIT', 'ZRIMO-LICENSE-MIT.txt'),
        @('node_modules\@zrimo\viewer\LICENSE-APACHE', 'ZRIMO-LICENSE-APACHE.txt'),
        @('node_modules\@zrimo\viewer\THIRD_PARTY_NOTICES.md', 'ZRIMO-THIRD-PARTY.md'),
        @('node_modules\shrimp-doc-viewer\LICENSE-MIT', 'SHRIMP-LICENSE-MIT.txt'),
        @('node_modules\shrimp-doc-viewer\LICENSE-APACHE', 'SHRIMP-LICENSE-APACHE.txt'),
        @('node_modules\shrimp-doc-viewer\THIRD-PARTY-NOTICES.txt', 'SHRIMP-THIRD-PARTY.txt')
    )

    foreach ($item in $copy) {
        $src = Join-Path $front $item[0]
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $lic $item[1]) -Force
        }
    }
}
finally {
    Pop-Location
}

$entry = Join-Path $root 'ViewerDist\viewer.js'
if (-not (Test-Path $entry)) {
    throw 'Build finished but ViewerDist\viewer.js was not found.'
}

Write-Host 'DONE: ViewerDist is ready.' -ForegroundColor Green
