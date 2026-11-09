@echo off
setlocal
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set ROOT=%~dp0
set OUT=%ROOT%bin
set GEN=%ROOT%..\..\Assets\Scripts\csharp\login\network\protocol
if not exist "%OUT%" mkdir "%OUT%"
if not exist "%GEN%" mkdir "%GEN%"
"%CSC%" /nologo /out:"%OUT%\ProtocolGen.exe" /target:exe "%ROOT%Program.cs"
if errorlevel 1 exit /b 1
"%OUT%\ProtocolGen.exe" "%ROOT%xml" "%GEN%"
exit /b %ERRORLEVEL%
