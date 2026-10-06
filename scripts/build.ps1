$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$tests = Join-Path $projectRoot 'tests\SwitchProXInput.Core.Tests\SwitchProXInput.Core.Tests.csproj'
$app = Join-Path $projectRoot 'src\SwitchProXInput.App\SwitchProXInput.App.csproj'
$publish = Join-Path $projectRoot 'artifacts\publish'

Write-Host 'Step 1/2: run the mapping and deadzone checks.'
dotnet run --project $tests --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Core checks failed with exit code $LASTEXITCODE."
}

Write-Host 'Step 2/2: publish the self-contained Windows x64 app.'
dotnet publish $app --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published app: $publish\SwitchProXInput.exe"
