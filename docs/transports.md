Transport Protocols
===

NetMQ支援三種主要的協定：

+ TCP (`tcp://`)
+ InProc (`inproc://`)
+ PGM (`pgm://`) &mdash; requires MSMQ and running as administrator

下面會一一介紹。


## TCP

TCP是最常用到的協定，因此，大部份的程式碼會使用TCP展示。

### Example

又一個簡單的範例：

    :::csharp
    using (var server = new ResponseSocket())
    using (var client = new RequestSocket())
    {
        server.Bind("tcp://*:5555");
        client.Connect("tcp://localhost:5555");

        Console.WriteLine("Sending Hello");
        client.SendFrame("Hello");

        var message = server.ReceiveFrameString();
        Console.WriteLine("Received {0}", message);

        Console.WriteLine("Sending World");
        server.SendFrame("World");

        message = client.ReceiveFrameString();
        Console.WriteLine("Received {0}", message);
    }

輸出：

    :::text
    Sending Hello
    Received Hello
    Sending World
    Received World

### Address format

注意位址格式字串會傳送給`Bind()`及`Connect()`函式。
在TCP連線中，它會被組成：

    :::text
    tcp://*:5555

這由三個部份構成：

 1. 協議（tcp）
 2. 主機（IP地址，主機名或匹配`"*"`的wildcard）
 3. 埠號（5555）


## InProc

InProc (in-process)讓你可以在同一個process中用sockets連線溝通，這很有用，有幾個理由：

+ 取消共享狀態/鎖。當你傳送資料至socket時不需要擔心共享狀態。Socket的每一端都有自己的副本。
+ 能夠在系統的不同的部分之間進行通信。

NetMQ提供了幾個使用InProc的組件，例如[Actor模型](actor.md)和Devices，在相關文件中會再討論。

### Example

現在讓我們通過在兩個執行緒之間傳送一個字串（為了簡單起見）展示一個簡單的InProc。

    :::csharp
    using (var end1 = new PairSocket())
    using (var end2 = new PairSocket())
    {
        end1.Bind("inproc://inproc-demo");
        end2.Connect("inproc://inproc-demo");

        var end1Task = Task.Run(() =>
        {
            Console.WriteLine("ThreadId = {0}", Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("Sending hello down the inproc pipeline");
            end1.SendFrame("Hello");
        });
        var end2Task = Task.Run(() =>
        {
            Console.WriteLine("ThreadId = {0}", Thread.CurrentThread.ManagedThreadId);
            var message = end2.ReceiveFrameString();
            Console.WriteLine(message);
        });
        Task.WaitAll(new[] { end1Task, end2Task });
    }

輸出：

    :::text
    ThreadId = 12
    ThreadId = 6
    Sending hello down the inproc pipeline
    Hello

### Address format

注意位址格式字串會傳送給`Bind()`及`Connect()`函式。
在InProc連線中，它會被組成：

    :::text
    inproc://inproc-demo

這由兩個部份構成：

1. 協定(inproc)
2. 辨識名稱（inproc-demo可以是任何字串，在process範圍內是唯一的名稱）


## PGM

Pragmatic General Multicast (PGM) is a reliable multicast transport protocol for applications that require ordered
or unordered, duplicate-free, multicast data delivery from multiple sources to multiple receivers.

PGM guarantees that a receiver in the group either receives all data packets from transmissions and repairs, or
is able to detect unrecoverable data packet loss. PGM is specifically intended as a workable solution for multicast
applications with basic reliability requirements. Its central design goal is simplicity of operation with due
regard for scalability and network efficiency.

To use PGM with NetMQ, we do not have to do too much. We just need to follow these three pointers:

1. The socket types are now `PublisherSocket` and `SubscriberSocket`
   which are talked about in more detail in the [pub-sub pattern](pub-sub.md) documentation.
2. Make sure you are running the app as "Administrator"
3. Make sure you have turned on the "Multicasting Support". You can do that as follows:

![](Images/PgmSettingsInWindows.png)

### Example

Here is a small demo that use PGM, as well as `PublisherSocket` and `SubscriberSocket` and a few option values.

    :::csharp
    const int MegaBit = 1024;
    const int MegaByte = 1024;
    using (var pub = new PublisherSocket())
    using (var sub1 = new SubscriberSocket())
    using (var sub2 = new SubscriberSocket())
    {
        pub.Options.MulticastHops = 2;
        pub.Options.MulticastRate = 40 * MegaBit; // 40 megabit
        pub.Options.MulticastRecoveryInterval = TimeSpan.FromMinutes(10);
        pub.Options.SendBuffer = MegaByte * 10; // 10 megabyte
        pub.Connect("pgm://224.0.0.1:5555");

        sub1.Options.ReceiveBuffer = MegaByte * 10;
        sub1.Bind("pgm://224.0.0.1:5555");
        sub1.Subscribe("");

        sub2.Bind("pgm://224.0.0.1:5555");
        sub2.Options.ReceiveBuffer = MegaByte * 10;
        sub2.Subscribe("");

        Console.WriteLine("Server sending 'Hi'");
        pub.Send("Hi");

        bool more;
        Console.WriteLine("sub1 received = '{0}'", sub1.ReceiveString(out more));
        Console.WriteLine("sub2 received = '{0}'", sub2.ReceiveString(out more));
    }

Which when run gives the following sort of output:

    :::text
    Server sending 'Hi'
    sub1 received = 'Hi'
    sub2 received = 'Hi'


### Address format

Notice the format of the address string passed to `Bind()` and `Connect()`. For InProc connections, it will resemble:

    :::text
    pgm://224.0.0.1:5555

This is made up of three parts:

1. The protocol (`pgm`)
2. The host (an IP address such as `244.0.0.1`, host name, or the wildcard `*` to match any)
3. The port number (`5555`)

Another good source for PGM information is NetMQ's [PGM unit tests](https://github.com/zeromq/netmq/blob/master/src/NetMQ.Tests/PgmTests.cs).
