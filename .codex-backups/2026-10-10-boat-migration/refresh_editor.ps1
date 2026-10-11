$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TutorialEditorWindow {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint first, uint second, bool attach);
}
'@
$tutorialEditor = Get-Process Unity | Where-Object { $_.MainWindowTitle -like 'CraftFight - *' } | Select-Object -First 1
if ($null -eq $tutorialEditor) { throw 'CraftFight Unity window not found' }
$tutorialWindow = $tutorialEditor.MainWindowHandle
[uint32]$tutorialForegroundProcess = 0
$tutorialForegroundThread = [TutorialEditorWindow]::GetWindowThreadProcessId([TutorialEditorWindow]::GetForegroundWindow(), [ref]$tutorialForegroundProcess)
$tutorialCurrentThread = [TutorialEditorWindow]::GetCurrentThreadId()
$tutorialAttached = [TutorialEditorWindow]::AttachThreadInput($tutorialCurrentThread, $tutorialForegroundThread, $true)
try {
    [TutorialEditorWindow]::ShowWindow($tutorialWindow, 9) | Out-Null
    [TutorialEditorWindow]::SetForegroundWindow($tutorialWindow) | Out-Null
} finally {
    if ($tutorialAttached) { [TutorialEditorWindow]::AttachThreadInput($tutorialCurrentThread, $tutorialForegroundThread, $false) | Out-Null }
}
Start-Sleep -Milliseconds 700
[uint32]$tutorialActualProcess = 0
[TutorialEditorWindow]::GetWindowThreadProcessId([TutorialEditorWindow]::GetForegroundWindow(), [ref]$tutorialActualProcess) | Out-Null
if ($tutorialActualProcess -ne $tutorialEditor.Id) { throw 'Unity is not foreground; no keyboard input was sent' }
$tutorialShell = New-Object -ComObject WScript.Shell
$tutorialShell.SendKeys('^r')
Write-Output ('Refreshed CraftFight Unity PID ' + $tutorialEditor.Id)
