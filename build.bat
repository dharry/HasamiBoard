@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\HasamiBoard\HasamiBoard.csproj"
set "PUBLISH_DIR=%ROOT%publish"
set ISCC="C:\Program Files\Inno Setup 7\ISCC.exe"

echo === HasamiBoard build ===

echo [1/3] dotnet publish (Release, win-x64)
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false -o "%PUBLISH_DIR%" --nologo
if errorlevel 1 (
    echo [ERROR] HasamiBoard Release Build failed!
    exit /b 1
)

echo [2/3] Building Inno Setup installer
%ISCC% "%ROOT%installer\installer.iss"
if errorlevel 1 (
    echo [ERROR] Inno Setup compilation failed!
    exit /b 1
)

:done
echo [3/3] Done

echo.
echo ===================================================
echo  SUCCESS ^(%DATE% %TIME%^)
echo ===================================================

if /I "%1"=="all" (
	pushd "%ROOT%dist"
		.\HasamiBoardSetup.exe /SILENT
	popd
)
endlocal
