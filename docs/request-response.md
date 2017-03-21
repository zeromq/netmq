Request / Response
=====

Request/Response 應該是所有`NetMQ` socket 組合中最簡單的一種了。這不是說RequestSocket和ResponseSocket必須總是一起使用，不是的，只是會有很多時候你想將某一種socket和另一種socket一起使用。有一些特定的socket的組合，剛好很適合在一起使用，而RequestSocket和ResponseSocket就是這樣的一個模式。

The particular socket combinations that work well together are all covered in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>. Whilst it may seem a cop out to simply tell you to read more documentation somewhere else, there really is **NO BETTER** documentation on ZeroMQ/NetMQ than you will find in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>, as it covers well known patterns that have been proved in the field and are known to work well.

我們有點離題了，無論如何，這篇文章是關於Request/Response，所以讓我們繼續吧！


## How it works

Request / Response模式是兩個NetMQ sockets協調工作的一個配置。這種組合類似於你在發出一個web request時看到的模式，也就是說，你提出請求，且期望得到回應。

RequestSocket 和 ResponseSocket 是**同步式**、**阻塞**的，如果你試著以錯誤的順序讀取訊息，你會得到一個例外。

你應該使用`RequestSocket`和`ResponseSockets`連結的方式如下：

1. 從`RequestSocket`傳送訊息
1. `ResponseSocket`讀取請求的訊息
1. `ResponseSocket`傳送回應訊息
1.  `RequestSocket`接收來自`ResponseSocket`的訊息

不管你相信與否，你應該已看過這種範例很多次，因為它已是最簡單的示範了。

這裡有一個小範例，其中`RequestSocket`和`ResponseSockets`都在同一個process中，但這可以很容易地放在兩個不同的process中。我們盡可能保持簡單以用於展示的目的。

    :::csharp
    using (var responseSocket = new ResponseSocket("@tcp://*:5555"))
    using (var requestSocket = new RequestSocket(">tcp://localhost:5555"))
    {
        Console.WriteLine("requestSocket : Sending 'Hello'");
        requestSocket.SendFrame("Hello");

        var message = responseSocket.ReceiveFrameString();

        Console.WriteLine("responseSocket : Server Received '{0}'", message);

        Console.WriteLine("responseSocket Sending 'World'");
        responseSocket.SendFrame("World");

        message = requestSocket.ReceiveFrameString();
        Console.WriteLine("requestSocket : Received '{0}'", message);

        Console.ReadLine();
    }

輸出如下：

![](Images/RequestResponse.png)


## Request/Response is blocking

如上所述，`RequestSocket`和`ResponseSocket`是阻塞的，這意味著任何意外的發送或接呼叫將會導致異常。這裡是這種例外的範例。

這個範例中我們試著在`RequestSocket`中執行兩次Send()。

![](Images/RequestResponse2Sends.png)

或者這個範例，我們嘗試執行RecieveString()兩次，但只有一個訊息從RequestSocket傳送。

![](Images/RequestResponse2Receives.png)

所以要小心你用Request/Response模式做了什麼，魔鬼總在細節裡。
