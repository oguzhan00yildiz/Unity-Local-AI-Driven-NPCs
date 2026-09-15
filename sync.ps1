# Sync-DirectoryContents helper: mirrors files from $src to $dst and prunes deletions
function Sync-DirectoryContents ($src, $dst) {
    if (-not (Test-Path $src)) { return }
    if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }

    # Prune stale files in $dst that do not exist in $src
    $dstResolved = (Resolve-Path $dst).Path
    $srcResolved = (Resolve-Path $src).Path
    Get-ChildItem -Path $dst -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($dstResolved.Length).TrimStart('\', '/')
        $srcFile = Join-Path $srcResolved $rel
        if (-not (Test-Path $srcFile)) {
            Remove-Item -Path $_.FullName -Force
        }
    }

    # Prune stale empty directories in $dst
    Get-ChildItem -Path $dst -Recurse -Directory | Sort-Object -Property FullName -Descending | ForEach-Object {
        if ((Get-ChildItem -Path $_.FullName -Force | Measure-Object).Count -eq 0) {
            Remove-Item -Path $_.FullName -Force -Recurse
        }
    }

    Copy-Item -Path "$src\*" -Destination "$dst\" -Recurse -Force
}

# 1. Sync internal package
Write-Host 'Syncing to AIPackageInstaller...'
Sync-DirectoryContents -src 'Assets\AI Driven NPCs System\Editor' -dst 'AIPackageInstaller\Editor'

$srcBase = 'Assets\AI Driven NPCs System'
if (-not (Test-Path "$srcBase\Scripts") -and (Test-Path "$srcBase\.staging~\Scripts")) {
    $srcBase = 'Assets\AI Driven NPCs System\.staging~'
}

if (Test-Path "$srcBase\Scripts") {
    Sync-DirectoryContents -src "$srcBase\Scripts" -dst 'AIPackageInstaller\Samples~\Scripts'
}
if (Test-Path "$srcBase\Prefabs") {
    Sync-DirectoryContents -src "$srcBase\Prefabs" -dst 'AIPackageInstaller\Samples~\Prefabs'
}
if (Test-Path "$srcBase\Scenes") {
    Sync-DirectoryContents -src "$srcBase\Scenes" -dst 'AIPackageInstaller\Samples~\Scenes'
}
if (Test-Path "$srcBase\Resources") {
    Sync-DirectoryContents -src "$srcBase\Resources" -dst 'AIPackageInstaller\Samples~\Resources'
}
if (Test-Path 'Assets\AI Driven NPCs System\README.md') {
    Copy-Item -Path 'Assets\AI Driven NPCs System\README.md' -Destination 'AIPackageInstaller\README.md' -Force
}
if (Test-Path 'Assets\AI Driven NPCs System\ThirdPartyNotices.md') {
    Copy-Item -Path 'Assets\AI Driven NPCs System\ThirdPartyNotices.md' -Destination 'AIPackageInstaller\ThirdPartyNotices.md' -Force
    if (Test-Path 'Assets\AI Driven NPCs System\ThirdPartyNotices.md.meta') {
        Copy-Item -Path 'Assets\AI Driven NPCs System\ThirdPartyNotices.md.meta' -Destination 'AIPackageInstaller\ThirdPartyNotices.md.meta' -Force
    }
}
if (Test-Path 'Assets\AI Driven NPCs System\SETUP_GUIDE_EN.md') {
    Copy-Item -Path 'Assets\AI Driven NPCs System\SETUP_GUIDE_EN.md' -Destination 'AIPackageInstaller\Samples~\SETUP_GUIDE_EN.md' -Force
}

# 2. Sync to AITest Asset Store package directory (if present)
$aiTestAssets = 'c:\Projects\AITest\Assets\AI Driven NPCs System'
if (Test-Path $aiTestAssets) {
    Sync-DirectoryContents -src 'Assets\AI Driven NPCs System\Editor' -dst "$aiTestAssets\Editor"
    Write-Host "Synced to AITest Assets: $aiTestAssets\Editor\"
    if (Test-Path "$aiTestAssets\Scripts") {
        Sync-DirectoryContents -src "$srcBase\Scripts" -dst "$aiTestAssets\Scripts"
        Write-Host "Synced to AITest Assets Scripts: $aiTestAssets\Scripts\"
    }
    if (Test-Path "$aiTestAssets\Prefabs") {
        Sync-DirectoryContents -src "$srcBase\Prefabs" -dst "$aiTestAssets\Prefabs"
        Write-Host "Synced to AITest Assets Prefabs: $aiTestAssets\Prefabs\"
    }
    if (Test-Path "$aiTestAssets\Scenes") {
        Sync-DirectoryContents -src "$srcBase\Scenes" -dst "$aiTestAssets\Scenes"
        Write-Host "Synced to AITest Assets Scenes: $aiTestAssets\Scenes\"
    }
    if (Test-Path "$aiTestAssets\Resources") {
        Sync-DirectoryContents -src "$srcBase\Resources" -dst "$aiTestAssets\Resources"
        Write-Host "Synced to AITest Assets Resources: $aiTestAssets\Resources\"
    }
}

# 3. Sync to AITest PackageCache (if present)
$aiTestPkg = Get-Item 'c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*' -ErrorAction SilentlyContinue
if ($aiTestPkg) {
    Sync-DirectoryContents -src 'AIPackageInstaller\Editor' -dst "$($aiTestPkg.FullName)\Editor"
    Copy-Item -Path 'AIPackageInstaller\README.md' -Destination "$($aiTestPkg.FullName)\README.md" -Force
    if (Test-Path 'AIPackageInstaller\ThirdPartyNotices.md') {
        Copy-Item -Path 'AIPackageInstaller\ThirdPartyNotices.md' -Destination "$($aiTestPkg.FullName)\ThirdPartyNotices.md" -Force
    }
    Sync-DirectoryContents -src 'AIPackageInstaller\Samples~' -dst "$($aiTestPkg.FullName)\Samples~"
    Write-Host "Synced to AITest PackageCache: $($aiTestPkg.FullName)"
}

# 4. Sync to AITest imported samples (if present)
$aiTestImported = Get-Item 'c:\Projects\AITest\Assets\Samples\AI Driven NPCs System\*\AI Driven NPCs System' -ErrorAction SilentlyContinue
if ($aiTestImported) {
    Sync-DirectoryContents -src 'AIPackageInstaller\Samples~\Scripts' -dst "$($aiTestImported.FullName)\Scripts"
    Sync-DirectoryContents -src 'AIPackageInstaller\Samples~\Prefabs' -dst "$($aiTestImported.FullName)\Prefabs"
    Write-Host "Synced to AITest imported samples: $($aiTestImported.FullName)"
}

Write-Host 'Sync completed successfully.'
