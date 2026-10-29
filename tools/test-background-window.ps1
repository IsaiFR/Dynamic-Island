param(
    [string]$ExecutablePath = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Dynamic Island\DynamicIsland.App.exe'),
    [long]$ForegroundWindowBefore = 0
)

# Diagnóstico de solo lectura; no activa ventanas ni inyecta entradas de teclado/ratón.
$ErrorActionPreference = 'Stop'
if (-not ('DynamicIslandBackgroundDiagnostics' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DynamicIslandBackgroundDiagnostics
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr window);
}
'@
}

$matchingProcesses = @(Get-Process DynamicIsland.App -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $ExecutablePath })
if ($matchingProcesses.Count -ne 1) {
    throw "Se esperaba una sola instancia en '$ExecutablePath'; encontradas: $($matchingProcesses.Count)."
}
$islandProcess = $matchingProcesses[0]
$windowHandle = $islandProcess.MainWindowHandle
if ($windowHandle -eq 0) { throw 'La isla no tiene una ventana principal visible.' }
$extendedStyle = [DynamicIslandBackgroundDiagnostics]::GetWindowLongPtr($windowHandle, -20).ToInt64()
$style = [DynamicIslandBackgroundDiagnostics]::GetWindowLongPtr($windowHandle, -16).ToInt64()
$foregroundWindow = [DynamicIslandBackgroundDiagnostics]::GetForegroundWindow()
$checks = [ordered]@{
    Visible = [DynamicIslandBackgroundDiagnostics]::IsWindowVisible($windowHandle)
    Topmost = ($extendedStyle -band 0x00000008) -ne 0
    ToolWindow = ($extendedStyle -band 0x00000080) -ne 0
    NoActivate = ($extendedStyle -band 0x08000000) -ne 0
    NotAppWindow = ($extendedStyle -band 0x00040000) -eq 0
    Frameless = ($style -band 0x00C40000) -eq 0
}
if ($foregroundWindow -ne 0) {
    $checks['NotForeground'] = $foregroundWindow -ne $windowHandle
} else {
    Write-Warning 'Este contexto no permite leer la ventana en primer plano; no se valida el foco real.'
}
if ($foregroundWindow -ne 0 -and $ForegroundWindowBefore -ne 0) {
    $checks['ForegroundPreserved'] = $foregroundWindow.ToInt64() -eq $ForegroundWindowBefore
}
[pscustomobject]$checks
if ($checks.Values -contains $false) {
    throw 'Falló una comprobación del comportamiento de fondo.'
}
