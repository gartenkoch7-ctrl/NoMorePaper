#Requires -Version 5.1
<#
.SYNOPSIS
    Creates the self-signed certificate that encrypts the Data Protection key ring (ADR-0015).
.DESCRIPTION
    Writes a PKCS#12 file (RSA 3072, valid 10 years) protected by a random password and prints the
    values for .env. Keep the file OUTSIDE the repository and the Docker volumes, and back it up
    separately from the data backups (e.g. password manager): without it, restored keys are unreadable.
.EXAMPLE
    ./scripts/new-keyring-certificate.ps1 -OutputDirectory C:\Secrets\nomorepaper
#>
param(
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$FileName = 'keyring.pfx'
)

$ErrorActionPreference = 'Stop'

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
$path = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path $FileName
if (Test-Path -LiteralPath $path) {
    throw "'$path' already exists; refusing to overwrite a certificate that may protect existing keys."
}

# Random password from a cryptographic generator; letters and digits only, so .env needs no quoting.
# Bytes above the largest multiple of the alphabet length are discarded so every character is equally likely.
$alphabet = [char[]]'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789'
$limit = 256 - (256 % $alphabet.Length)
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$password = ''
try {
    $buffer = New-Object byte[] 1
    while ($password.Length -lt 32) {
        $rng.GetBytes($buffer)
        if ($buffer[0] -lt $limit) { $password += $alphabet[$buffer[0] % $alphabet.Length] }
    }
} finally { $rng.Dispose() }

# System.Security.Cryptography works on Windows PowerShell 5.1 and PowerShell 7 alike.
$rsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
        'CN=NoMorePaper key ring', $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $now = [System.DateTimeOffset]::UtcNow
    $certificate = $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddYears(10))
    try {
        $pfx = $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password)
        [System.IO.File]::WriteAllBytes($path, $pfx)
        $thumbprint = $certificate.Thumbprint
    } finally { $certificate.Dispose() }
} finally { $rsa.Dispose() }

Write-Host "Created $path (thumbprint $thumbprint, valid 10 years)."
Write-Host ''
Write-Host 'Add to .env (the password is shown only now; store it with the file, apart from the data backups):'
Write-Host "NOMOREPAPER_KEYRING_CERT=$path"
Write-Host "NOMOREPAPER_KEYRING_CERT_PASSWORD=$password"
