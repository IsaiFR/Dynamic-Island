param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$identityDirectory = Join-Path $PSScriptRoot 'identity'
$artifactDirectory = Join-Path $projectRoot 'artifacts\identity'
$packageContentDirectory = Join-Path $artifactDirectory 'layout'
$packagePath = Join-Path $artifactDirectory 'DynamicIsland.Identity.msix'
$publicCertificatePath = Join-Path $artifactDirectory 'DynamicIsland.Local.cer'
$installationDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Dynamic Island'
$sdkTools = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'
$makeAppx = Join-Path $sdkTools 'makeappx.exe'
$signTool = Join-Path $sdkTools 'signtool.exe'
$certificateSubject = 'CN=Kikef Dynamic Island Local'

foreach ($tool in @($makeAppx, $signTool)) {
    if (-not (Test-Path -LiteralPath $tool)) {
        throw "Falta una herramienta del Windows SDK: $tool"
    }
}

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $certificateSubject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30)
    } |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $certificateSubject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
Export-Certificate -Cert $certificate -FilePath $publicCertificatePath -Force | Out-Null

# Build the package layout from versioned inputs; generated packages and staging
# files stay in artifacts rather than being mixed into the manifest directory.
$packageAssetsDirectory = Join-Path $packageContentDirectory 'Assets'
New-Item -ItemType Directory -Path $packageAssetsDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $identityDirectory 'AppxManifest.xml') -Destination (Join-Path $packageContentDirectory 'AppxManifest.xml') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\DynamicIsland.App\Assets\DynamicIsland.png') -Destination (Join-Path $packageAssetsDirectory 'DynamicIsland.png') -Force

& $makeAppx pack /o /d $packageContentDirectory /nv /p $packagePath
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo crear el paquete de identidad.'
}

& $signTool sign /fd SHA256 /sha1 $certificate.Thumbprint $packagePath
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo firmar el paquete de identidad.'
}

$registered = Get-AppxPackage -Name 'Kikef.DynamicIsland'
if (-not $registered) {
    $trustedCertificate = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
        Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } |
        Select-Object -First 1
    if (-not $trustedCertificate) {
        throw "Falta confiar el certificado público en Cert:\LocalMachine\TrustedPeople: $publicCertificatePath"
    }

    Add-AppxPackage -Path $packagePath -ExternalLocation $installationDirectory
}

$registered = Get-AppxPackage -Name 'Kikef.DynamicIsland'
if (-not $registered) {
    throw 'Windows no registró la identidad de Dynamic Island.'
}

& (Join-Path $PSScriptRoot 'install-local.ps1') -Platform $Platform -WithPackageIdentity

Write-Output "Identidad registrada: $($registered.PackageFamilyName)"
Write-Output "Certificado local: $($certificate.Thumbprint)"
