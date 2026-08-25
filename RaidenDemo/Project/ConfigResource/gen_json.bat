set WORKSPACE=..
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=.
set CODE_PATH=%WORKSPACE%\Assets\Scripts\csharp\cfg
set DATA_PATH=%WORKSPACE%\Assets\ConfigBin

dotnet %LUBAN_DLL% ^
    -t client ^
    -d json ^
    -c cs-simple-json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir=%CODE_PATH% ^
    -x outputDataDir=%DATA_PATH%

pause