@echo off
rem Fica numa pasta propria (cli) no PATH: assim "mexerica" chama este arquivo, que espera o programa terminar.
if "%~1"=="" goto ajuda
"%~dp0..\Mexerica.exe" %*
exit /b %errorlevel%
:ajuda
"%~dp0..\Mexerica.exe" ajuda
exit /b %errorlevel%
