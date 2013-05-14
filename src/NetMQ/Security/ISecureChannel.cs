using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetMQ.Security
{
  public interface ISecureChannel : IDisposable
  {
    bool SecureChannelReady { get; }
    
    X509Certificate2 Certificate { get; set; }
    
    CipherSuite[] AllowedCipherSuites { get; set; }
    
    bool ProcessMessage(NetMQMessage incomingMessage, IList<NetMQMessage> outgoingMesssages);
    
    NetMQMessage EncryptApplicationMessage(NetMQMessage plainMessage);
    
    NetMQMessage DecryptApplicationMessage(NetMQMessage cipherMessage);    
  }
}