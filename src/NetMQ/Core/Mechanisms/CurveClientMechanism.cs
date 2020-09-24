using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NaCl;

namespace NetMQ.Core.Mechanisms
{
    internal class CurveClientMechanism : CurveMechanismBase
    {
        enum State
        {
            SendHello,
            ExpectWelcome,
            SendInitiate,
            ExpectReady,
            ErrorReceived,
            Connected
        };

        private byte[] m_secretKey;
        private byte[] m_publicKey;
        private byte[] m_serverKey;

        private byte[] m_cnSecretKey;
        private byte[] m_cnPublicKey;
        private byte[]? m_cnServerKey;
        private byte[]? m_cnCookie;

        private State m_state;

        public CurveClientMechanism(SessionBase session, Options options) :
            base(session, options, "CurveZMQMESSAGEC", "CurveZMQMESSAGES")
        {
            m_secretKey = (byte[]) options.CurveSecretKey.Clone();
            m_publicKey = (byte[]) options.CurvePublicKey.Clone();
            m_serverKey = (byte[]) options.CurveServerKey.Clone();
            Curve25519XSalsa20Poly1305.KeyPair(out m_cnSecretKey, out m_cnPublicKey);
        }

        public override void Dispose()
        {
            base.Dispose();
            Array.Clear(m_secretKey, 0, 32);
            Array.Clear(m_publicKey, 0, 32);
            Array.Clear(m_serverKey, 0, 32);
            Array.Clear(m_cnSecretKey, 0, 32);
            Array.Clear(m_cnPublicKey, 0, 32);
        }

        public override MechanismStatus Status
        {
            get
            {
                if (m_state == State.Connected)
                    return MechanismStatus.Ready;
                if (m_state == State.ErrorReceived)
                    return MechanismStatus.Error;

                return MechanismStatus.Handshaking;
            }
        }

        public override PullMsgResult NextHandshakeCommand(ref Msg msg)
        {
            PullMsgResult result = 0;

            switch (m_state)
            {
                case State.SendHello:
                    result = ProduceHello(ref msg);
                    if (result == PullMsgResult.Ok)
                        m_state = State.ExpectWelcome;
                    break;
                case State.SendInitiate:
                    result = ProduceInitiate(ref msg);
                    if (result == 0)
                        m_state = State.ExpectReady;
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

            if (IsCommand("WELCOME", ref msg))
                result = ProcessWelcome(ref msg);
            else if (IsCommand("READY", ref msg))
                result = ProcessReady(ref msg);
            else if (IsCommand("ERROR", ref msg))
                result = ProcessError(ref msg);
            else
                result = PushMsgResult.Error;

            if (result == PushMsgResult.Ok)
            {
                msg.Close();
                msg.InitEmpty();
            }

            return result;
        }

        PullMsgResult ProduceHello(ref Msg msg)
        {
            msg.InitPool(200);

            Span<byte> helloNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> helloPlaintext = stackalloc byte[64];
            Span<byte> helloBox = msg.Slice(120);

            // Prepare the full nonce
            HelloNoncePrefix.CopyTo(helloNonce);
            NetworkOrderBitsConverter.PutUInt64(m_nonce, helloNonce.Slice(16));

            // Create Box [64 * %x0](C'->S)
            using var box = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_serverKey);
            box.Encrypt(helloBox, helloPlaintext, helloNonce);

            Span<byte> hello = msg;

            HelloLiteral.CopyTo(hello);
            
            //  CurveZMQ major and minor version numbers
            hello[6] = 1;
            hello[7] = 0;

            // 8-80 are left zeros for anti-amplification padding

            //  Client public connection key
            m_cnPublicKey.CopyTo(hello.Slice(80));

            //  Short nonce, prefixed by "CurveZMQHELLO---"
            helloNonce.Slice(16).CopyTo(hello.Slice(112));

            m_nonce++;

            return PullMsgResult.Ok;
        }

        PushMsgResult ProcessWelcome(ref Msg msg)
        {
            if (msg.Size != 168)
                return PushMsgResult.Error;

            Span<byte> welcomeNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> welcomePlaintext = stackalloc byte[128];
            Span<byte> welcomeBox = msg.Slice(24, 144);

            WelcomeNoncePrefix.CopyTo(welcomeNonce);
            msg.Slice(8, 16).CopyTo(welcomeNonce.Slice(8));

            using var box = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_serverKey);
            bool isDecrypted = box.TryDecrypt(welcomePlaintext, welcomeBox, welcomeNonce);
            if (!isDecrypted)
                return PushMsgResult.Error;

            m_cnServerKey = welcomePlaintext.Slice(0, 32).ToArray();
            m_cnCookie = welcomePlaintext.Slice(32, 16 + 80).ToArray();

            // Create the curve box
            m_box = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_cnServerKey);

            m_state = State.SendInitiate;

            return PushMsgResult.Ok;
        }

        PullMsgResult ProduceInitiate(ref Msg msg)
        {
            int metadataLength = BasicPropertiesLength;
            byte[] metadataPlaintext = new byte[metadataLength];
            AddBasicProperties(metadataPlaintext);
            msg.InitPool(113 + 128 + metadataLength + Curve25519XSalsa20Poly1305.TagLength);

            Span<byte> vouchNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> vouchPlaintext = stackalloc byte[64];
            Span<byte> vouchBox = stackalloc byte[Curve25519XSalsa20Poly1305.TagLength + 64];

            //  Create vouch = Box [C',S](C->S')
            m_cnPublicKey.CopyTo(vouchPlaintext);
            m_serverKey.CopyTo(vouchPlaintext.Slice(32));

            VouchNoncePrefix.CopyTo(vouchNonce);
            using var rng = RandomNumberGenerator.Create();
#if NETSTANDARD2_1
            rng.GetBytes(vouchNonce.Slice(8));
#else
            byte[] temp = new byte[16];
            rng.GetBytes(temp);
            temp.CopyTo(vouchNonce.Slice(8));
#endif

            using var box = new Curve25519XSalsa20Poly1305(m_secretKey, m_cnServerKey);
            box.Encrypt(vouchBox, vouchPlaintext, vouchNonce);

            Span<byte> initiate = msg;
            Span<byte> initiateNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            Span<byte> initiatePlaintext = new byte[128 + metadataLength];
            Span<byte> initiateBox = initiate.Slice(113);

            //  Create Box [C + vouch + metadata](C'->S')
            m_publicKey.CopyTo(initiatePlaintext);
            vouchNonce.Slice(8).CopyTo(initiatePlaintext.Slice(32));
            vouchBox.CopyTo(initiatePlaintext.Slice(48));
            metadataPlaintext.CopyTo(initiatePlaintext.Slice(48 + 80));
            Array.Clear(metadataPlaintext, 0, metadataPlaintext.Length);

            InitiatieNoncePrefix.CopyTo(initiateNonce);
            NetworkOrderBitsConverter.PutUInt64(m_nonce, initiateNonce.Slice(16));

            using var box2 = new Curve25519XSalsa20Poly1305(m_cnSecretKey, m_cnServerKey);
            box2.Encrypt(initiateBox, initiatePlaintext, initiateNonce);
            
            InitiateLiteral.CopyTo(initiate);

            //  Cookie provided by the server in the WELCOME command
            m_cnCookie.CopyTo(initiate.Slice(9, 96));

            //  Short nonce, prefixed by "CurveZMQINITIATE"
            initiateNonce.Slice(16).CopyTo(initiate.Slice(105));

            m_nonce++;

            return PullMsgResult.Ok;
        }

        PushMsgResult ProcessReady(ref Msg msg)
        {
            if (msg.Size < 30)
                return PushMsgResult.Error;

            Span<byte> readyNonce = stackalloc byte[Curve25519XSalsa20Poly1305.NonceLength];
            var readyPlaintext = new byte[msg.Size - 14 - Curve25519XSalsa20Poly1305.TagLength];
            Span<byte> readyBox = msg.Slice(14);

            ReadyNoncePrefix.CopyTo(readyNonce);
            msg.Slice(6, 8).CopyTo(readyNonce.Slice(16));
            m_peerNonce = NetworkOrderBitsConverter.ToUInt64(msg, 6);

            Assumes.NotNull(m_box);

            bool isDecrypted = m_box.TryDecrypt(readyPlaintext, readyBox, readyNonce);
            if (!isDecrypted)
                return PushMsgResult.Error;

            if (!ParseMetadata(readyPlaintext))
                return PushMsgResult.Error;
            
            Array.Clear(readyPlaintext, 0, readyPlaintext.Length);

            m_state = State.Connected;
            return PushMsgResult.Ok;
        }
        
        PushMsgResult ProcessError(ref Msg msg)
        {
            if (m_state != State.ExpectWelcome && m_state != State.ExpectReady)
                return PushMsgResult.Error;
            
            if (msg.Size < 7) 
                return PushMsgResult.Error;;

            int errorReasonLength = msg[6];
            if (errorReasonLength > msg.Size - 7)
                return PushMsgResult.Error;

            string errorReason = msg.GetString(Encoding.ASCII, 7, errorReasonLength);
            m_state = State.ErrorReceived;
            return PushMsgResult.Ok;
        }
    }
}