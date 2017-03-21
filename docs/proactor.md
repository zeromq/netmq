Proactor
========

`NetMQProactor`會使用專有的執行緒處理在socket上收到的訊息。

    :::csharp
    using (var receiveSocket = new DealerSocket(">tcp://localhost:5555"))
    using (var proactor = new NetMQProactor(receiveSocket, 
            (socket, message) => ProcessMessage(message)))
    {
        // ...
    }

在內部，proactor為socket建了一個`NetMQPoller`，以及一個`NetMQActor`處理poller執行緒及disposal。
