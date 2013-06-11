using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security
{
	public enum NetMQSecurityErrorCode
	{
		/// <summary>
		/// The number of frames of a message is different than expected
		/// </summary>
		InvalidFramesCount,

		/// <summary>
		/// The length of a plain frame is diffrent than expected
		/// </summary>
		InvalidFrameLength,

		/// <summary>
		/// Protcol version is not matching the implementation
		/// </summary>
		InvalidProtocolVersion,

		/// <summary>
		/// Diffrent content type was expected
		/// </summary>
		InvalidContentType,

		/// <summary>
		/// Trying to encrypted a message while the secure channel is not ready
		/// </summary>
		SecureChannelNotReady,

		/// <summary>
		/// Message which was already handled was sent again (or message with very old sequence number).
		/// </summary>
		ReplayAttack,

		/// <summary>
		/// The MAC of the frame didn't match to the content of the frame
		/// </summary>
		MACNotMatched,

		/// <summary>
		/// Frames were removed from the encrypted message
		/// </summary>
		EncryptedFramesMissing,

		/// <summary>
		/// Encrypted frame length is not multiplication of Block Size
		/// </summary>
		EncryptedFrameInvalidLength,

		/// <summary>
		/// The handshake layer expected a diffrent message type
		/// </summary>
		HandshakeUnexpectedMessage,

		/// <summary>
		/// Client failed to verify server certificate
		/// </summary>
		HandshakeVerifyCertificateFailed,

		/// <summary>
		/// Verify data on finished message failed
		/// </summary>
		HandshakeVerifyData,				
	}

	/// <summary>
	/// Thrown by the SecureChannel when error occured, check the ErrorCode property for the specific error.
	/// </summary>
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
