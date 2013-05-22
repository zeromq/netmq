using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class FinishedMessage : HandshakeMessage
  {
    public const int VerifyDataLength = 12;

    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.Finished; }
    }

    public byte[] VerifyData { get; set; }

    public override void SetFromNetMQMessage(NetMQMessage message)
    {
      base.SetFromNetMQMessage(message);

			if (message.FrameCount != 1)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      NetMQFrame verifyDataFrame = message.Pop();

      VerifyData = verifyDataFrame.ToByteArray();
    }

    public override NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = base.ToNetMQMessage();
      message.Append(VerifyData);

      return message;
    }
  }
}
