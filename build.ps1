[CmdletBinding()]
param(
    [string]$EmbyServerPath = 'D:/emby/system'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $projectRoot 'CoverFixer.sln'
$project = Join-Path $projectRoot 'src/CoverFixer/CoverFixer.csproj'
$output = Join-Path $projectRoot 'dist'

dotnet build $solution -c Release -p:EmbyServerPath=$EmbyServerPath
if ($LASTEXITCODE -ne 0) {
    throw "构建失败，退出码：$LASTEXITCODE"
}

dotnet run --project (Join-Path $projectRoot 'tests/CoverFixer.Tests/CoverFixer.Tests.csproj') -c Release --no-build -p:EmbyServerPath=$EmbyServerPath
if ($LASTEXITCODE -ne 0) {
    throw "测试失败，退出码：$LASTEXITCODE"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/CoverFixer/bin/Release/net8.0/CoverFixer.dll') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/CoverFixer/bin/Release/net8.0/CoverFixer.pdb') -Destination $output -Force

Write-Host "构建完成：$output"
