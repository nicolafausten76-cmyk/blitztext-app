param(
    [Parameter(Mandatory=$true)][string]$KeyStorePath,
    [string]$Alias = 'sprechklar',
    [string]$Keytool = 'keytool'
)
$ErrorActionPreference = 'Stop'
# Choose a private location OUTSIDE any repository, synced knowledge folder or artifact directory.
$absolute = [IO.Path]::GetFullPath($KeyStorePath)
if (Test-Path -LiteralPath $absolute) { throw 'Refusing to overwrite an existing keystore.' }
if (-not (Test-Path -LiteralPath (Split-Path -Parent $absolute))) { throw 'Create the private destination directory first.' }
# Password is requested by keytool on the console; never put it in command-line arguments or logs.
& $Keytool -genkeypair -v -keystore $absolute -storetype PKCS12 -alias $Alias -keyalg RSA -keysize 3072 -validity 10950 -dname 'CN=Sprechklar Private Distribution'
if ($LASTEXITCODE -ne 0) { throw 'Key generation failed.' }
Write-Output 'Keystore created. Back up the keystore and password privately; do not commit or upload as an artifact.'
Write-Output 'Configure the four repository Actions secrets described in ports/docs/SECURITY.md.'
