powershell scripts/Set-BuildVersion.ps1
nuget restore | exit 0
dotnet restore
hMSBuild.bat GitForUnity.sln
