echo Start Majordomo Broker Process
start ..\Majordomo\MDPBrokerProcess\bin\Debug\MDPBrokerProcess.exe -v
echo Start Titanic Broker Process
start TitanicBroker\bin\Debug\TitanicBroker.exe -v
echo Start Titanic Client Example Process
start TitanicClientExample\bin\Debug\TitanicClientExample.exe -v
echo Start Titanic Worker Example
echo -- Could have been a MDPWorker as well
start TitanicWorkerExample\bin\Debug\TitanicWorkerExample.exe -v