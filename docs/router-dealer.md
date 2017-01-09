Router Dealer
=====


## RouterSocket

From the [ZeroMQ guide](http://zguide.zeromq.org/page:all):

> The ROUTER socket, unlike other sockets, tracks every connection it has, and tells the caller about these. The way it tells the caller is to stick the connection identity in front of each message received. An identity, sometimes called an address, is just a binary string with no meaning except "this is a unique handle to the connection". Then, when you send a message via a ROUTER socket, you first send an identity frame.
>
> When receiving messages a ZMQ_ROUTER socket shall prepend a message part containing the identity of the originating peer to the message before passing it to the application. Messages received are fair-queued from among all connected peers. When sending messages a ZMQ_ROUTER socket shall remove the first part of the message and use it to determine the identity of the peer the message shall be routed to.
>
> Identities are a difficult concept to understand, but it's essential if you want to become a ZeroMQ expert. The ROUTER socket invents a random identity for each connection with which it works. If there are three REQ sockets connected to a ROUTER socket, it will invent three random identities, one for each REQ socket.

所以我們來看一個較小的範例，我們有一個`DealerSocket`，帶有一個3 byte的示別碼"ABC"，在內部，這表示`RouterSocket`型別的socket內保有一個hash table，它可以搜尋"ABC"，並找到這一個`DealerSocket`的TCP連線。

當我們收到來自`DealerSocket`的訊息時，我們取得三個frames：

![](https://github.com/imatix/zguide/raw/master/images/fig28.png)


### Identities and Addresses

From [ZeroMQ guide, Identities and Addresses](http://zguide.zeromq.org/page:all#Identities-and-Addresses):

> The identity concept in ZeroMQ refers specifically to ROUTER sockets and how they identify the connections they have to other sockets. More broadly, identities are used as addresses in the reply envelope. In most cases, the identity is arbitrary and local to the ROUTER socket: it's a lookup key in a hash table. Independently, a peer can have an address that is physical (a network endpoint like "tcp://192.168.55.117:5670") or logical (a UUID or email address or other unique key).
>
> An application that uses a ROUTER socket to talk to specific peers can convert a logical address to an identity if it has built the necessary hash table. Because ROUTER sockets only announce the identity of a connection (to a specific peer) when that peer sends a message, you can only really reply to a message, not spontaneously talk to a peer.
>
> This is true even if you flip the rules and make the ROUTER connect to the peer rather than wait for the peer to connect to the ROUTER. However you can force the ROUTER socket to use a logical address in place of its identity. The zmq_setsockopt reference page calls this setting the socket identity. It works as follows:
>
> + The peer application sets the ZMQ_IDENTITY option of its peer socket (DEALER or REQ) before binding or connecting.
> + Usually the peer then connects to the already-bound ROUTER socket. But the ROUTER can also connect to the peer.
> + At connection time, the peer socket tells the router socket, "please use this identity for this connection".
> + If the peer socket doesn't say that, the router generates its usual arbitrary random identity for the connection.
> + The ROUTER socket now provides this logical address to the application as a prefix identity frame for any messages coming in from that peer.
> + The ROUTER also expects the logical address as the prefix identity frame for any outgoing messages.


## DealerSocket

NetMQ的`DealerSocket`不做任何特別的事情，它提供的是以完全非同步方式工作的能力。

Which if you recall was not something that other socket types could do, where the `ReceieveXXX` / `SendXXX` methods are blocking, and would also throw exceptions should you try to call
things in the wrong order, or more than expected.


DealerSocket的主要賣點是它的非同步能力。通常，`DealerSocket`會與`RouterSocket`結合使用，這就是為什麼我們決定將這兩種socket型別的介紹放在一起。

If you want to know more details about socket combinations involving `DealerSocket`s, then as ALWAYS the guide is your friend. In particular the <a href="http://zguide.zeromq.org/page:all#toc58" target="_blank">Request-Reply Combinations</a> page of the guide may be of interest.


## An example

Time for an example. The best way to think of this example is summarized in the bullet points below:

* 有一個伺服器，它綁定了一個`RouterSocket`，因此會儲存傳入的請求連線的示別資訊，所以可以正確的將訊息回應至client socket。
* 有很多個client，每個client都屬於個別執行緒，這些client的型別是`DealerSocket`，這一個client socket會提供固定的示別碼，以讓伺服端(`DealerSocket`)可以正確的回應訊息。

程式碼如下：

    :::csharp
    public static void Main(string[] args)
    {
        // NOTES
        // 1. Use ThreadLocal<DealerSocket> where each thread has
        //    its own client DealerSocket to talk to server
        // 2. Each thread can send using it own socket
        // 3. Each thread socket is added to poller

        const int delay = 3000; // millis

        var clientSocketPerThread = new ThreadLocal<DealerSocket>();

        using (var server = new RouterSocket("@tcp://127.0.0.1:5556"))
        using (var poller = new NetMQPoller())
        {
            // Start some threads, each with its own DealerSocket
            // to talk to the server socket. Creates lots of sockets,
            // but no nasty race conditions no shared state, each
            // thread has its own socket, happy days.
            for (int i = 0; i < 3; i++)
            {
                Task.Factory.StartNew(state =>
                {
                    DealerSocket client = null;

                    if (!clientSocketPerThread.IsValueCreated)
                    {
                        client = new DealerSocket();
                        client.Options.Identity =
                            Encoding.Unicode.GetBytes(state.ToString());
                        client.Connect("tcp://127.0.0.1:5556");
                        client.ReceiveReady += Client_ReceiveReady;
                        clientSocketPerThread.Value = client;
                        poller.Add(client);
                    }
                    else
                    {
                        client = clientSocketPerThread.Value;
                    }

                    while (true)
                    {
                        var messageToServer = new NetMQMessage();
                        messageToServer.AppendEmptyFrame();
                        messageToServer.Append(state.ToString());
                        Console.WriteLine("======================================");
                        Console.WriteLine(" OUTGOING MESSAGE TO SERVER ");
                        Console.WriteLine("======================================");
                        PrintFrames("Client Sending", messageToServer);
                        client.SendMultipartMessage(messageToServer);
                        Thread.Sleep(delay);
                    }

                }, string.Format("client {0}", i), TaskCreationOptions.LongRunning);
            }

            // start the poller
            poller.RunAsync();

            // server loop
            while (true)
            {
                var clientMessage = server.ReceiveMessage();
                Console.WriteLine("======================================");
                Console.WriteLine(" INCOMING CLIENT MESSAGE FROM CLIENT ");
                Console.WriteLine("======================================");
                PrintFrames("Server receiving", clientMessage);
                if (clientMessage.FrameCount == 3)
                {
                    var clientAddress = clientMessage[0];
                    var clientOriginalMessage = clientMessage[2].ConvertToString();
                    string response = string.Format("{0} back from server {1}",
                        clientOriginalMessage, DateTime.Now.ToLongTimeString());
                    var messageToClient = new NetMQMessage();
                    messageToClient.Append(clientAddress);
                    messageToClient.AppendEmptyFrame();
                    messageToClient.Append(response);
                    server.SendMultipartMessage(messageToClient);
                }
            }
        }
    }

    void PrintFrames(string operationType, NetMQMessage message)
    {
        for (int i = 0; i < message.FrameCount; i++)
        {
            Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, i,
                message[i].ConvertToString());
        }
    }

    void Client_ReceiveReady(object sender, NetMQSocketEventArgs e)
    {
        bool hasmore = false;
        e.Socket.Receive(out hasmore);
        if (hasmore)
        {
            string result = e.Socket.ReceiveFrameString(out hasmore);
            Console.WriteLine("REPLY {0}", result);
        }
    }

執行後，輸出應如下所示：


    :::text
    ======================================
     OUTGOING MESSAGE TO SERVER
    ======================================
    ======================================
     OUTGOING MESSAGE TO SERVER
    ======================================
    Client Sending Socket : Frame[0] =
    Client Sending Socket : Frame[1] = client 1
    Client Sending Socket : Frame[0] =
    Client Sending Socket : Frame[1] = client 0
    ======================================
     INCOMING CLIENT MESSAGE FROM CLIENT
    ======================================
    Server receiving Socket : Frame[0] = c l i e n t   1
    Server receiving Socket : Frame[1] =
    Server receiving Socket : Frame[2] = client 1
    ======================================
     INCOMING CLIENT MESSAGE FROM CLIENT
    ======================================
    Server receiving Socket : Frame[0] = c l i e n t   0
    Server receiving Socket : Frame[1] =
    Server receiving Socket : Frame[2] = client 0
    REPLY client 1 back from server 08:05:56
    REPLY client 0 back from server 08:05:56

記住這是非同步的程式碼，所以事件的發生順序可能不如你所預期的。
