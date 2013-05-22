using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class CertificateMessage : HandshakeMessage
  {
    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.Certificate;}
    }

    public X509Certificate2 Certificate { get; set; }
 
    public override void SetFromNetMQMessage(NetMQMessage message)
    {
      base.SetFromNetMQMessage(message);

			if (message.FrameCount != 1)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      NetMQFrame certificateFrame = message.Pop();

      byte[] certificteBytes = certificateFrame.ToByteArray();      

      Certificate = new X509Certificate2();
      Certificate.Import(certificteBytes);
    }

    public override NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = base.ToNetMQMessage();

      message.Append(Certificate.Export(X509ContentType.Cert));

      return message;
    }
  }
}
