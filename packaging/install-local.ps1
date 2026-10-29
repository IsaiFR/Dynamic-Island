param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$WithPackageIdentity
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectFile = Join-Path $projectRoot 'src\DynamicIsland.App\DynamicIsland.App.csproj'
$publishDirectory = Join-Path $projectRoot ("artifacts\publish\win-{0}" -f $Platform.ToLowerInvariant())
$buildDirectory = Join-Path $projectRoot ("src\DynamicIsland.App\bin\{0}\Release\net10.0-windows10.0.26100.0\win-{1}" -f $Platform, $Platform.ToLowerInvariant())
$installationDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Dynamic Island'
$executablePath = Join-Path $installationDirectory 'DynamicIsland.App.exe'
$iconPath = Join-Path $installationDirectory 'Assets\DynamicIsland.ico'
$WithPackageIdentity = $WithPackageIdentity -or [bool](Get-AppxPackage -Name 'Kikef.DynamicIsland' -ErrorAction SilentlyContinue)
$identityOption = if ($WithPackageIdentity) { 'true' } else { 'false' }
$manifestTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\mt.exe'

dotnet publish $projectFile -c Release -p:Platform=$Platform -p:WithPackageIdentity=$identityOption -r ("win-{0}" -f $Platform.ToLowerInvariant()) --self-contained true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo publicar Dynamic Island.'
}

# La publicación sin MSIX de WinUI puede omitir sus recursos XAML compilados.
Get-ChildItem -LiteralPath $buildDirectory -File -Recurse |
    Where-Object { $_.Extension -in '.xbf', '.pri' } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($buildDirectory.Length + 1)
        $destination = Join-Path $publishDirectory $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }

if ($WithPackageIdentity) {
    if (-not (Test-Path -LiteralPath $manifestTool)) {
        throw "Falta la herramienta del Windows SDK: $manifestTool"
    }

    $publishedExecutable = Join-Path $publishDirectory 'DynamicIsland.App.exe'
    $generatedManifest = Join-Path $publishDirectory 'DynamicIsland.App.generated.manifest'
    $identityManifest = Join-Path $publishDirectory 'DynamicIsland.App.identity.manifest'
    $sparseManifestPath = Join-Path $projectRoot 'packaging\identity\AppxManifest.xml'

    # Windows App SDK genera un manifiesto grande de activación WinRT y sustituye
    # ApplicationManifest; conservarlo al insertar la identidad del paquete.
    & $manifestTool "-inputresource:$publishedExecutable;#1" "-out:$generatedManifest"
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudo extraer el manifiesto del ejecutable.'
    }

    $manifest = [xml](Get-Content -LiteralPath $generatedManifest -Raw)
    $sparse = [xml](Get-Content -LiteralPath $sparseManifestPath -Raw)
    $assembly = $manifest.DocumentElement
    $packageIdentity = $sparse.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Identity"]')
    $application = $sparse.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Applications"]/*[local-name()="Application"]')
    if (-not $assembly -or -not $packageIdentity -or -not $application) {
        throw 'Falta identidad o aplicación en el manifiesto del paquete.'
    }

    $msixNamespace = 'urn:schemas-microsoft-com:msix.v1'
    $existingIdentity = $assembly.SelectSingleNode("*[local-name()='msix' and namespace-uri()='$msixNamespace']")
    if ($existingIdentity) {
        [void]$assembly.RemoveChild($existingIdentity)
    }
    $msix = $manifest.CreateElement('msix', $msixNamespace)
    $msix.SetAttribute('publisher', $packageIdentity.Publisher)
    $msix.SetAttribute('packageName', $packageIdentity.Name)
    $msix.SetAttribute('applicationId', $application.Id)
    [void]$assembly.AppendChild($msix)
    $manifest.Save($identityManifest)

    & $manifestTool "-manifest" $identityManifest "-outputresource:$publishedExecutable;#1"
    if ($LASTEXITCODE -ne 0) {
        throw 'No se pudo incrustar la identidad MSIX en el ejecutable.'
    }
}

$runningInstallation = Get-Process DynamicIsland.App -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $executablePath }
foreach ($runningProcess in $runningInstallation) {
    try {
        $runningProcess.Kill()
        $runningProcess.WaitForExit(5000) | Out-Null
    } catch [System.InvalidOperationException] {
        # El proceso terminó entre la búsqueda y el cierre.
    }
    if (Get-Process -Id $runningProcess.Id -ErrorAction SilentlyContinue) {
        throw "No se pudo cerrar la instalación anterior (PID $($runningProcess.Id))."
    }
}

New-Item -ItemType Directory -Path $installationDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $publishDirectory -Force |
    Copy-Item -Destination $installationDirectory -Recurse -Force

if (-not (Test-Path -LiteralPath $executablePath)) {
    throw 'No se encontró el ejecutable publicado.'
}

$shell = New-Object -ComObject WScript.Shell
$shortcutDirectories = @(
    [Environment]::GetFolderPath('DesktopDirectory'),
    [Environment]::GetFolderPath('Programs')
)
foreach ($shortcutDirectory in $shortcutDirectories) {
    if (-not (Test-Path -LiteralPath $shortcutDirectory)) {
        New-Item -ItemType Directory -Path $shortcutDirectory -Force | Out-Null
    }

    $shortcutPath = Join-Path $shortcutDirectory 'Dynamic Island.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executablePath
    $shortcut.WorkingDirectory = $installationDirectory
    $shortcut.IconLocation = "$iconPath,0"
    $shortcut.Description = 'Dynamic Island para Windows 11'
    $shortcut.Save()
    Write-Output "Acceso directo: $shortcutPath"
}

Write-Output "Aplicación instalada: $executablePath"
