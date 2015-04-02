@echo off
start ..\..\Majordomo\MDPBrokerProcess\bin\Debug\MDPBrokerProcess.exe
start TitanicBrokerProcess\bin\Debug\TitanicBrokerProcess.exe -v
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v
set/p runs="How many messages shall be send? "
start TitanicClientExample\bin\Debug\TitanicClientExample.exe -n%runs%
