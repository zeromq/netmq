using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NaCl;
using NetMQ.Core.Utils;

namespace NetMQ.Core.Mechanisms
{
    internal class CurveServerMechanism : CurveMechanismBase
    {
        protected enum State
        {
            WaitingForHello,
            SendingWelcome,
            SendingReady,
            WaitingForInitiate,
            Ready
        }

        private byte[] m_secretKey;
        private byte[] m_cnSecretKey;
        private byte[] m_cnPublicKey;
        private byte[] m_cnClientKey;
        private byte[] m_cookieKey;
        private State m_state;

        public CurveServerMechanism(SessionBase session, Options options) :
            base(session, options, "CurveZMQMESSAGES", "CurveZMQMESSAGEC")
        {
            m_secretKey = (byte[]) options.CurveSecretKey.Clone();
            m_cnClientKey = new byte[Curve25519XSalsa20Poly1305.KeyLength];
            m_cookieKey = new byte[Curve25519XSalsa20Poly1305.KeyLength];
            Curve25519XSalsa20Poly1305.KeyPair(out m_cnSecretKey, out m_cnPublicKey);
        }
        
        public override void Dispose()
        {
            base.Dispose();
            Array.Clear(m_secretKey, 0, 32);
            Array.Clear(m_cnSecretKey, 0, 32);
            Array.Clear(m_cnPublicKey, 0, 32);
            Array.Clear(m_cnClientKey, 0, 32);
            Array.Clear(m_cookieKey, 0, m_cookieKey.Length);
        }

        public override MechanismStatus Status
        {
            get
            {
                if (m_state == State.Ready)
                    return MechanismStatus.Ready;
                else
                    return MechanismStatus.Handshaking;
            }
        }

        public override PullMsgResult NextHandshakeCommand(ref Msg msg)
        {
            PullMsgResult result;

            switch (m_state)
            {
                case State.SendingWelcome:
                    result = ProduceWelcome(ref msg);
                    if (result == PullMsgResult.Ok)
                        m_state = State.WaitingForInitiate;
                    break;
                case State.SendingReady:
                    result = ProduceReady(ref msg);
                    if (result == PullMsgResult.Ok)
                        m_state = State.Ready;
                    break;
                default:
                    result = PullMsgResult.Empty;
                    break;
            }

            return result;
        }

        public override PushMsgResult ProcessHandshakeCommand(ref Msg msg)
        {
            PushMsgResult result;

            switch (m_state)
            {
                case State.WaitingForHello:
                    result = ProcessHello(ref msg);
                    break;
                case State.WaitingForInitiate:
                    result = ProcessInitiate(ref msg);
                    break;
                default:
                    return PushMsgResult.Error;
            }

            if (result == PushMsgResult.Ok)
            {
                msg.Close();
                msg.InitEmpty();
            }

            return result;
        }

        PushMsgResult ProcessHello(ref Msg msg)
        {
            if (!CheckBasicCommandStructure(ref msg))
                return PushMsgResult.Error;

            Span<byte> hello = msg;

            if (!IsCommand("HELLO", ref msg))
                return PushMsgResult.Error;

            if (hello.Length != 200)
                return PushMsgResult.Error;

            byte major = hello[6];
            byte minor = hello[7];

            if (major != 1 || minor != 0)
            {
                // client HELLO has unknown version number
                return PushMsgResult.Error;
            }

            // Save client's short-term public key (C')
            hello.Slice(80, 32).CopyTo(m_cnClientKey);

            Span<byte> helloNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            HelloNoncePrefix.CopyTo(helloNonce);
            hello.Slice(112, 8).CopyTo(helloNonce.Slice(16));
            m_peerNonce = NetworkOrderBitsConverter.ToUInt64(hello, 112);

            using var box = new Curve25519XSalsa20Poly1305(m_secretKey, m_cnClientKey);

            Span<byte> helloPlaintext = stackalloc byte[80];
            bool isDecrypted = box.TryDecrypt(helloPlaintext, hello.Slice(120, 80), helloNonce);
            if (!isDecrypted)
                return PushMsgResult.Error;

            helloPlaintext.Clear();
            
            m_state = State.SendingWelcome;
            return PushMsgResult.Ok;
        }

        PullMsgResult ProduceWelcome(ref Msg msg)
        {
            Span<byte> cookieNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> cookiePlaintext = stackalloc byte[64];
            Span<byte> cookieCiphertext = stackalloc byte[64 + XSalsa20Poly1305.TagLength];

            //  Create full nonce for encryption
            //  8-byte prefix plus 16-byte random nonce
            CookieNoncePrefix.CopyTo(cookieNonce);
            using var rng = RandomNumberGenerator.Create();
#if NETSTANDARD2_1
            rng.GetBytes(cookieNonce.Slice(8));
#else
            byte[] temp = new byte[16];
            rng.GetBytes(temp);
            temp.CopyTo(cookieNonce.Slice(8));
#endif

            // Generate cookie = Box [C' + s'](t)
            m_cnClientKey.CopyTo(cookiePlaintext);
            m_cnSecretKey.CopyTo(cookiePlaintext.Slice(32));

            // Generate fresh cookie key
            rng.GetBytes(m_cookieKey);

            // Encrypt using symmetric cookie key
            using var secretBox = new XSalsa20Poly1305(m_cookieKey);
            secretBox.Encrypt(cookieCiphertext, cookiePlaintext, cookieNonce);
            cookiePlaintext.Clear();

            Span<byte> welcomeNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> welcomePlaintext = stackalloc byte[128];
            Span<byte> welcomeCiphertext = stackalloc byte[128 + Curve25519XSalsa20Poly1305.TagLength];

            //  Create full nonce for encryption
            //  8-byte prefix plus 16-byte random nonce
            WelcomeNoncePrefix.CopyTo(welcomeNonce);
#if NETSTANDARD2_1
            rng.GetBytes(welcomeNonce.Slice(8));
#else
            rng.GetBytes(temp);
            temp.CopyTo(welcomeNonce.Slice(8));
#endif

            // Create 144-byte Box [S' + cookie](S->C')
            m_cnPublicKey.CopyTo(welcomePlaintext);
            cookieNonce.Slice(8).CopyTo(welcomePlaintext.Slice(32));
            cookieCiphertext.CopyTo(welcomePlaintext.Slice(48));
            using var box = new Curve25519XSalsa20Poly1305(m_secretKey, m_cnClientKey);
            box.Encrypt(welcomeCiphertext, welcomePlaintext, welcomeNonce);
            welcomePlaintext.Clear();

            msg.InitPool(168); // TODO: we can save some allocation here by allocating this earlier
            Span<byte> welcome = msg;
            WelcomeLiteral.CopyTo(welcome);
            welcomeNonce.Slice(8, 16).CopyTo(welcome.Slice(8));
            welcomeCiphertext.CopyTo(welcome.Slice(24));

            return PullMsgResult.Ok;
        }

        PushMsgResult ProcessInitiate(ref Msg msg)
        {
            if (!CheckBasicCommandStructure(ref msg))
                return PushMsgResult.Error;

            Span<byte> initiate = msg;

            if (!IsCommand("INITIATE", ref msg))
                return PushMsgResult.Error;

            if (initiate.Length < 257)
                return PushMsgResult.Error;

            Span<byte> cookieNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> cookiePlaintext = stackalloc byte[64];
            Span<byte> cookieBox = initiate.Slice(25, 80);

            CookieNoncePrefix.CopyTo(cookieNonce);
            initiate.Slice(9, 16).CopyTo(cookieNonce.Slice(8));

            using var secretBox = new XSalsa20Poly1305(m_cookieKey);
            bool decrypted = secretBox.TryDecrypt(cookiePlaintext, cookieBox, cookieNonce);

            if (!decrypted)
                return PushMsgResult.Error;

            //  Check cookie plain text is as expected [C' + s']
            if (!SpanUtility.Equals(m_cnClientKey, cookiePlaintext.Slice(0, 32)) ||
                !SpanUtility.Equals(m_cnSecretKey, cookiePlaintext.Slice(32, 32)))
                return PushMsgResult.Error;

            Span<byte> initiateNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            byte[] initiatePlaintext = new byte[msg.Size - 113];
            var initiateBox = initiate.Slice(113);

            InitiatieNoncePrefix.CopyTo(initiateNonce);
            initiate.Slice(105, 8).CopyTo(initiateNonce.Slice(16));
            m_peerNonce = NetworkOrderBitsConverter.ToUInt64(initiate, 105);

            using var box = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_cnClientKey);
            bool decrypt = box.TryDecrypt(initiatePlaintext, initiateBox, initiateNonce);
            if (!decrypt)
                return PushMsgResult.Error;
            
            Span<byte> vouchNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> vouchPlaintext = stackalloc byte[64];
            Span<byte> vouchBox = new Span<byte>(initiatePlaintext, 48, 80);
            var clientKey = new Span<byte>(initiatePlaintext, 0, 32);

            VouchNoncePrefix.CopyTo(vouchNonce);
            new Span<byte>(initiatePlaintext, 32, 16).CopyTo(vouchNonce.Slice(8));

            using var box2 = new Curve25519XSalsa20Poly1305(m_cnSecretKey, clientKey);
            decrypt = box2.TryDecrypt(vouchPlaintext, vouchBox, vouchNonce);
            if (!decrypt)
                return PushMsgResult.Error;

            //  What we decrypted must be the client's short-term public key
            if (!SpanUtility.Equals(vouchPlaintext.Slice(0, 32), m_cnClientKey))
                return PushMsgResult.Error;

            //  Create the session box
            m_box = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_cnClientKey);

            //  This supports the Stonehouse pattern (encryption without authentication).
            m_state = State.SendingReady;

            if (!ParseMetadata(new Span<byte>(initiatePlaintext, 128, initiatePlaintext.Length - 128 - 16)))
                return PushMsgResult.Error;
            
            vouchPlaintext.Clear();
            Array.Clear(initiatePlaintext, 0, initiatePlaintext.Length);

            return PushMsgResult.Ok;
        }

        PullMsgResult ProduceReady(ref Msg msg)
        {
            int metadataLength = BasicPropertiesLength;
            Span<byte> readyNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            byte[] readyPlaintext = new byte[metadataLength];

            //  Create Box [metadata](S'->C')
            AddBasicProperties(readyPlaintext);

            ReadyNoncePrefix.CopyTo(readyNonce);
            NetworkOrderBitsConverter.PutUInt64(m_nonce, readyNonce.Slice(16));

            msg.InitPool(14 + Curve25519XSalsa20Poly1305.TagLength + metadataLength);
            var readyBox = msg.Slice(14);

            Assumes.NotNull(m_box);
            
            m_box.Encrypt(readyBox, readyPlaintext, readyNonce);
            Array.Clear(readyPlaintext, 0, readyPlaintext.Length);
            
            Span<byte> ready = msg;
            ReadyLiteral.CopyTo(ready);

            //  Short nonce, prefixed by "CurveZMQREADY---"
            readyNonce.Slice(16).CopyTo(ready.Slice(6));

            m_nonce++;

            return 0;
        }
    }
}