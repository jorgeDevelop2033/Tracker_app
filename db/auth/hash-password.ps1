<#
.SYNOPSIS
    Genera un PasswordHash compatible con el servicio Auth del ERP.

.DESCRIPTION
    Reproduce exactamente Seguridad.Security.PasswordService.HashPassword:
      PBKDF2-HMAC-SHA256, salt aleatorio de 16 bytes, 210.000 iteraciones,
      clave derivada de 32 bytes, y el pepper concatenado a la contraseña.

    Formato de salida:  pbkdf2-sha256:{iteraciones}${saltBase64}${hashBase64}

    El PEPPER debe ser el MISMO que usa el servicio Auth del ambiente destino
    (variable de entorno / secreto). Si el ambiente no define pepper, omita
    el parámetro.

.EXAMPLE
    .\hash-password.ps1 -Password 'MiClaveSegura'

.EXAMPLE
    .\hash-password.ps1 -Password 'MiClaveSegura' -Pepper $env:PasswordHashing__Pepper

.NOTES
    Requiere PowerShell 7+ (usa Rfc2898DeriveBytes con HashAlgorithmName).
    Verifique el pepper del ambiente antes de generar hashes para UAT/PROD:
    un pepper distinto produce un hash que NO validará al iniciar sesión.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$Pepper = '',

    [int]$Iterations = 210000,
    [int]$SaltSize   = 16,
    [int]$KeySize    = 32
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# AppendPepper: el servicio concatena password + pepper y toma los bytes UTF-8.
$toHash = $Password + $Pepper

$salt = [byte[]]::new($SaltSize)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)

$kdf = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
    [System.Text.Encoding]::UTF8.GetBytes($toHash),
    $salt,
    $Iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256
)

try {
    $key = $kdf.GetBytes($KeySize)
}
finally {
    $kdf.Dispose()
}

$hash = 'pbkdf2-sha256:{0}${1}${2}' -f $Iterations,
    [Convert]::ToBase64String($salt),
    [Convert]::ToBase64String($key)

# Los avisos van por stderr/informational para que stdout contenga SOLO el hash
# y pueda capturarse por pipeline:  $h = .\hash-password.ps1 -Password '...'
if ([string]::IsNullOrEmpty($Pepper)) {
    Write-Warning 'Se genero SIN pepper. Si el ambiente destino define uno, el login fallara.'
}
Write-Information 'Pegue el hash en @AdminPasswordHash / @FlotaPasswordHash de 04_tracker_admin.sql' -InformationAction Continue

# Única salida a stdout
Write-Output $hash
