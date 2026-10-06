$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$publish = Join-Path $artifactsRoot 'publish'

& (Join-Path $PSScriptRoot 'build.ps1')

$token = [guid]::NewGuid().ToString('N')
$portableStage = Join-Path $env:TEMP "SwitchProXInput-package-app-$token"
$sourceStage = Join-Path $env:TEMP "SwitchProXInput-package-source-$token"
$portableZip = Join-Path $artifactsRoot 'SwitchProXInput-1.0.0-win-x64.zip'
$sourceZip = Join-Path $artifactsRoot 'SwitchProXInput-source-1.0.0.zip'

New-Item -ItemType Directory -Path $portableStage, $sourceStage -Force | Out-Null
try {
    Copy-Item -LiteralPath $publish -Destination (Join-Path $portableStage 'app') -Recurse
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $portableStage
    New-Item -ItemType Directory -Path (Join-Path $portableStage 'scripts') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $portableStage 'third_party') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot 'third_party\THIRD_PARTY.md') -Destination (Join-Path $portableStage 'third_party')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'third_party\LICENSE-HIDMaestro.txt') -Destination (Join-Path $portableStage 'third_party')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-XInput.ps1') -Destination (Join-Path $portableStage 'scripts') -Force

    Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portableZip -CompressionLevel Optimal -Force

    foreach ($rootFile in @('README.md', 'SwitchProXInput.sln', '.gitignore')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $rootFile) -Destination $sourceStage
    }

    foreach ($folder in @('src', 'tests', 'third_party', 'scripts')) {
        $sourceFolder = Join-Path $projectRoot $folder
        foreach ($file in Get-ChildItem -LiteralPath $sourceFolder -File -Recurse) {
            $relative = [IO.Path]::GetRelativePath($sourceFolder, $file.FullName)
            $segments = $relative -split '[\\/]'
            if ($segments -contains 'bin' -or $segments -contains 'obj') {
                continue
            }

            $destination = Join-Path (Join-Path $sourceStage $folder) $relative
            $destinationFolder = Split-Path -Parent $destination
            New-Item -ItemType Directory -Path $destinationFolder -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }

    Compress-Archive -Path (Join-Path $sourceStage '*') -DestinationPath $sourceZip -CompressionLevel Optimal -Force
    Get-Item -LiteralPath $portableZip, $sourceZip | Select-Object FullName,Length
}
finally {
    $tempRoot = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    foreach ($stage in @($portableStage, $sourceStage)) {
        $fullStage = [IO.Path]::GetFullPath($stage)
        $stageLeaf = Split-Path -Leaf $fullStage
        $isInsideTemp = $fullStage.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)
        $isPackageStage = $stageLeaf.StartsWith('SwitchProXInput-package-', [StringComparison]::OrdinalIgnoreCase)
        if ($isInsideTemp -and $isPackageStage) {
            Remove-Item -LiteralPath $fullStage -Recurse -Force
        }
    }
}
