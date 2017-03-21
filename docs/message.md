Message Structure 訊息結構
===

So if you have come here after looking at some of the introductory material, you may have come across an example or two, maybe even a "hello world" example resembling:

    :::csharp
    using (var server = new ResponseSocket("@tcp://127.0.0.1:5556"))
    using (var client = new RequestSocket(">tcp://127.0.0.1:5556"))
    {
        client.Send("Hello");

        string fromClientMessage = server.ReceiveFrameString();

        Console.WriteLine("From Client: {0}", fromClientMessage);

        server.SendFrame("Hi Back");

        string fromServerMessage = client.ReceiveFrameString();

        Console.WriteLine("From Server: {0}", fromServerMessage);

        Console.ReadLine();
    }

也許你有注意到(或沒有)NetMQ的socket有一個`ReceiveFrameString()`函式，這是一個很好且有用的函式，但如果你認為只能用它那就不對了。

事實是ZeroMQ/NetMQ是基於frame的，意味著它們實現某種型式的協定。 Some of you may balk at this prospect, and may curse, and think damm it, I am not a protocol designer I was not expecting to get my hands that dirty.

While it is true that if you wish to come up with some complex and elaborate architecture you would be best of coming up with a nice protocol, thankfully you will not need to do this all the time. This is largely down to ZeroMQ/NetMQ's clever sockets that abstract away a lot of that from you, and the way in which you can treat the sockets as building blocks to build complex architecture (think lego).

一個例子是[RouterSocket](router-dealer.md)，它與眾不同且聰明地使用frame，它在傳送者訊息上加了一層代表回傳位址的資訊，所以當它接收到一個回傳訊息(從另一個工作的socket)，它可以使用收到的frame訊息來獲得來源位址，並依此位址回傳訊息至正確的socket。

所以你應該注意的一個內建的frame的使用的例子，但frame並不限制在[RouterSocket](router-dealer.md)類型，你可以在所有的地方使用，如下列範例：

+ 你也許想讓`frame[0]`表示接下來的frame的型態，這讓接收者可以去掉不感興趣的訊息，且不需要花費時間反序列化訊息，ZeroMQ/NetMQ在[Pub-Sub sockets](pub-sub.md)中使用這個想法，你可以替換或是擴充它。
+ 你也許想讓`frame[0]`代表某種命令，`frame[1]`代表參數，`frame[2]`代表實際訊息內容(也許包含序列化的物件)。

這只是一些範例，實際上你可以用任何你想的方式來操作frame，雖然一些socket型別會期待或產生特定的frame結構。

當你使用多段訊息(frames)時你需要一次傳送/接收所有區段的訊息。

有一個內建的"more"的概念可以讓你整合使用，稍後會有更多例子。

## Creating multipart messages 建立多段訊息

建立多段訊息很簡單，有兩個方式可以達成。

### Building a message object

你可以建立`NetMQMessage`物件並透過`Append(...)`覆載函式來加上frame資料，也有其它覆載可讓你加上`Blob`, `NetMQFrae`, `byte[]`, `int`, `long`及`string`等。

下列是一個加上兩個frame的訊息的範例，每個frame都包含一個字串值：

    :::csharp
    var message = new NetMQMessage();
    message.Append("IAmFrame0");
    message.Append("IAmFrame1");
    server.SendMessage(message);

### Sending frame by frame

另一個傳送多段訊息的方法是使用`SendMoreFrame`擴充函式，這不像`SendMessage`一樣有很多覆載，但是它讓可以讓你很簡單地傳送`byte[]`,`string`資料。這是一個和前述範例相像的範例：

    :::csharp
    server.SendMoreFrame("IAmFrame0")
          .SendFrame("IAmFrame1");

要傳送超過兩個frame，可將多個`SendMoreFrame`呼叫鏈結在一起，只要確定最後一個是`SendFrame`！

## Reading multipart messages 讀取多段訊息

讀取多段訊息也有兩個方法。

### Receiving individual frames

你可以從socket中一次讀出一個frame。Out參數`more`會告訴你目前是不是最後一個訊息。

你也可以使用方便的NetMQ函式`ReceiveFrameString(out more)`多次，只需要知道是不是還有frame待讀取，所以要追蹤`more`變數的狀態，如下範例：

    :::csharp
    // server sends a message with two frames
    server.SendMoreFrame("A")
          .SendFrame("Hello");

    // client receives all frames in the message, one by one
    bool more = true;
    while (more)
    {
        string frame = client.ReceiveFrameString(out more);
        Console.WriteLine("frame={0}", frame);
        Console.WriteLine("more={0}", more);
    }

這個迴圈將執行兩次。第一次，`more`將被設為**true**。第二次，**false**。輸出將是：

    :::text
    frame=A
    more=true
    frame=Hello
    more=false

### Reading entire messages 讀取整個訊息

一個更簡單的方法是使用`ReceiveMultipartMessage()`函式，它提供一個包含消息的所有frame的物件。

    :::csharp
    NetMQMessage message = client.ReceiveMultipartMessage();
    Console.WriteLine("message.FrameCount={0}", message.FrameCount);
    Console.WriteLine("message[0]={0}", message[0].ConvertToString());
    Console.WriteLine("message[1]={0}", message[1].ConvertToString());

輸出會是：

    :::text
    message.FrameCount=2
    message[0]=A
    message[1]=Hello

也有其它功能，如：

    :::csharp
    IEnumerable<string> framesAsStrings = client.ReceiveMultipartStrings();

    IEnumerable<byte[]> framesAsByteArrays = client.ReceiveMultipartBytes();


## A Full Example

這裡有一個完整的範例，以加深至目前為止我們談論的印象：

    :::csharp
    using (var server = new ResponseSocket("@tcp://127.0.0.1:5556"))
    using (var client = new RequestSocket(">tcp://127.0.0.1:5556"))
    {
        // client sends message consisting of two frames
        Console.WriteLine("Client sending");
        client.SendMoreFrame("A").SendFrame("Hello");

        // server receives frames
        bool more = true;
        while (more)
        {
            string frame = server.ReceiveFrameString(out more);
            Console.WriteLine("Server received frame={0} more={1}",
                frame, more);
        }

        Console.WriteLine("================================");

        // server sends message, this time using NetMqMessage
        var msg = new NetMQMessage();
        msg.Append("From");
        msg.Append("Server");

        Console.WriteLine("Server sending");
        server.SendMultipartMessage(msg);

        // client receives the message
        msg = client.ReceiveMultipartMessage();
        Console.WriteLine("Client received {0} frames", msg.FrameCount);

        foreach (var frame in msg)
            Console.WriteLine("Frame={0}", frame.ConvertToString());

        Console.ReadLine();
    }

執行後的輸出如下：

    :::text
    Client sending
    Server received frame=A more=true
    Server received frame=Hello more=false
    ================================
    Server sending
    Client received 2 frames
    Frame=From
    Frame=Server
