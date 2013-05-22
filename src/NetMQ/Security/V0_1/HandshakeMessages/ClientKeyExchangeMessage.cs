using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class ClientKeyExchangeMessage : HandshakeMessage
  {
    public const int PreMasterSecretLength = 48;

    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.ClientKeyExchange; }
    }

    public byte[] EncryptedPreMasterSecret { get; set; }

    public override NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = base.ToNetMQMessage();
      message.Append(EncryptedPreMasterSecret);

      return message;
    }

    public override void SetFromNetMQMessage(NetMQMessage message)
    {
      base.SetFromNetMQMessage(message);

			if (message.FrameCount != 1)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      NetMQFrame preMasterSecretFrame = message.Pop();

      EncryptedPreMasterSecret = preMasterSecretFrame.ToByteArray();
    }

    
  }
}
