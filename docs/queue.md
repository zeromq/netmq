Queue
======

`NetMQQueue<T>`是一個支援多個生產者及單一消費者的生產者/消費者佇列。

你應該將佇列加至`NetMQPoller`中，且在`ReceiveReady`事件中加上消費者程式碼，而生產者會呼叫`Enque(T)`將資料加入。

This class can eliminate boilerplate code associated with marshalling operations onto a single thread.

    :::csharp
    using (var queue = new NetMQQueue<ICommand>())
    using (var poller = new NetMQPoller { queue })
    {
        queue.ReceiveReady += (sender, args) => ProcessCommand(queue.Dequeue());

        poller.RunAsync();

        // Then, from various threads...
        queue.Enqueue(new DoSomethingCommand());
        queue.Enqueue(new DoSomethingElseCommand());
    }
