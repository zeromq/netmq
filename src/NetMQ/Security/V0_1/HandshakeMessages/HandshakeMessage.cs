using System;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  public enum HandshakeType : byte
  {
    HelloRequest = 0,
    ClientHello = 1,
    ServerHello = 2,
    Certificate = 11,
    ServerKeyExchange = 12,
    CertificateRequest = 13,
    ServerHelloDone = 14,
    CertificateVerify = 15,
    ClientKeyExchange = 16,
    Finished = 20
  }

  abstract class HandshakeMessage
  {    
    public abstract HandshakeType HandshakeType { get; }

    public virtual NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = new NetMQMessage();
      message.Append(new byte[] {(byte) HandshakeType});

      return message;
    }

    public virtual void SetFromNetMQMessage(NetMQMessage message)
    {
			if (message.FrameCount == 0)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      // remove the handshake type column
      message.Pop();
    }    
  }
}
