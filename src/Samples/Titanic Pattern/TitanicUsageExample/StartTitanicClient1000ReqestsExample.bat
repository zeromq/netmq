start ..\..\Majordomo\MDPBrokerProcess\bin\Debug\MDPBrokerProcess.exe
start TitanicBrokerProcess\bin\Debug\TitanicBrokerProcess.exe -v
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v
set /p continue=
start TitanicClientExample\bin\Debug\TitanicClientExample.exe 1000
