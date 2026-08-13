# This is a PowerShell script that allows you to set a secret value in a given file
# usage:

# given a secret
# .\Set-Secret.ps1 -filePath .\Ui\Assert.cs -Pattern "===REPLACE_ME_WITH_SALT===" -Secret "secret_value"
# .\Set-Secret.ps1 -filePath .\Ui\Assert.cs -Pattern "===REPLACE_ME_WITH_SALT===" -Secret "secret_value" -isRevert

# read secret from file
# .\Set-Secret.ps1 -filePath .\Ui\Assert.cs -Pattern "===REPLACE_ME_WITH_SALT===" -localSecretFilePath "C:\RemoteX_Secret\Salt.txt"
# .\Set-Secret.ps1 -filePath .\Ui\Assert.cs -Pattern "===REPLACE_ME_WITH_SALT===" -localSecretFilePath "C:\RemoteX_Secret\Salt.txt" -isRevert


param (
    [string]$filePath,
    [string]$Pattern,
    [string]$Secret,
    [string]$localSecretFilePath,
    [switch]$isRevert # true then replace $Secret with $Pattern
)

if (!$Pattern) {
    Write-Host "Error: Pattern is empty."
    exit 1
}

# Check if Secret is empty
if (!$Secret) {

    if(!$localSecretFilePath){
        Write-Host "Error: Secret is empty."
        exit 1
    }
    if (Test-Path -Path $localSecretFilePath -PathType Leaf) {
        $Secret = Get-Content $localSecretFilePath
    }
    else {
        exit 0
    }
}

# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

cd ..


# Check if the file exists
if (!(Test-Path -Path $filePath -PathType Leaf)) {
    Write-Host "Error: $filePath does not exist."
    Set-Location $originalDirectory
    exit 2
}


$target = """$Pattern"";"
$replacement = """$Secret"";"

if ($isRevert) {
    $target = $Secret
    $replacement = $Pattern
}

$fileLines = Get-Content $filePath

# Escape special characters in the target string
$escapedTarget = [regex]::Escape($target)

$matched = 0
foreach ($l in $fileLines) {
    if($l -match $escapedTarget) {
        $matched = 1
        break
    }
}
if (!$matched) {
    if($isRevert) {
        Write-Host "Warning: secret string not found in $filePath"
    }
    else{
        Write-Host "Error: $target not found in $filePath"
        Set-Location $originalDirectory
        exit 4
    }
}

# Replace the content of the file
(Get-Content $filePath) -Replace $escapedTarget, $replacement | Set-Content $filePath

# Set the current directory back to the original location
Set-Location $originalDirectory
