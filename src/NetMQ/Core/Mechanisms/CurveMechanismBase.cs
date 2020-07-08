using System;
using System.Text;
using NaCl;

namespace NetMQ.Core.Mechanisms
{
    internal abstract class CurveMechanismBase : Mechanism
    {
        protected static byte[] HelloNoncePrefix = Encoding.ASCII.GetBytes("CurveZMQHELLO---");
        protected static byte[] CookieNoncePrefix = Encoding.ASCII.GetBytes("COOKIE--");
        protected static byte[] WelcomeNoncePrefix = Encoding.ASCII.GetBytes("WELCOME-");
        protected static byte[] InitiatieNoncePrefix = Encoding.ASCII.GetBytes("CurveZMQINITIATE");
        protected static byte[] VouchNoncePrefix = Encoding.ASCII.GetBytes("VOUCH---");
        protected static byte[] ReadyNoncePrefix = Encoding.ASCII.GetBytes("CurveZMQREADY---");
        protected static readonly byte[] WelcomeLiteral = Encoding.ASCII.GetBytes("\x07WELCOME");
        protected static readonly byte[] ReadyLiteral = Encoding.ASCII.GetBytes("\x05READY");
        protected static readonly byte[] HelloLiteral = Encoding.ASCII.GetBytes("\x05HELLO");
        protected static readonly byte[] InitiateLiteral = Encoding.ASCII.GetBytes("\x08INITIATE");
        private static readonly byte[] MessageLiteral = Encoding.ASCII.GetBytes("\x07MESSAGE");

        private readonly byte[] m_encodeNoncePrefix;
        private readonly byte[] m_decodeNoncePrefix;

        protected UInt64 m_nonce;
        protected UInt64 m_peerNonce;
        protected Curve25519XSalsa20Poly1305? m_box;

        protected CurveMechanismBase(SessionBase session, Options options,
            string encodeNoncePrefix, string decodeNoncePrefix) : base(session, options)
        {
            m_encodeNoncePrefix = Encoding.ASCII.GetBytes(encodeNoncePrefix);
            m_decodeNoncePrefix = Encoding.ASCII.GetBytes(decodeNoncePrefix);

            if (m_encodeNoncePrefix.Length != 16)
                throw new ArgumentException();

            if (m_decodeNoncePrefix.Length != 16)
                throw new ArgumentException();

            m_nonce = 1;
            m_peerNonce = 1;
        }

        public override void Dispose()
        {
            m_box?.Dispose();
        }

        public override PullMsgResult Encode(ref Msg msg)
        {
            Span<byte> messageNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            m_encodeNoncePrefix.CopyTo(messageNonce);
            NetworkOrderBitsConverter.PutUInt64(m_nonce, messageNonce.Slice(16));

            byte flags = 0;
            if (msg.HasMore)
                flags |= 0x01;
            if (msg.HasCommand)
                flags |= 0x02;

            Msg plaintext = new Msg();
            plaintext.InitPool(msg.Size + 1);
            plaintext[0] = flags;
            msg.CopyTo(plaintext.Slice(1));

            msg.Close();
            msg.InitPool(16 + Curve25519XSalsa20Poly1305.TagLength + plaintext.Size);

            Assumes.NotNull(m_box);

            m_box.Encrypt(msg.Slice(16), plaintext, messageNonce);
            plaintext.Close();

            MessageLiteral.CopyTo(msg);
            NetworkOrderBitsConverter.PutUInt64(m_nonce, msg.Slice(8));

            m_nonce++;
            return PullMsgResult.Ok;
        }

        public override PushMsgResult Decode(ref Msg msg)
        {
            if (!CheckBasicCommandStructure(ref msg))
                return PushMsgResult.Error;

            int size = msg.Size;

            if (!IsCommand("MESSAGE", ref msg))
                return PushMsgResult.Error;

            if (size < 33) // Size of 16 bytes of command + 16 bytes of MAC + 1 byte for flag
                return PushMsgResult.Error;

            Span<byte> messageNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            m_decodeNoncePrefix.CopyTo(messageNonce);
            msg.Slice(8,8).CopyTo(messageNonce.Slice(16));

            UInt64 nonce = NetworkOrderBitsConverter.ToUInt64(msg, 8);
            if (nonce <= m_peerNonce)
                return PushMsgResult.Error;

            m_peerNonce = nonce;

            Msg plain = new Msg();
            plain.InitPool(msg.Size - 16 - Curve25519XSalsa20Poly1305.TagLength);

            Assumes.NotNull(m_box);

            var isAuthenticate = m_box.TryDecrypt(plain,msg.Slice(16), messageNonce);

            if (!isAuthenticate)
            {
                plain.Close();
                return PushMsgResult.Error;
            }

            msg.Move(ref plain);

            byte flags = msg[0];
            if ((flags & 0x01) != 0)
                msg.SetFlags(MsgFlags.More);
            if ((flags & 0x02) != 0)
                msg.SetFlags(MsgFlags.Command);

            msg.TrimPrefix(1);

            return PushMsgResult.Ok;
        }
    }
}