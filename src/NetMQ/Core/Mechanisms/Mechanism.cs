using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NetMQ.Core.Mechanisms
{
    internal enum MechanismState 
    {
        Handshaking,
        Ready,
        Error
    }

    internal abstract class Mechanism
    {
        static class SocketNames
        {
            public const string Pair = "PAIR";
            public const string Pub = "PUB";
            public const string Sub = "SUB";
            public const string Req = "REQ";
            public const string Rep = "REP";
            public const string Dealer = "DEALER";
            public const string Router = "ROUTER";
            public const string Pull = "PULL";
            public const string Push = "PUSH";
            public const string Xpub = "XPUB";
            public const string Xsub = "XSUB";
            public const string Stream = "STREAM";    
            public const string Peer = "PEER";
        }
        
        const int NameLengthSize = sizeof(byte);
        const int ValueLengthSize = sizeof(Int32);

        private const string ZmtpPropertySocketType = "Socket-Type";
        private const string ZmtpPropertyIdentity = "Identity";

        public Mechanism(SessionBase session, Options options)
        {
            Session = session;
            Options = options;
        }
        
        /// <summary>
        /// Prepare next handshake command that is to be sent to the peer.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public abstract PullMsgResult NextHandshakeCommand (ref Msg msg);

        /// <summary>
        /// Process the handshake command received from the peer.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public abstract PushMsgResult ProcessHandshakeCommand (ref Msg msg);

        public virtual PullMsgResult Encode(ref Msg msg)
        {
            return PullMsgResult.Ok;
        }

        public virtual PushMsgResult Decode(ref Msg msg)
        {
            return PushMsgResult.Ok;
        }

        /// <summary>
        /// Returns the status of this mechanism.
        /// </summary>
        public abstract MechanismState State { get; }
        
        public byte[] PeerIdentity { get; set; }

        public SessionBase Session { get; }
        protected Options Options { get; }
        
        /// <summary>
        ///  Only used to identify the socket for the Socket-Type
        ///  property in the wire protocol. 
        /// </summary>
        /// <param name="socketType"></param>
        protected string GetSocketName(ZmqSocketType socketType)
        {
            switch (socketType)
            {
                case ZmqSocketType.Pair:
                    return SocketNames.Pair;
                case ZmqSocketType.Pub:
                    return SocketNames.Pub;
                case ZmqSocketType.Sub:
                    return SocketNames.Sub;
                case ZmqSocketType.Req:
                    return SocketNames.Req;
                case ZmqSocketType.Rep:
                    return SocketNames.Rep;
                case ZmqSocketType.Dealer:
                    return SocketNames.Dealer;
                case ZmqSocketType.Router:
                    return SocketNames.Router;
                case ZmqSocketType.Pull:
                    return SocketNames.Pull;
                case ZmqSocketType.Push:
                    return SocketNames.Push;
                case ZmqSocketType.Xpub:
                    return SocketNames.Xpub;
                case ZmqSocketType.Xsub:
                    return SocketNames.Xsub;
                case ZmqSocketType.Stream:
                    return SocketNames.Stream;
                case ZmqSocketType.Peer:
                    return SocketNames.Peer;
                default:
                    throw new ArgumentOutOfRangeException(nameof(socketType), socketType, null);
            }
        }
        
        protected int AddProperty(byte[] output, int outputOffset, string name, byte[] value)
        {
            if (name.Length > 255)
                throw new ArgumentException("property name length exceed maximum size");
            
            int totalLength =  GetPropertyLength(name, value.Length);
            if (totalLength > (output.Length - outputOffset))
                throw new Exception("totalLength of property exceed maximum size");

            output[outputOffset] = (byte) name.Length;
            outputOffset += NameLengthSize;
            System.Text.Encoding.ASCII.GetBytes(name, 0, name.Length, output, outputOffset);
            outputOffset += name.Length;
            NetworkOrderBitsConverter.PutInt32(value.Length, output, outputOffset);
            outputOffset += ValueLengthSize;
            Buffer.BlockCopy(value, 0, output, outputOffset, value.Length);
            
            return totalLength;
        }

        protected int AddProperty(byte[] output, int outputOffset, string name, string value)
        {
            return AddProperty(output, outputOffset, name, System.Text.Encoding.ASCII.GetBytes(value));
        }

        protected  int GetPropertyLength(string name, int valueLength)
        {
            return NameLengthSize + name.Length + ValueLengthSize + valueLength;
        }

        protected  int AddBasicProperties(byte[] output, int offset)
        {
            int initialOffset = offset;
            
            //  Add socket type property
            string socketName = GetSocketName(Options.SocketType);
            offset += AddProperty(output, offset, ZmtpPropertySocketType, socketName);
            
            //  Add identity property
            if (Options.SocketType == ZmqSocketType.Req || 
                Options.SocketType == ZmqSocketType.Dealer || 
                Options.SocketType == ZmqSocketType.Router)
            {
                if (Options.Identity != null)
                    offset += AddProperty(output, offset, ZmtpPropertyIdentity, Options.Identity);
                else                     
                    offset += AddProperty(output, offset, ZmtpPropertyIdentity, new byte[0]);
            }

            return offset - initialOffset;
        }

        protected int BasicPropertiesLength
        {
            get
            {
                string socketName = GetSocketName(Options.SocketType);

                int length = GetPropertyLength(ZmtpPropertySocketType, socketName.Length);

                if (Options.SocketType == ZmqSocketType.Req ||
                    Options.SocketType == ZmqSocketType.Dealer ||
                    Options.SocketType == ZmqSocketType.Router)
                    length += GetPropertyLength(ZmtpPropertyIdentity, Options.IdentitySize);

                return length;    
            }
        }

        protected void MakeCommandWithBasicProperties(ref Msg msg, string prefix)
        {
            int commandSize = 1 + prefix.Length + BasicPropertiesLength;
            msg.InitPool(commandSize);
            msg.Put((byte)prefix.Length, 0);
            Encoding.ASCII.GetBytes(prefix, 0, prefix.Length, msg.Data, msg.Offset + 1);
            
            AddBasicProperties(msg.Data, msg.Offset + prefix.Length + 1);
        }

        /// <summary>
        /// Parses a metadata.
        /// Metadata consists of a list of properties consisting of
        /// name and value as size-specified strings.
        /// </summary>
        /// <returns>Returns true on success and false on error.</returns>
        protected bool ParseMetadata(byte[] source, int offset, int length)
        {
            int bytesLeft = length;
            
            while (bytesLeft > 1)
            {
                int nameLength = source[offset];
                offset += NameLengthSize;
                bytesLeft -= NameLengthSize;
                if (bytesLeft < nameLength)
                    break;

                string name = Encoding.ASCII.GetString(source, offset, nameLength);
                offset += nameLength;
                bytesLeft -= nameLength;
                if (bytesLeft < ValueLengthSize)
                    break;

                int valueLength = NetworkOrderBitsConverter.ToInt32(source, offset);
                offset += ValueLengthSize;
                bytesLeft -= ValueLengthSize;
                if (bytesLeft < valueLength)
                    break;
                
                byte[] value = new byte[valueLength];
                Buffer.BlockCopy(source, offset, value, 0, valueLength);
                offset += valueLength;
                bytesLeft -= valueLength;

                if (name == ZmtpPropertyIdentity && Options.RecvIdentity)
                    PeerIdentity = value;
                else if (name == ZmtpPropertySocketType) 
                {
                    if (!CheckSocketType(Encoding.ASCII.GetString(value)))
                        return false;
                } 
                else
                {
                    if (!GetProperty(name, value, valueLength))
                        return false;
                }
            }
            
            if (bytesLeft > 0)
                return false;

            return true;
        }

        /// <summary>
        /// This is called by ParseProperty method whenever it
        /// parses a new property. The function returns true
        /// on success and false on error. Signaling error prevents parser from
        /// parsing remaining data.
        /// Derived classes are supposed to override this
        /// method to handle custom processing. 
        /// </summary>
        /// <returns></returns>
        protected virtual bool GetProperty(string name, byte[] output, int size)
        {
            //  Default implementation does not check
            //  property values and returns true to signal success.
            return true;
        }

        /// <summary>
        /// Returns true iff socket associated with the mechanism
        /// is compatible with a given socket type 'type'.
        /// </summary>
        /// <returns></returns>
        private bool CheckSocketType(string type)
        {
            switch (Options.SocketType)
            {
                case ZmqSocketType.Pair:
                    return type == SocketNames.Pair;
                case ZmqSocketType.Pub:
                case ZmqSocketType.Xpub:
                    return type == SocketNames.Sub || type == SocketNames.Xsub;
                case ZmqSocketType.Sub:
                case ZmqSocketType.Xsub:
                    return type == SocketNames.Pub || type == SocketNames.Xpub;
                case ZmqSocketType.Req:
                    return type == SocketNames.Rep || type == SocketNames.Router;
                case ZmqSocketType.Rep:
                    return type == SocketNames.Req || type == SocketNames.Dealer;
                case ZmqSocketType.Dealer:
                    return type == SocketNames.Dealer || type == SocketNames.Router || type == SocketNames.Rep;
                case ZmqSocketType.Router:
                    return type == SocketNames.Dealer || type == SocketNames.Router || type == SocketNames.Req;
                case ZmqSocketType.Pull:
                    return type == SocketNames.Push;
                case ZmqSocketType.Push:
                    return type == SocketNames.Pull;
                case ZmqSocketType.Peer:
                    return type == SocketNames.Peer;
                default:
                    return false;
            }
        }

        protected bool CheckBasicCommandStructure(ref Msg msg)
        {
            if (msg.Size <= 1 || 
                msg.Size <= msg[0]) {
                // TODO: Session.Socket.EventHandshakeFailedProtocol 

                return false;
            }
            return true;
        }
    }
}