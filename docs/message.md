What's a message
=====

So if you have come here after looking at some of the introductory material, you may have come across an example or 2, maybe even a hello world example which may have gone something like this:

    using (NetMQContext ctx = NetMQContext.Create())
    {
        using (var server = ctx.CreateResponseSocket())
        {
            server.Bind("tcp://127.0.0.1:5556");
            using (var client = ctx.CreateRequestSocket())
            {
                client.Connect("tcp://127.0.0.1:5556");

                client.Send("Hello");

                string fromClientMessage = server.ReceiveString();

                Console.WriteLine("From Client: {0}", fromClientMessage);

                server.Send("Hi Back");

                string fromServerMessage = client.ReceiveString();

                Console.WriteLine("From Server: {0}", fromServerMessage);

                Console.ReadLine();
            }
        }
    }


Where you may have noticed (or perhaps not) that the NetMQ socket(s) have a RecieveString() method. This is good, and extremely useful, but you may be fooled into thinking this is what you should be using all the time.

Truth is ZeroMQ, and therefore NetMQ are really frame based, which implies some form of protocol. Some of you may balk at this prospect, and may curse, and think damm it, I am not a protocol designer I was not expecting to get my hands that dirty.

While it is true that if you wish to come up with some complex and elaborate architecture you would be best of coming up with a nice protocol, thankfully you will not need to do this all the time. This is largely down to ZeroMQ/NetMQ clever socket(s) that abstract away a lot of that from you, and the way in which you can treat the socket(s) as building blocks to build complex architecture (think lego). 

One precanned example of this is the [RouterSocket](https://github.com/zeromq/netmq/blob/master/docs/router.md) which makes very clever use of frames for you out of the box. Where it effectively onion skins the current message with the senders return address, so that when it gets a message back (say from a worker socket), it can use that frame information again to obtain the correct return address and send it back to the correct socket.

So that is one inbuilt use of frames that you should be aware of, but frames are not limited to [RouterSocket](https://github.com/zeromq/netmq/blob/master/docs/router.md), you can use them yourself for all sorts of things, here are some examples:

+ You may decide to dedicate frame[0] to be a specific message type, that can be examined by sockets to see if they should examine it further (this may be useful in a pub/sub type of arrangement, where frame[0] is the topic. By doing this, the subscribers may save themselves a lot of work of dersializing the rest of the message that they may not care about anyway
+ You may decide to use frame[0] as some sort of command, frame[1] and some sort of parameter and have frame[2] as the message payload (where it may contain some serialized object, say a JSON seriailized object)

These are just some examples, you can use frames how you wish really

When you work with multipart messages (frames) you must send/receive all the parts of the message you want to work with.

There is also an inbuilt concept of "more" which you can integrate for. We will see some examples of this in just a moment 



How Do I Create Frames
=====

Creating multi part messages is fairly simple, we just need to use the NetMQMessage class, and then make use of one of the many Append() method overloads (there are overloads for appending Blob/NetMQFrame/Byte[]/int/long/string).

Here is a simple example where we create a new NetMQMessage which expects to contain 2 NetMQFrame(s), and we use the NetMQMessage.Append() method to append 2 string values.

    var message = new NetMQMessage();
    message.Append("IAmFrame0");
    message.Append("IAmFrame1");
    server.SendMessage(message);


There is also another way of doing this, which is to use the NetMQ IOutgoingSocket.SendMore() (an internal interface) method. This doesn't have as many overloads as SendMessage but it is still ok, it allows you to send Byte[] and string data quite easily. 

Here is an example of usage, where we are sending 2 string values using the IOutgoingSocket.SendMore() method


    var client = ctx.CreateRequestSocket()
    client.Connect("tcp://127.0.0.1:5556");

    //client send message
    client.SendMore("A");
    client.Send("Hello");

The problem with using IOutgoingSocket.SendMore() rather than IOutgoingSocket.Send(), if you intend to send more than 1 
frame, is that you MUST ensure you call SendMore, where as if you use the SendMessage() method, you do not have to worry about that, that is all taken care of for you.



How Do I Read Frames
=====

Reading multiple frames can also be done in 2 ways. You may use the NetMQ conveience RecieveString(out more) method mutiple times, where you would need to know if there was more than 1 message part to read, which you would need to track in a bool variable. This is shown below

    //client send message
    client.SendMore("A");
    client.Send("Hello");

    //server receive 1st part
    bool more;
    string messagePart1 = server.ReceiveString(out more);
    Console.WriteLine("string messagePart1 = server.ReceiveString(out more)");
    Console.WriteLine("messagePart1={0}", messagePart1);
    Console.WriteLine("HasMore={0}", more);


    //server receive 2nd part
    if (more)
    {
        string messagePart2 = server.ReceiveString(out more);
        Console.WriteLine("string messagePart2 = server.ReceiveString(out more)");
        Console.WriteLine("messagePart1={0}", messagePart2);
        Console.WriteLine("HasMore={0}", more);
        Console.WriteLine("================================");
    }
    
An easier way is to use the RecieveMessage() method, and then read the frames as you want to. Here is an example of that

    var message4 = client.ReceiveMessage();
    Console.WriteLine("message4={0}", message4);
    Console.WriteLine("message4.FrameCount={0}", message4.FrameCount);

    Console.WriteLine("message4[0]={0}", message4[0].ConvertToString());
    Console.WriteLine("message4[1]={0}", message4[1].ConvertToString());



A Full Example
=====

Just to solidify this information here is a complete example showing everything we have discussed above:

    using System;
    using NetMQ;

    namespace ConsoleApplication2
    {
        internal class Program
        {
            private static void Main(string[] args)
            {
                using (NetMQContext ctx = NetMQContext.Create())
                {
                    using (var server = ctx.CreateResponseSocket())
                    {
                        server.Bind("tcp://127.0.0.1:5556");

                        using (var client = ctx.CreateRequestSocket())
                        {

                            client.Connect("tcp://127.0.0.1:5556");

                            //client send message
                            client.SendMore("A");
                            client.Send("Hello");

                            //server receive 1st part
                            bool more;
                            string messagePart1 = server.ReceiveString(out more);
                            Console.WriteLine("string messagePart1 = server.ReceiveString(out more)");
                            Console.WriteLine("messagePart1={0}", messagePart1);
                            Console.WriteLine("HasMore={0}", more);


                            //server receive 2nd part
                            if (more)
                            {
                                string messagePart2 = server.ReceiveString(out more);
                                Console.WriteLine("string messagePart2 = server.ReceiveString(out more)");
                                Console.WriteLine("messagePart1={0}", messagePart2);
                                Console.WriteLine("HasMore={0}", more);
                                Console.WriteLine("================================");
                            }


                            //server send message, this time use NetMqMessage
                            //which will be sent as frames if the client calls
                            //ReceieveMessage()
                            var m3 = new NetMQMessage();
                            m3.Append("From");
                            m3.Append("Server");
                            server.SendMessage(m3);
                            Console.WriteLine("Sending 2 frame message");




                            //client receive
                            var message4 = client.ReceiveMessage();
                            Console.WriteLine("message4={0}", message4);
                            Console.WriteLine("message4.FrameCount={0}", message4.FrameCount);

                            Console.WriteLine("message4[0]={0}", message4[0].ConvertToString());
                            Console.WriteLine("message4[1]={0}", message4[1].ConvertToString());


                            Console.ReadLine();
                        }
                    }
                }
            }
        }
    }


Which when run will give you some output like this:

<p>
string messagePart1 = server.ReceiveString(out more)<br/>
messagePart1=A<br/>
HasMore=True<br/>
string messagePart2 = server.ReceiveString(out more)<br/>
messagePart1=Hello<br/>
HasMore=False<br/>
================================<br/>
Sending 2 frame message<br/>
message4=NetMQMessage[From,Server]<br/>
message4.FrameCount=2<br/>
message4[0]=From<br/>
message4[1]=Server<br/>
<i>

</i>
</p>

