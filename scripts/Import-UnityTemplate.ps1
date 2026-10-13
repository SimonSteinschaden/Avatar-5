param(
    [Parameter(Mandatory = $true)]
    [string] $TemplateProject,
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)
$ErrorActionPreference = 'Stop'
$TemplateProject = (Resolve-Path $TemplateProject).Path
if (-not (Test-Path (Join-Path $TemplateProject 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Source must be a Unity Hub project created from the 3D (URP) template.'
}
if (Test-Path (Join-Path $RepositoryRoot 'ProjectSettings/ProjectVersion.txt')) {
    throw 'Target already contains Unity ProjectSettings. Refusing to overwrite.'
}
Copy-Item (Join-Path $TemplateProject 'Packages') $RepositoryRoot -Recurse
Copy-Item (Join-Path $TemplateProject 'ProjectSettings') $RepositoryRoot -Recurse
$targetAssets = Join-Path $RepositoryRoot 'Assets'
New-Item -ItemType Directory -Path $targetAssets -Force | Out-Null
Get-ChildItem (Join-Path $TemplateProject 'Assets') -Force | ForEach-Object {
    if (Test-Path (Join-Path $targetAssets $_.Name)) {
        throw "Template path would overwrite existing Avatar5 asset: $($_.Name)"
    }
    Copy-Item $_.FullName $targetAssets -Recurse -Force
}
Write-Host 'URP template imported. Open RepositoryRoot in Unity Hub, then choose Avatar5 > Setup > Generate Starter Scenes.'
