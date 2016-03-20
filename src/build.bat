@ECHO OFF
:: Usage:     build.bat [Clean]
::
::  Requires NuGet.exe to get references NuGets prior to command line build.
::  If you prefer to not install NuGet.exe fom https://nuget.org/nuget.exe,
::  build first time from NetMQ.sln using DevStudio, and the referenced NuGet
::  packages will be downloaded for you.
::  After that first build, you can then use this script to build with no errors.
::
@setlocal

set ACTION=Building

:: supports passing in Clean as third argument if "make clean" behavior is desired
SET target=%1
if /i "%target%" == "clean" set ACTION=Cleaning
if NOT "%target%" == "" set target=/t:%target%

:: sets version and vsversion
CALL :discover_Visual_Studio_version

SET solution=NetMQ.sln
SET log=build_%vsversion%.log
SET tools=Microsoft Visual Studio %version%\VC\vcvarsall.bat
SET environment="%programfiles(x86)%\%tools%"
IF NOT EXIST %environment% SET environment="%programfiles%\%tools%"
IF NOT EXIST %environment% GOTO no_tools


ECHO %ACTION% %solution% with DevStudio%vsversion%...

:: save path
@set oldpath=%PATH%

CALL %environment% x86 >> %log%

::  non error if not found, but build may fail
ECHO Restoring referenced NuGet packages
for /f %%n in ('@echo NuGet.exe') do set NUGETEXE=%%~dpf$PATH:n
if NOT "%NUGETEXE%" == "" "%NUGETEXE%" restore netmq.sln

ECHO Configuration=Debug
msbuild /m /v:n /p:Configuration=Debug /p:Platform="Any CPU" %solution% %target% >> %log%
IF errorlevel 1 GOTO error
ECHO Configuration=Release
msbuild /m /v:n /p:Configuration=Release /p:Platform="Any CPU" %solution% %target% >> %log%
IF errorlevel 1 GOTO error

: restore path
@set PATH=%oldpath%
set oldpath=

ECHO %ACTION% complete: %solution% with DevStudio%vsversion%...
GOTO end


:: sets version and vsversion to on disk and product name versions of visual studio
:discover_Visual_Studio_version
for /f "tokens=8" %%v in ('dir /ad "c:\Program Files (x86)\microsoft visual studio *.0"') do if not "%%v" == "" call :validate_full_vs %%v
set /a vsversion = %version:~0,2%
if not "%vsversion%" == "" if %vsversion% gtr 10 set /a vsversion = vsversion + 1
goto :eof
:: make sure this actually have a VC# compiler installed
:validate_full_vs
set ondiskversion=%1
if not exist "c:\Program Files (x86)\microsoft visual studio %ondiskversion%\Common7\Tools\vsvars32.bat" set ondiskversion=
if not exist "c:\Program Files (x86)\microsoft visual studio %ondiskversion%\VC#" set ondiskversion=
if not "%ondiskversion%" == "" set version=%ondiskversion%
goto :eof


:error
if NOT "%oldpath%" == "" set PATH=%oldpath%&set oldpath=
ECHO *** ERROR, build terminated early: see %log%
if "%NUGETEXE%" == "" ECHO *** ERROR, Either install NuGet (https://nuget.org/nuget.exe) or do very first build using DevStudio NetMQ.sln
GOTO end

:no_tools
ECHO *** ERROR, build tools not found: %tools%

:end
@endlocal