using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ;

namespace NetMQ.Serialization
{
  public static class SocketExtensions
  {
    public static void SendProtocolMessage<TMessageType>(this NetMQSocket socket, 
      BaseProtocol<TMessageType> protocol, object message, bool dontWait = false)
    {
      TMessageType messageType = protocol.GetMessageType(message);

      var messsageTypeBytes = protocol.ConvertMessageTypeToBytes(messageType);

      socket.SendMore(messsageTypeBytes);

      var serializer = protocol.GetSerializer(messageType);

      socket.SendMultiple(serializer(message), dontWait);
    }

    public static object ReceiveProtocolMessage<TMessageType>(this NetMQSocket socket,
                                                              BaseProtocol<TMessageType> protocol, bool dontWait = false)
    {
      bool more;

      byte[] messageTypeBytes = socket.Receive(dontWait, out more);

      TMessageType messageType = protocol.ConvertBytesToMessageType(messageTypeBytes);

      var deserializer = protocol.GetDeserializer(messageType);

      if (more)
      {
        return deserializer(socket.ReceiveMessages().ToArray());
      }
      else
      {
        return deserializer(new byte[0][]);
      }
    }
  }
}
