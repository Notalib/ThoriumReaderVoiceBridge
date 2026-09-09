Option Explicit

Dim sh, fso, baseDir, ps1
Set sh  = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

' baseDir = mappen hvor denne .vbs ligger (Ressourcer)
baseDir = fso.GetParentFolderName(WScript.ScriptFullName)

ps1 = baseDir & "\Flyt stemmer til Thorium.ps1"

' Kør PowerShell skjult (0), og vent ikke (False)
sh.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps1 & """", 0, False