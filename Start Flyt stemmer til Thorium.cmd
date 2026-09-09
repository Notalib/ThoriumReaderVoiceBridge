@echo off
setlocal

REM Start VBS som admin (UAC) fra embedded files-mappen
powershell.exe -NoProfile -Command ^
  "Start-Process -FilePath wscript.exe -Verb RunAs -ArgumentList @('%b2eincfilepath%\run.vbs')"

exit /b