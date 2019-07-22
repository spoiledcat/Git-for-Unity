<#
.SYNOPSIS
.DESCRIPTION
#>
[CmdletBinding()]

Param(
    [switch]
    $Appveyor = $false
    ,
    [switch]
    $Yamato = $false
    ,
    [int]
    $BuildNumber = -1
    ,
    [switch]
    $Trace = $false
)

Set-StrictMode -Version Latest
if ($Trace) {
    Set-PSDebug -Trace 1
}

. $PSScriptRoot\common.ps1 | out-null
$env:PATH = "$scriptsDirectory;$env:PATH"

Push-Location $rootDirectory

$BuildFolder = $rootDirectory

if ($AppVeyor) {
    $BuildFolder = $env:appveyor_build_folder
}

$version = Get-Content "$($BuildFolder)\common\SolutionInfo.cs" | %{ $regex = "private const string [\w]+Version = `"([^`"]*)`""; if ($_ -match $regex) { $matches[1] } }

$json = Get-Content "$($BuildFolder)\src\api\package.json" | ConvertFrom-Json
if ($json.version.Contains("-preview")) {
    $version = "$($version)-preview"
}

if ($BuildNumber -gt -1) {
    $version="$($version).$($BuildNumber)"
} elseif ($AppVeyor) {
    $version="$($version).$($env:APPVEYOR_BUILD_NUMBER)"
    Update-AppveyorBuild -Version $version
} elseif ($Yamato) {
    $buildId = $env:BOKKEN_RESOURCEGROUPNAME | %{ $regex = "[^\d]*([\d]*)"; if ($_ -match $regex) { $matches[1] } };
    $version="$($version).$($buildId)"
}

$json.version = $version
ConvertTo-Json $json | Set-Content "$($BuildFolder)\src\api\package.json"

$env:package_version = $version

return $env:package_version
