using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NetMQ.Security.V0_1;

namespace NetMQ.Security
{
	public delegate bool VerifyCertificateDelegate(X509Certificate2 certificate2);

  public interface ISecureChannel : IDisposable
  {
    bool SecureChannelReady { get; }
    
    X509Certificate2 Certificate { get; set; }
    
    CipherSuite[] AllowedCipherSuites { get; set; }  	

  	void SetVerifyCertificate(VerifyCertificateDelegate verifyCertificate);
    
    bool ProcessMessage(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMesssages);
    
    NetMQMessage EncryptApplicationMessage(NetMQMessage plainMessage);
    
    NetMQMessage DecryptApplicationMessage(NetMQMessage cipherMessage);    
  }
}