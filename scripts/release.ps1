param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Directory.Build.props"
[xml]$xml = Get-Content $propsPath
$node = $xml.SelectSingleNode("//Version")
if (-not $node) {
    throw "Version node not found in Directory.Build.props"
}

$parts = $node.InnerText.Split(".") | ForEach-Object { [int]$_ }
while ($parts.Count -lt 3) { $parts += 0 }
switch ($Bump) {
    "major" { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
    "minor" { $parts[1]++; $parts[2] = 0 }
    "patch" { $parts[2]++ }
}
$version = "{0}.{1}.{2}" -f $parts[0], $parts[1], $parts[2]
$node.InnerText = $version
$xml.Save($propsPath)

Push-Location $root
try {
    git add Directory.Build.props
    git commit -m "Release v$version"
    git tag "v$version"
    git push
    git push --tags
    Write-Host "Tagged and pushed v$version. GitHub Actions will pack and publish the release."
}
finally {
    Pop-Location
}
