using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Serialization
{
  public abstract class BaseProtocol<TMessageType>
  {
    private Dictionary<TMessageType, Func<object, byte[][]>> m_serializers= new Dictionary<TMessageType, Func<object, byte[][]>>();
 
    private Dictionary<TMessageType, Func<byte[][], object>> m_deserializers = new Dictionary<TMessageType, Func<byte[][], object>>(); 

    private Dictionary<Type, TMessageType> m_typeToMessageType = new Dictionary<Type, TMessageType>();

    public void RegisterMessage(TMessageType messageType, Type type)
    {
      if (m_typeToMessageType.ContainsKey(type))
      {
        throw new ArgumentException("type already registered");
      }

      if (m_serializers.ContainsKey(messageType))
      {
        throw new ArgumentException("message type already registered");
      }

      SerializerBuilder serializerBuilder = new SerializerBuilder(type);

      var serializer = serializerBuilder.CreateSerializer();

      var deserializer = serializerBuilder.CreateDeserializer();

      m_serializers.Add(messageType, serializer);
      m_deserializers.Add(messageType, deserializer);
      m_typeToMessageType.Add(type, messageType);
    }

    public void RegisterMessage<TMessage>(TMessageType messageType)
    {
      RegisterMessage(messageType, typeof(TMessage));
    }

    internal Func<object, byte[][]> GetSerializer(TMessageType messageType)
    {
      Func<object, byte[][]> serializer;

      if (m_serializers.TryGetValue(messageType, out serializer))
      {
        return serializer;
      }
      else
      {
        throw new NetMQProtocolException(string.Format("Message type {0} doesn't exist", messageType));
      }
    }

    internal Func<byte[][], object> GetDeserializer(TMessageType messageType)
    {      
      Func<byte[][], object> deserializer;

      if (m_deserializers.TryGetValue(messageType, out deserializer))
      {
        return deserializer;
      }
      else
      {
        throw new NetMQProtocolException(string.Format("Message type {0} doesn't exist", messageType));
      }
    }

    public TMessageType GetMessageType(object message)
    {
      Type type = message.GetType();

      TMessageType messageType;

      if (m_typeToMessageType.TryGetValue(type, out messageType))
      {
        return messageType;
      }
      else
      {
        throw new NetMQProtocolException(string.Format("Type {0} is not registered", type.FullName));
      }
    }

    protected internal abstract TMessageType ConvertBytesToMessageType(byte[] bytes);

    protected internal abstract byte[] ConvertMessageTypeToBytes(TMessageType messageType);
  }
}
