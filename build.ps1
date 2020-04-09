Param(
    [Parameter(Position=0)]
    [string]
    $mode = "debug",

    [Parameter(Position=1)]
    [string]
    $version = "0.0.1"
)

$version

$ErrorActionPreference = "Stop";

if($mode -eq "release")
{
    # Release mode, build single file.
    dotnet publish ./src/AutoStep.LanguageServer/ -r win-x64 -o .\artifacts\server\win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
    dotnet publish ./src/AutoStep.LanguageServer/ -r linux-x64 -o .\artifacts\server\linux-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
}
else 
{
    dotnet publish ./src/AutoStep.LanguageServer/ -o .\artifacts\server\portable -c Debug
}

if (Test-Path src/Extension/server)
{
    Remove-Item ./src/Extension/server -Recurse -Force
}

Copy-Item ./artifacts/server ./src/Extension/server -Recurse

# Do the NPM compile.
Push-Location ./src/Extension

npm install -g vsce
npm install

npm run compile

$compileCode = $LASTEXITCODE;

if ($compileCode -ne 0)
{
    exit $compileCode;
}

if ($mode -eq "release")
{
    # Package
    vsce package -o "autostep-$version.vsix"
}

Pop-Location

if($mode -eq "release")
{
    Move-Item ./src/Extension/autostep*.vsix ./artifacts -Force
}