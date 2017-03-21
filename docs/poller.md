Pollers
===

## Motivation 1: Efficiency

NetMQPoller有很多範例。首先讓我們來看一個簡單的伺服器：

    :::csharp
    using (var rep = new ResponseSocket("@tcp://*:5002"))
    {
        // process requests, forever...
        while (true)
        {
            // receive a request message
            var msg = rep.ReceiveFrameString();

            // send a canned response
            rep.Send("Response");
        }
    }

這個伺服器會很娛快的永遠處理回應。

如果我們想在同一個執行緒中處理兩個不同的response sockets中呢？

    :::csharp
    using (var rep1 = new ResponseSocket("@tcp://*:5001"))
    using (var rep2 = new ResponseSocket("@tcp://*:5002"))
    {
        while (true)
        {
            // Hmmm....
        }
    }

我們要如何公平的處理兩個response sockets的服務？不能一次處理一個嗎？

    :::csharp
    // blocks until a message is received
    var msg1 = rep1.ReceiveString();

    // might never reach this code!
    var msg2 = rep2.ReceiveString();

一個等待接收的函式會阻塞直到有訊息抵達。如果我們在**rep1**等待接收，那傳送給**rep2**的所有訊息會被忽略，直到**rep1**收到訊息-也可能永遠收不到，所以這當然不是一個好方法。

相反的，我們可以在**rep1**和**rep2**上用非阻塞式的接收函式，但這可能會在沒有訊息的狀況下讓當前CPU的負載過高，所以，這也不是一個好方法…

我們可以引進使用非阻塞式函式中的timeout參數。然而，什麼值比較合適呢？如果我們用10ms，那如果**rep1**沒有收到訊息，那**rep2**最多只能取得每秒100個訊息(反之也成立)，這嚴重限制了吞吐量，而且無法有效地利用資源。

所以我們需要一個較好的方式。

## Motivation 2: Correctness

接續上面的範例，也許你會考慮每個socket放在不同的執行緒當中，並且採用阻塞式呼叫，雖然這在一些狀況下是個好方法，但是它有一些限制。

對ZeroMQ/NetMQ來說，為了發揮最大效能，所存在的限制是我們使用socket的方式。特別地說，`NetMQSocket`不是執行緒安全的，在多個執行緒中同步使用同一個socket是無效的。

舉例來說，考慮我們在Thread A中有一個socket A的迴圈在服務，在Thread B中有一個socket B的迴圈在服務，若試著在socket A中接收訊息，並傳送至socket B，是無效的。Socket不是執行緒安全的，所以試著在執行緒A和B中同步使用可能會導致錯誤。

事實上，這裡描述的模式被稱為[proxy](proxy.md)，並且也被內置在NetMQ中。在這一點上，你可能不會訝異地發現它由`NetMQPoller`來實作。

## Example: ReceiveReady

讓我們使用一個`Poller`來從一個執行緒簡單地服務兩個sockets：

    :::csharp
    using (var rep1 = new ResponseSocket("@tcp://*:5001"))
    using (var rep2 = new ResponseSocket("@tcp://*:5002"))
    using (var poller = new NetMQPoller { rep1, rep2 })
    {
        // these event will be raised by the Poller
        rep1.ReceiveReady += (s, a) =>
        {
            // receive won't block as a message is ready
            string msg = a.Socket.ReceiveString();
            // send a response
            a.Socket.Send("Response");
        };
        rep2.ReceiveReady += (s, a) =>
        {
            // receive won't block as a message is ready
            string msg = a.Socket.ReceiveString();
            // send a response
            a.Socket.Send("Response");
        };

        // start polling (on this thread)
        poller.Run();
    }

這段程式設置了兩個sockets，並綁定到不同的位址，並在一個`NetMQPoller`中使用集合初始化加入這兩個sockets(也可以使用`Add(NetMQSocket)`函式)，並在各別socket的`ReceiveReady`事件加上處理函式，最後poller由`Run()`啟動，並開始阻塞直到Poller的`Stop`函式被呼叫為止。

在內部，`NetMQPoller`以最佳方式解決上述問題。

## Example: SendReady

**TODO** add a realistic example showing use of the `SendReady` event.

## Timers

Pollers有一個額外的功能：Timer。

如果你需要在一個執行緒當中對一或多個sockets，執行一些週期性的操作，你可以在`NetMQPoller`中加上一個`NetMQTimer`。

這個範例會每秒推送一個訊息至所有已連線的端點。

    :::csharp
    var timer = new NetMQTimer(TimeSpan.FromSeconds(1));

    using (var pub = new PublisherSocket("@tcp://*:5001"))
    using (var poller = new NetMQPoller { pub, timer })
    {
        pub.ReceiveReady += (s, a) => { /* ... */ };

        timer.Elapsed += (s, a) =>
        {
            pub.Send("Beep!");
        };

        poller.Run();
    }

## Adding/removing sockets/timers

Sockets和timers在執行時可以被安全的加入至或從Poller中移除。

注意`NetMQSocket`,`NetMQActor`and `NetMQBeacon`都實作了`ISocketPollable`，所以`NetMQPoller`可以監示所有這些型別。

* `Add(ISocketPollable)`
* `Remove(ISocketPollable)`
* `Add(NetMQTimer)`
* `Remove(NetMQTimer)`
* `Add(System.Net.Sockets.Socket, Action<Socket>)`
* `Remove(System.Net.Sockets.Socket)`

## Controlling polling

到目前為止，我們學到了`Run函式`。這讓執行緒用於輪詢活動，直到`Poller`被從`socket/timer`事件處理程序或從另一個執行緒中取消。

如果您希望繼續使被調用執行緒進行其他操作，可以呼叫`RunAsync`，它會在新執行緒中呼叫`Run`。

要停止`Poller`，請使用`Stop`或`StopAsync`。後者會等待直到`Poller`的迴圈在返回之前完全離開，這在軟體完整的離開前是必需的。

## A more complex example

讓我們看一個較複雜的範例，其中會使用我們目前為止看到的大部分工具。我們在接收到第一條訊息時將從`NetMQPoller`中刪除一個`ResponseSocket`，即使訊息是正確的，`ReceiveReady`也不會被觸發。

    :::csharp
    using (var rep = new ResponseSocket("@tcp://127.0.0.1:5002"))
    using (var req = new RequestSocket(">tcp://127.0.0.1:5002"))
    using (var poller = new NetMQPoller { rep })
    {
        // this event will be raised by the Poller
        rep.ReceiveReady += (s, a) =>
        {
            bool more;
            string messageIn = a.Socket.ReceiveFrameString(out more);
            Console.WriteLine("messageIn = {0}", messageIn);
            a.Socket.SendFrame("World");

            // REMOVE THE SOCKET!
            poller.Remove(a.Socket);
        };

        // start the poller
        poller.RunAsync();

        // send a request
        req.SendFrame("Hello");

        bool more2;
        string messageBack = req.ReceiveFrameString(out more2);
        Console.WriteLine("messageBack = {0}", messageBack);

        // SEND ANOTHER MESSAGE
        req.SendFrame("Hello Again");

        // give the message a chance to be processed (though it won't be)
        Thread.Sleep(1000);
    }

輸出如下：

    :::text
    messageIn = Hello
    messageBack = World

看到為什麼`Hello Again`沒有收到嗎？這是因為在`RecieiveReady`中處理第一條訊息時將`ResponseSocket`從`NetMQPoller`中移除。


## Performance

使用`poller`接收消息比在socket上直接呼叫`Receive`函式慢。當處理數千條訊息時，第二個或更多的`poller`可能是瓶頸。但是解決方案很簡單，我們只需要使用`Try *`函式獲取當前可用的socket的所有訊息。以下是一個範例：

    :::csharp
    rep1.ReceiveReady += (s, a) =>
    {
        string msg;
        // receiving all messages currently available in the socket before returning to the poller
        while (a.Socket.TryReceiveFrameString(out msg))
        {
            // send a response
            a.Socket.Send("Response");
        }
    };

如果socket載入了不會停止的訊息串流，則上述解決方案可能導致其他socket的Starving。要解決這個問題，你可以限制一個批次中可以提取的訊息數量。

    :::csharp
    rep1.ReceiveReady += (s, a) =>
    {
        string msg;
        //  receiving 1000 messages or less if not available
        for (int count = 0; count < 1000; i++)
        {
            // exit the for loop if failed to receive a message
            if (!a.Socket.TryReceiveFrameString(out msg))
                break;
                
            // send a response
            a.Socket.Send("Response");
        }
    };

## Further Reading

A good place to look for more information and code samples is the [`Poller` unit test source](https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/PollerTests.cs).
