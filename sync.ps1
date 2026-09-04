# 1. Sync internal package
Write-Host 'Syncing to AIPackageInstaller...'
Copy-Item -Path 'Assets\AI Driven NPCs System\Editor\*' -Destination 'AIPackageInstaller\Editor\' -Recurse -Force
Copy-Item -Path 'Assets\AI Driven NPCs System\Scripts\*' -Destination 'AIPackageInstaller\Samples~\Scripts\' -Recurse -Force
Copy-Item -Path 'Assets\AI Driven NPCs System\Prefabs\*' -Destination 'AIPackageInstaller\Samples~\Prefabs\' -Recurse -Force
Copy-Item -Path 'Assets\AI Driven NPCs System\Scenes\*' -Destination 'AIPackageInstaller\Samples~\Scenes\' -Recurse -Force
Copy-Item -Path 'Assets\AI Driven NPCs System\Resources\*' -Destination 'AIPackageInstaller\Samples~\Resources\' -Recurse -Force
Copy-Item -Path 'Assets\AI Driven NPCs System\README.md' -Destination 'AIPackageInstaller\README.md' -Force

# 2. Sync to AITest PackageCache (if present)
$aiTestPkg = Get-Item 'c:\Projects\AITest\Library\PackageCache\com.yildizoguzhan.ai-driven-npcs@*' -ErrorAction SilentlyContinue
if ($aiTestPkg) {
    Copy-Item -Path 'AIPackageInstaller\Editor\*' -Destination $($aiTestPkg.FullName)\Editor" -Recurse -Force
 Copy-Item -Path 'AIPackageInstaller\README.md' -Destination $($aiTestPkg.FullName)\README.md -Force
 Copy-Item -Path 'AIPackageInstaller\Samples~\*' -Destination $($aiTestPkg.FullName)\Samples~" -Recurse -Force
    Write-Host Synced to AITest PackageCache: $($aiTestPkg.FullName)
}

# 3. Sync to AITest imported samples (if present)
$aiTestImported = Get-Item 'c:\Projects\AITest\Assets\Samples\AI Driven NPCs System\*\AI Driven NPCs System' -ErrorAction SilentlyContinue
if ($aiTestImported) {
    Copy-Item -Path 'AIPackageInstaller\Samples~\Scripts\*' -Destination $($aiTestImported.FullName)\Scripts" -Recurse -Force
 Copy-Item -Path 'AIPackageInstaller\Samples~\Prefabs\*' -Destination $($aiTestImported.FullName)\Prefabs" -Recurse -Force
    Write-Host Synced to AITest imported samples: $($aiTestImported.FullName)
}

Write-Host 'Sync completed successfully.'
