介紹
=====

所以你正在找訊息函式庫，也許你對WCF跟MSMQ感到沮喪(我們也是…)，且聽說ZeroMQ很快，所以你找到這裡，NetMQ，一個Zero(或稱ØMQ)的.Net移植。

是的，NetMQ是一個訊息函式庫，而且很快，但需要一點學習時間，期望你能夠快速地掌握它。

## 從那裡開始

`ZeroMQ`和`NetMQ`不是那些你下載後，看一下範例就會的函式庫，它背後有一些原則，要先瞭解後才能順利應用，所以最佳的開始的地方是[ZeroMQ guide](http://zguide.zeromq.org/page:all)，讀一或兩次後，再回來這篇文章。

## ZeroMQ中的Zero

`ZeroMQ`的哲理是從**Zero**開始。**Zero**是指`Zero broker`(`ZeroMQ`沒有中介者)、零延遲、零成本(免費的)及零管理。

更進一步說，**"zero"**代表滲透整個專案的極簡主義的文化。我們通過消除複雜性而不是增加新函式來提供功能。


## 取得函式庫

你可以從[NuGet](https://nuget.org/packages/NetMQ/)取得函式庫。

## 傳送及接收

由於`NetMQ`就是關於`sockets`的，所以傳送及接收是很自然的預期。更由於這屬於NetMQ的一般區域，所以另有一個關於[接收與傳送](https://netmq.readthedocs.io/en/latest/receiving-sending/)的介紹頁面。


## 第一個範例

讓我們開始第一個範例吧，(當然)是**"Hello world"**了：

### Server

    :::csharp
    using (var server = new ResponseSocket())
    {
        server.Bind("tcp://*:5555");

        while (true)
        {
            var message = server.ReceiveFrameString();

            Console.WriteLine("Received {0}", message);

            // processing the request
            Thread.Sleep(100);

            Console.WriteLine("Sending World");
            server.SendFrame("World");
        }
    }

伺服端建立了一個`response`的`socket`型別(在[request-response](request-response.md))章節有更多介紹)，將它綁定到port 5555然後等待訊息。

你可以看到我們不用任何設定，只需要傳送字串。`NetMQ`不只可以傳送字串，雖然它沒有實作序列化的功能(你需要自己實作)，不過你可以在後續學到一些很酷的技巧([Multipart messages](#multipart-messages)。

### Client

    :::csharp
    using (var client = new RequestSocket())
    {
        client.Connect("tcp://localhost:5555");

        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine("Sending Hello");
            client.SendFrame("Hello");

            var message = client.ReceiveFrameString();
            Console.WriteLine("Received {0}", message);
        }
    }

`Client端`建立了一個`request`的`socket`型別，連線並開始傳送訊息。

傳送及接收函式(預設)是阻塞式的。對接收來說很簡單：如果沒有收到訊息它會阻塞；而傳送較複雜一點，且跟它的socket型別有關。對request sockets來說，如果到達high watermark，且沒有另一端的連線，函式會阻塞。

然而你可以呼叫`TrySend`和`TryReceive`以避免等待，如果需要等待，它會回傳false。

    :::csharp
    string message;
    if (client.TryReceiveFrameString(out message))
        Console.WriteLine("Received {0}", message);
    else
        Console.WriteLine("No message received");


## Bind vs Connect

上述範例中你可能會注意到server端使用Bind而client端使用Connect，為什麼？有什麼不同嗎？

`ZeroMQ`為每個潛在的連線建立佇列。如果你的socket連線到三個socket端點，背後實際有三個佇列存在。

使用`Bind`，可以讓其它端點和你建立連接，因為你不知道未來會有多少端點且無法先建立佇列，相反，佇列會在每個端點bound後建立。

使用`Connect`，`ZeroMQ`知道至少會有一個端點，因此它會馬上建立佇列，除了ROUTE型別外的所有型別都是如此，而ROUTE型別只會在我們連線的每個端點有了回應後才建立佇列。

因此，當傳送訊息至沒有綁定的端點的socket或至沒有連線的ROUTE時，將沒有可以儲存訊息的佇列存在。

### When should I use bind and when connect?

作為一般規則，在架構中最穩定的端點上使用**bind**，在動態的、易變的端點上使用**connect**。對request/reply型來說，伺服端使用**bind**，而client端使用**connect**，如同傳統的TCP一樣。

If you can't figure out which parts are more stable (i.e. peer-to-peer), consider a stable device in the middle, which all sides can connect to.
如果你無法確認那一部份會比較穩定(例如點對點連線)，可以考慮在中間放一個穩定的可讓所有端點連線的裝置。

你可 進一步閱讀[ZeroMQ FAQ](http://zeromq.org/area:faq)中的_"Why do I see different behavior when I bind a socket versus connect a socket?"_部份。

## Multipart messages 多段訊息

ZeroMQ/NetMQ在frame的概念上工作，大多數的訊息都可以想成含有一或多個frame。NetMQ提供一些方便的函式讓你傳送字串訊息，然而你也應該瞭解多段frame的概念及如何應用。

在[Message](message.md)章節有更多說明。

## Patterns

ZeroMQ (and therefore NetMQ) is all about patterns and building blocks. The <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> covers everything you need to know to help you with these patterns. You should make sure you read the following sections before you attempt to start work with NetMQ.

+ <a href="http://zguide.zeromq.org/page:all#Chapter-Sockets-and-Patterns" target="_blank">Chapter 2 - Sockets and Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Advanced-Request-Reply-Patterns" target="_blank">Chapter 3 - Advanced Request-Reply Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Reliable-Request-Reply-Patterns" target="_blank">Chapter 4 - Reliable Request-Reply Patterns</a>
+ <a href="http://zguide.zeromq.org/page:all#Chapter-Advanced-Pub-Sub-Patterns" target="_blank">Chapter 5 - Advanced Pub-Sub Patterns</a>


NetMQ also has some examples of a few of these patterns written using the NetMQ APIs. Should you find the pattern you are looking for in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> it should be fairly easy to translate that into NetMQ usage.

Here are some links to the patterns that are available within the NetMQ codebase:

+ <a href="https://github.com/zeromq/netmq/tree/master/src/Samples/Brokerless%20Reliability%20(Freelance%20Pattern)/Model%20One" target="_blank">Brokerless Reliability Pattern - Freelance Model one</a>
+ <a href="https://github.com/zeromq/netmq/tree/master/src/Samples/Load%20Balancing%20Pattern" target="_blank">Load Balancer Patterns</a>
+ <a href="https://github.com/zeromq/netmq/tree/master/src/Samples/Pirate%20Pattern/Lazy%20Pirate" target="_blank">Lazy Pirate Pattern</a>
+ <a href="https://github.com/zeromq/netmq/tree/master/src/Samples/Pirate%20Pattern/Simple%20Pirate" target="_blank">Simple Pirate Pattern</a>

For other patterns, the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a>
will be your first port of call

ZeroMQ patterns are implemented by pairs of sockets of particular types. In other words, to understand ZeroMQ patterns you need to understand socket types and how they work together. Mostly, this just takes study; there is little that is obvious at this level.

The built-in core ZeroMQ patterns are:

+ [**Request-reply**](request-response.md), which connects a set of clients to a set of services. This is a remote procedure call and task distribution pattern.
+ [**Pub-sub**](pub-sub.md), which connects a set of publishers to a set of subscribers. This is a data distribution pattern.
+ **Pipeline**, which connects nodes in a fan-out/fan-in pattern that can have multiple steps and loops. This is a parallel task distribution and collection pattern.
+ **Exclusive pair**, which connects two sockets exclusively. This is a pattern for connecting two threads in a process, not to be confused with "normal" pairs of sockets.

These are the socket combinations that are valid for a connect-bind pair (either side can bind):

+ `PublisherSocket` and `SubscriberSocket`
+ `RequestSocket` and `ResponseSocket`
+ `RequestSocket`  and `RouterSocket`
+ `DealerSocket` and `ResponseSocket`
+ `DealerSocket` and `RouterSocket`
+ `DealerSocket` and `DealerSocket`
+ `RouterSocket` and `RouterSocket`
+ `PushSocket` and `PullSocket`
+ `PairSocket` and `PairSocket`

Any other combination will produce undocumented and unreliable results, and future versions of ZeroMQ will probably return errors if you try them. You can and will, of course, bridge other socket types via code, i.e., read from one socket type and write to another.


## Options

NetMQ comes with several options that will effect how things work.

Depending on the type of sockets you are using, or the topology you are attempting to create, you may find that you need to set some ZeroMQ options. In NetMQ this is done using the `NetMQSocket.Options` property.

Here is a listing of the available properties that you may set on a `NetMQSocket`. It is hard to say exactly which of these values you may need to set, as that obviously depends entirely on what you are trying to achieve. All I can do is list the options, and make you aware of them. So here they are:

+ `Affinity`
+ `BackLog`
+ `CopyMessages`
+ `DelayAttachOnConnect`
+ `Endian`
+ `GetLastEndpoint`
+ `IPv4Only`
+ `Identity`
+ `Linger`
+ `MaxMsgSize`
+ `MulticastHops`
+ `MulticastRate`
+ `MulticastRecoveryInterval`
+ `ReceiveHighWaterMark`
+ `ReceiveMore`
+ `ReceiveBuffer`
+ `ReconnectInterval`
+ `ReconnectIntervalMax`
+ `SendHighWaterMark`
+ `SendTimeout`
+ `SendBuffer`
+ `TcpAcceptFilter`
+ `TcpKeepAlive`
+ `TcpKeepaliveIdle`
+ `TcpKeepaliveInterval`
+ `XPubVerbose`

We will not be covering all of these here, but shall instead cover them in the areas where they are used. For now just be aware that if you have read something in the <a href="http://zguide.zeromq.org/page:all" target="_blank">ZeroMQ guide</a> that mentions some option, that this is most likely the place you will need to set it/read from it.
