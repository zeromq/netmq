using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class ServerHelloMessage : HandshakeMessage
  {
    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.ServerHello; }
    }

    public byte[] RandomNumber { get; set; }

    public CipherSuite CipherSuite { get; set; }

    public override void SetFromNetMQMessage(NetMQMessage message)
    {
      base.SetFromNetMQMessage(message);

			if (message.FrameCount != 2)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      // get the randon number
      NetMQFrame randomNumberFrame = message.Pop();
      RandomNumber = randomNumberFrame.ToByteArray();

      // set the cipher suite
      NetMQFrame cipherSuiteFrame = message.Pop();
      CipherSuite = (CipherSuite)cipherSuiteFrame.Buffer[1];
    }

    public override NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = base.ToNetMQMessage();
      message.Append(RandomNumber);
      message.Append(new  byte[ ] {0, (byte)CipherSuite});

      return message;
    }
  }
}
