using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class StreamTests
    {
        [Test]
        public void StreamToStream()
        {
            using (var context = new Factory().CreateContext())
            {
                using (var server = context.CreateStreamSocket())
                {
                    server.Bind("tcp://*:5557");

                    using (var client = context.CreateStreamSocket())
                    {
                        client.Connect("tcp://127.0.0.1:5557");

                        byte[] id = client.Options.Identity;

                        client.SendMore(id).Send("GET /\r\n");

                        id = server.Receive();

                        string message = server.ReceiveString();

                        Assert.AreEqual(message, "GET /\r\n");

                        string response = "HTTP/1.0 200 OK\r\n" +
                                          "Content-Type: text/plain\r\n\r\n" + "Hello, World!";

                        server.SendMore(id).Send(response);

                        id = client.Receive();
                        message = client.ReceiveString();

                        Assert.AreEqual(message, response);
                    }
                }
            }
        }
    }
}
