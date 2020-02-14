function PrintMessageAndExit($ErrorMessage, $ReturnCode)
{
    Write-Host $ErrorMessage
    if (!$Force)
    {
        Pause
    }
    exit $ReturnCode
}

$ErrorCodes = Data {
    ConvertFrom-StringData @'
    Success = 0
    NoScriptPath = 1
'@
}

$ScriptPath = $null
try
{
    $ScriptPath = (Get-Variable MyInvocation).Value.MyCommand.Path
    $ScriptDir = Split-Path -Parent $ScriptPath
}
catch {}

if (!$ScriptPath)
{
    PrintMessageAndExit "Can't get current directory" $ErrorCodes.NoScriptPath
}

$Package = Get-AppxPackage -name '07220b98-ffa5-4000-9f7c-e168a00899a6'
if($Package){
	Remove-AppxPackage $Package.PackageFullName;
	Write-Host ("Removed BlueMuse " + $Package.Version)
}

$InstallScript = Join-Path $ScriptDir "./Install.ps1"
Write-Host ("Running: " + $InstallScript)
& $InstallScript

