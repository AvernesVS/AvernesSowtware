@echo off
setlocal
echo Building AvernesTreiner v0.9.6 for .NET Framework 4.8
if "%SHVDN2_DLL%"=="" (echo Set SHVDN2_DLL to ScriptHookVDotNet2.dll ^& exit /b 2)
if "%NATIVEUI_DLL%"=="" (echo Set NATIVEUI_DLL to NativeUI.dll ^& exit /b 2)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REF=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8
if not exist "%CSC%" exit /b 2
if not exist "%REF%\mscorlib.dll" exit /b 2
if not exist "..\build" mkdir "..\build"
"%CSC%" /noconfig /target:library /platform:anycpu /optimize+ /nostdlib /out:..\build\AvernesTreiner.dll /reference:"%REF%\mscorlib.dll" /reference:"%SHVDN2_DLL%" /reference:"%NATIVEUI_DLL%" /reference:"%REF%\System.dll" /reference:"%REF%\System.Core.dll" /reference:"%REF%\System.Drawing.dll" /reference:"%REF%\System.Windows.Forms.dll" /reference:"%REF%\System.Web.Extensions.dll" /reference:"%REF%\System.IO.Compression.dll" /reference:"%REF%\System.IO.Compression.FileSystem.dll" /reference:"%REF%\Microsoft.CSharp.dll" /recurse:*.cs
if errorlevel 1 exit /b %ERRORLEVEL%
echo Build succeeded.
endlocal
