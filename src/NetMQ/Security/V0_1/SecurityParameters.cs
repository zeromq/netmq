using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Security.V0_1
{
  public enum ConnectionEnd
  {
    Server,Client       
  }

  public enum PRFAlgorithm
  {
    SHA256
  }

  public enum BulkCipherAlgorithm
  {
    Null=0, AES=3
  }

  public enum CipherType
  {
    Block=1
  }

  public enum MACAlgorithm
  {
    Null = 0, HMACSha1=2, HMACSha256
  }  

  public  enum CompressionMethod
  {
    Null = 0,
  }


  public class SecurityParameters
  {
    public ConnectionEnd Entity { get; set; }
    public PRFAlgorithm PRFAlgorithm { get; set; }
    public BulkCipherAlgorithm BulkCipherAlgorithm { get; set; }
    public CipherType CipherType { get; set; }
    public byte EncKeyLength { get; set; }
    public byte BlockLength { get; set; }
    public byte FixedIVLength { get; set; }
    public byte RecordIVLength { get; set; }
    public MACAlgorithm MACAlgorithm { get; set; }
    public byte MACLength { get; set; }
    public byte MACKeyLength { get; set; }
    public CompressionMethod CompressionAlgorithm { get; set; }
    public byte[] MasterSecret { get; set; }
    public byte[] ClientRandom { get; set; }
    public byte[] ServerRandom { get; set; }
  }
}
