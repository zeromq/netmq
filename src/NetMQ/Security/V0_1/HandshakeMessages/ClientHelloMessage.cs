using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1.HandshakeMessages
{
  class ClientHelloMessage : HandshakeMessage
  {
    public byte[] RandomNumber { get; set; }

    public override HandshakeType HandshakeType
    {
      get { return HandshakeType.ClientHello; }
    }

    public CipherSuite[] CipherSuites { get; set; }

    public override void SetFromNetMQMessage(NetMQMessage message)
    {
      base.SetFromNetMQMessage(message);

			if (message.FrameCount != 3)
			{
				throw new NetMQSecurityException(NetMQSecurityErrorCode.InvalidFramesCount, "Malformed message");
			}

      // get the randon number
      NetMQFrame randomNumberFrame = message.Pop();
      RandomNumber = randomNumberFrame.ToByteArray();

      // get the length of the ciphers array
      NetMQFrame ciphersLengthFrame = message.Pop();
      int ciphersLength = BitConverter.ToInt32(ciphersLengthFrame.Buffer, 0);

      // get the ciphers
      NetMQFrame ciphersFrame = message.Pop();
      CipherSuites = new CipherSuite[ciphersLength];
      for (int i = 0; i < ciphersLength; i++)
      {
        CipherSuites[i] = (CipherSuite)ciphersFrame.Buffer[i * 2 + 1];
      }
    }

    public override NetMQMessage ToNetMQMessage()
    {
      NetMQMessage message = base.ToNetMQMessage();
      
      message.Append(RandomNumber);
      
      message.Append(BitConverter.GetBytes(CipherSuites.Length));

      byte[] cipherSuitesBytes = new byte[2 * CipherSuites.Length];

      int bytesIndex = 0;

      foreach (CipherSuite cipherSuite in CipherSuites)
      {
        cipherSuitesBytes[bytesIndex++] = 0;
        cipherSuitesBytes[bytesIndex++] = (byte)cipherSuite;
      }

      message.Append(cipherSuitesBytes);

      return message;
    }
  }
}
