@echo off
setlocal
title Wynnyng - Gerar e jogar
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build-and-Play.ps1" %*
if errorlevel 1 (
  echo.
  echo Nao foi possivel gerar o jogo. Veja a mensagem acima e os logs em work\WindowsBuild.
  pause
  exit /b 1
)
endlocal
