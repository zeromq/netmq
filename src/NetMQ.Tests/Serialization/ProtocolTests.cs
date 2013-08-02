using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NetMQ.Serialization;

namespace NetMQ.Tests.Serialization
{    
  [TestFixture]
  public class ProtocolTests
  {
    public class RequestMessage
    {
      [NetMQMember(0)]
      public string Text { get; set; }      
    }

    public class ResponseMessage
    {
      [NetMQMember(0)]
      public string Text { get; set; }
    }

    [Test]
    public void IntegerBaseProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            IntegerBasedProtocol protocol = new IntegerBasedProtocol();
            protocol.RegisterMessage<RequestMessage>(0);
            protocol.RegisterMessage<ResponseMessage>(1);

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response"});

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }      
    }

    [Test]
    public void ByteBasedProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            ByteBasedProtocol protocol = new ByteBasedProtocol();
            protocol.RegisterMessage<RequestMessage>(0);
            protocol.RegisterMessage<ResponseMessage>(1);

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response" });

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }
    }

    [Test]
    public void ShortBasedProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            ShortBasedProtocol protocol = new ShortBasedProtocol();
            protocol.RegisterMessage<RequestMessage>(0);
            protocol.RegisterMessage<ResponseMessage>(1);

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response" });

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }
    }

    [Test]
    public void StringBasedProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            StringBasedProtocol protocol = new StringBasedProtocol();
            protocol.RegisterMessage<RequestMessage>("0");
            protocol.RegisterMessage<ResponseMessage>("1");

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response" });

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }
    }

    public enum MessageType
    {
      Request =0, Reply = 1
    }

    [Test]
    public void EnumInt32BasedProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            EnumBasedProtocol<MessageType> protocol = new EnumBasedProtocol<MessageType>(EnumSerializationType.Int32);
            protocol.RegisterMessage<RequestMessage>(MessageType.Request);
            protocol.RegisterMessage<ResponseMessage>(MessageType.Reply);

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response" });

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }
    }

    [Test]
    public void EnumStringBasedProtocol()
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (NetMQSocket responseSocket = context.CreateResponseSocket())
        {
          responseSocket.Bind("tcp://127.0.0.1:5556");

          using (NetMQSocket requestSocket = context.CreateRequestSocket())
          {
            requestSocket.Connect("tcp://127.0.0.1:5556");

            EnumBasedProtocol<MessageType> protocol = new EnumBasedProtocol<MessageType>(EnumSerializationType.MemberName);
            protocol.RegisterMessage<RequestMessage>(MessageType.Request);
            protocol.RegisterMessage<ResponseMessage>(MessageType.Reply);

            requestSocket.SendProtocolMessage(protocol, new RequestMessage { Text = "request" });

            var requestMessage = responseSocket.ReceiveProtocolMessage(protocol) as RequestMessage;

            Assert.IsNotNull(requestMessage);
            Assert.AreEqual(requestMessage.Text, "request");

            responseSocket.SendProtocolMessage(protocol, new ResponseMessage { Text = "response" });

            var responseMessage = requestSocket.ReceiveProtocolMessage(protocol) as ResponseMessage;
            Assert.IsNotNull(responseMessage);
            Assert.AreEqual(responseMessage.Text, "response");
          }
        }
      }
    }

  }
}
