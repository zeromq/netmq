using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security
{
	public enum NetMQSecurityErrorCode
	{
		WrongFramesCount,
		WrongFrameLength,
		InvalidProtocolVersion,
		InvalidContentType,
		SecureChannelNotReady,
		ReplayAttack,
		MACNotMatched,
		FramesMissing,
		InvalidBlockSize,
		HandshakeUnexpectedMessage,
		HandshakeVerifyCertificateFailed,
		HandshakeVerifyData,				
	}

  public class NetMQSecurityException : Exception
  {  	
  	public NetMQSecurityException(NetMQSecurityErrorCode errorCode, string message)
			: base(message)
		{
			ErrorCode = errorCode;
		}

		public NetMQSecurityErrorCode ErrorCode { get; private set; }  
  }
}
