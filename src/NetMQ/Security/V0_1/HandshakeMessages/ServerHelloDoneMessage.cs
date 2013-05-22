using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class ServerHelloDoneMessage : HandshakeMessage
  {
    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.ServerHelloDone; }
    }

		public override void SetFromNetMQMessage(NetMQMessage message)
		{
			base.SetFromNetMQMessage(message);

			if (message.FrameCount != 0)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount,  "Malformed message");
			}
		}
  }
}
