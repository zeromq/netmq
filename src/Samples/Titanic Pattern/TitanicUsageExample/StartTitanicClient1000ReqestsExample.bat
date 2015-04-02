@echo off
start ..\..\Majordomo\MDPBrokerProcess\bin\Debug\MDPBrokerProcess.exe
set /p verbose="Shall the test be run verbose? (yY/nN)"
if %verbose%=="y" goto VERBOSE
if %verbose%=="Y" goto VERBOSE
goto SILENT

VERBOSE:
start TitanicBrokerProcess\bin\Debug\TitanicBrokerProcess.exe -v
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v
goto CLIENT

SILENT:
start TitanicBrokerProcess\bin\Debug\TitanicBrokerProcess.exe
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe

CLIENT:
set /p runs="How many messages shall be send? "
start TitanicClientExample\bin\Debug\TitanicClientExample.exe %runs%
