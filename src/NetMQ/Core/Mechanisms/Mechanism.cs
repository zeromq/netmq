using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NetMQ.Core.Utils;
using NetMQ.Utils;

namespace NetMQ.Core.Mechanisms
{
    internal enum MechanismStatus 
    {
        Handshaking,
        Ready,
        Error
    }

    internal abstract class Mechanism : IDisposable
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
            public const string Server = "SERVER";
            public const string Client = "CLIENT";
            public const string Radio = "RADIO";
            public const string Dish = "DISH";
            public const string Gather = "GATHER";
            public const string Scatter = "SCATTER";
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

        /// <summary>
        /// Dispose the mechanism resources
        /// </summary>
        public abstract void Dispose();

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
        public abstract MechanismStatus Status { get; }
        
        public byte[]? PeerIdentity { get; set; }

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
                case ZmqSocketType.Server:
                    return SocketNames.Server;
                case ZmqSocketType.Client:
                    return SocketNames.Client;
                case ZmqSocketType.Radio:
                    return SocketNames.Radio;
                case ZmqSocketType.Dish:
                    return SocketNames.Dish;
                case ZmqSocketType.Gather:
                    return SocketNames.Gather;
                case ZmqSocketType.Scatter:
                    return SocketNames.Scatter;
                default:
                    throw new ArgumentOutOfRangeException(nameof(socketType), socketType, null);
            }
        }
        
        protected int AddProperty(Span<byte> output, string name, byte[] value)
        {
            if (name.Length > 255)
                throw new ArgumentException("property name length exceed maximum size");
            
            int totalLength =  GetPropertyLength(name, value.Length);
            if (totalLength > output.Length)
                throw new Exception("totalLength of property exceed maximum size");

            output[0] = (byte) name.Length;
            output = output.Slice(NameLengthSize);
            System.Text.Encoding.ASCII.GetBytes(name, output);
            output = output.Slice(name.Length);
            NetworkOrderBitsConverter.PutInt32(value.Length, output);
            output = output.Slice(ValueLengthSize);
            value.CopyTo(output);
            
            return totalLength;
        }

        protected int AddProperty(Span<byte> output, string name, string value)
        {
            return AddProperty(output, name, Encoding.ASCII.GetBytes(value));
        }

        protected  int GetPropertyLength(string name, int valueLength)
        {
            return NameLengthSize + name.Length + ValueLengthSize + valueLength;
        }

        protected void AddBasicProperties(Span<byte> output)
        {
            //  Add socket type property
            string socketName = GetSocketName(Options.SocketType);
            int written = AddProperty(output, ZmtpPropertySocketType, socketName);
            output = output.Slice(written);
            
            //  Add identity property
            if (Options.SocketType == ZmqSocketType.Req || 
                Options.SocketType == ZmqSocketType.Dealer || 
                Options.SocketType == ZmqSocketType.Router)
            {
                if (Options.Identity != null)
                {
                    written = AddProperty(output, ZmtpPropertyIdentity, Options.Identity);
                    output = output.Slice(written);
                }
                else
                {
                    written = AddProperty(output, ZmtpPropertyIdentity, new byte[0]);
                    output = output.Slice(written);
                }
            }
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
            msg.Put(Encoding.ASCII, prefix, 1);
            
            AddBasicProperties(msg.Slice(prefix.Length + 1));
        }

        /// <summary>
        /// Parses a metadata.
        /// Metadata consists of a list of properties consisting of
        /// name and value as size-specified strings.
        /// </summary>
        /// <returns>Returns true on success and false on error.</returns>
        protected bool ParseMetadata(Span<byte> source)
        {
            while (source.Length > 1)
            {
                int nameLength = source[0];
                source = source.Slice(NameLengthSize);
                if (source.Length < nameLength)
                    break;

                string name = SpanUtility.ToAscii(source.Slice(0, nameLength));
                source = source.Slice(nameLength);
                if (source.Length < ValueLengthSize)
                    break;

                int valueLength = NetworkOrderBitsConverter.ToInt32(source);
                source = source.Slice(ValueLengthSize);
                if (source.Length < valueLength)
                    break;
                
                byte[] value = new byte[valueLength];
                source.Slice(0, valueLength).CopyTo(value);
                source = source.Slice(valueLength);

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
            
            if (source.Length > 0)
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
                case ZmqSocketType.Server:
                    return type == SocketNames.Client;
                case ZmqSocketType.Client:
                    return type == SocketNames.Server;
                case ZmqSocketType.Peer:
                    return type == SocketNames.Peer;
                case ZmqSocketType.Radio:
                    return type == SocketNames.Dish;
                case ZmqSocketType.Dish:
                    return type == SocketNames.Radio;
                case ZmqSocketType.Gather:
                    return type == SocketNames.Scatter;
                case ZmqSocketType.Scatter:
                    return type == SocketNames.Gather;
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
        
        internal protected bool IsCommand(string command, ref Msg msg)
        {
            if (msg.Size >= command.Length + 1 && msg[0] == command.Length)
            {
                return msg.GetString(Encoding.ASCII, 1, msg[0]) == command;
            }
            return false;
        }
    }
}
