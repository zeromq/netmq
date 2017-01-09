Pub/Sub
=====

From [Wikipedia](http://en.wikipedia.org/wiki/Publish%E2%80%93subscribe_pattern):

> 發布/訂閱（Publish/subscribe 或pub/sub）是一種訊息規範，訊息的傳送者（發布者）不是計劃傳送其訊息給特定的接收者（訂閱者）。而是發布的訊息分為不同的類別，而不需要知道什麼樣的訂閱者訂閱。訂閱者對一個或多個類別表達興趣，於是只接收感興趣的訊息，而不需要知道什麼樣的發布者發布的訊息。這種發布者和訂閱者的解耦可以允許更好的可延伸性和更為動態的網路拓撲.

上述所謂的_類別_也可以當成是一個_"主題"_或_"過濾器"_。

`NetMQ`用兩種socket型別支援`Pub/Sub`模式：

+ `PublisherSocket`
+ `SubscriberSocket`

## Topics

`ZeroMQ/NetMQ`使用多段訊息傳送主題資訊，可用byte陣列來表示主題，或是字串並加上適當的`System.Text.Encoding`。

A publisher must include the topic in the message's' first frame, prior to the message payload. For example, to publish a status message to subscribers of the `status` topic:

    :::csharp
    // send a message on the 'status' topic
    pub.SendMoreFrame("status").SendFrame("All is well");

訂閱者使用`SubscriberSocket`的`Subscribe`函式指定他們有興趣的主題。

    :::csharp
    // subscribe to the 'status' topic
    sub.Subscribe("status");


## Topic heirarchies

一個訊息的主題會用prefix檢查和訂閱者的訂閱主題比較。

也就是說，訂閱`主題`的訂閱者會接收具有主題的訊息：

* `topic`
* `topic/subtopic`
* `topical`

然而它不會接受這些主題：

* `topi`
* `TOPIC` （記住，它是以byte做為比較方式）

使用prefix比對行為的結果，可以讓你以空字串來訂閱所有發佈的訊息。

    :::csharp
    sub.Subscribe(""); // subscribe to all topics


## An Example

到了介紹範例的時間了，這範例很簡單，並遵守下列規則：

+ 有一個發佈者的process，會以500ms的時間隨機發佈主題為`TopicA`或'TopicB`的訊息。
+ 可能會有很多訂閱者，欲訂閱的主題名稱會以命令列參數代入程式中。

### Publisher

    :::csharp
    using System;
    using System.Threading;
    using NetMQ;
    using NetMQ.Sockets;

    namespace Publisher
    {
        class Program
        {
            static void Main(string[] args)
            {
                Random rand = new Random(50);

                using (var pubSocket = new PublisherSocket())
                {
                    Console.WriteLine("Publisher socket binding...");
                    pubSocket.Options.SendHighWatermark = 1000;
                    pubSocket.Bind("tcp://localhost:12345");

                    for (var i = 0; i < 100; i++)
                    {
                        var randomizedTopic = rand.NextDouble();
                        if (randomizedTopic > 0.5)
                        {
                            var msg = "TopicA msg-" + i;
                            Console.WriteLine("Sending message : {0}", msg);
                            pubSocket.SendMoreFrame("TopicA").SendFrame(msg);
                        }
                        else
                        {
                            var msg = "TopicB msg-" + i;
                            Console.WriteLine("Sending message : {0}", msg);
                            pubSocket.SendMoreFrame("TopicB").SendFrame(msg);
                        }

                        Thread.Sleep(500);
                    }
                }
            }
        }
    }


### Subscriber

    :::csharp
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NetMQ;
    using NetMQ.Sockets;

    namespace SubscriberA
    {
        class Program
        {
            public static IList<string> allowableCommandLineArgs
                = new [] { "TopicA", "TopicB", "All" };

            static void Main(string[] args)
            {
                if (args.Length != 1 || !allowableCommandLineArgs.Contains(args[0]))
                {
                    Console.WriteLine("Expected one argument, either " +
                                      "'TopicA', 'TopicB' or 'All'");
                    Environment.Exit(-1);
                }

                string topic = args[0] == "All" ? "" : args[0];
                Console.WriteLine("Subscriber started for Topic : {0}", topic);

                using (var subSocket = new SubscriberSocket())
                {
                    subSocket.Options.ReceiveHighWatermark = 1000;
                    subSocket.Connect("tcp://localhost:12345");
                    subSocket.Subscribe(topic);
                    Console.WriteLine("Subscriber socket connecting...");
                    while (true)
                    {
                        string messageTopicReceived = subSocket.ReceiveFrameString();
                        string messageReceived = subSocket.ReceiveFrameString();
                        Console.WriteLine(messageReceived);
                    }
                }
            }
        }
    }

在這邊提供三個批次檔，讓你方便執行，不過要稍微修改一下路徑等一適合你的環境。

### RunPubSub.bat

    :::text
    start RunPublisher.bat
    start RunSubscriber "TopicA"
    start RunSubscriber "TopicB"
    start RunSubscriber "All"

### RunPublisher.bat

    :::text
    cd Publisher\bin\Debug
    Publisher.exe

### RunSubscriber.bat

    ::text
    set "topic=%~1"
    cd Subscriber\bin\Debug
    Subscriber.exe %topic%


執行時輸出如下：

![](Images/PubSubUsingTopics.png)


Other Considerations
=====

### High water mark

SendHighWaterMark / ReceiveHighWaterMark選項可設定指定socket的high water mark。High water mark是對未完成訊息的最大數量的限制，NetMQ會將正在與指定的socket通訊的任何端點的訊息排入佇列中。

如果到達此限制，socket會進入異常狀態，並且根據socket類型，NetMQ應採取適當的措施，如阻止或丟棄發送的訊息。

預設的SendHighWaterMark / ReceiveHighWaterMark值為1000.零值表示“無限制”。

你也可以使用`xxxSocket.Options`屬性值設定下列兩個屬性：

+  `pubSocket.Options.SendHighWatermark = 1000;`
+  `pubSocket.Options.ReceiveHighWatermark = 1000;`


### Slow subscribers

This is covered in the <a href="http://zguide.zeromq.org/php:chapter5" target="_blank">ZeroMQ guide</a>


### Late joining subscribers

This is covered in the <a href="http://zguide.zeromq.org/php:chapter5" target="_blank">ZeroMQ guide</a>
