using System;
using System.Text;
using NetMQ.Core.Utils;

namespace NetMQ.Core.Mechanisms
{
    internal class NullMechanism : Mechanism
    {
        const string ReadyCommandName = "READY";
        private const string ErrorCommandName = "ERROR";
        private const int ErrorReasonLengthSize = 1;

        bool m_readyCommandSent;
        bool m_readyCommandReceived;
        bool m_errorCommandReceived;

        public NullMechanism(SessionBase session, Options options) : base(session, options)
        {
        }

        public override void Dispose()
        {
        }

        public override MechanismStatus Status
        {
            get
            {
                if (m_readyCommandSent && m_readyCommandReceived)
                    return MechanismStatus.Ready;

                bool commandSent = m_readyCommandSent;
                bool commandReceived = m_readyCommandReceived || m_errorCommandReceived;
                return commandSent && commandReceived ? MechanismStatus.Error : MechanismStatus.Handshaking;
            }
        }

        PushMsgResult ProcessReadyCommand(Span<byte> commandData)
        {
            m_readyCommandReceived = true;
            if (!ParseMetadata(commandData.Slice(ReadyCommandName.Length + 1)))
                return PushMsgResult.Error;

            return PushMsgResult.Ok;
        }

        PushMsgResult ProcessErrorCommand(Span<byte> commandData)
        {
            int fixedPrefixSize = ErrorCommandName.Length + 1 + ErrorReasonLengthSize;
            if (commandData.Length < fixedPrefixSize)
                return PushMsgResult.Error;

            int errorReasonLength = commandData[ErrorCommandName.Length + 1];
            if (errorReasonLength > commandData.Length - fixedPrefixSize)
                return PushMsgResult.Error;

            string errorReason = SpanUtility.ToAscii(commandData.Slice(fixedPrefixSize, errorReasonLength));

            // TODO: handle error, nothing todo at the moment as monitoring and zap are not yet implemented

            m_errorCommandReceived = true;
            return PushMsgResult.Ok;
        }

        public override PullMsgResult NextHandshakeCommand(ref Msg msg)
        {
            if (m_readyCommandSent)
                return PullMsgResult.Empty;

            MakeCommandWithBasicProperties(ref msg, ReadyCommandName);
            m_readyCommandSent = true;

            return PullMsgResult.Ok;
        }

        public override PushMsgResult ProcessHandshakeCommand(ref Msg msg)
        {
            if (m_readyCommandReceived || m_errorCommandReceived)
                return PushMsgResult.Error;

            PushMsgResult result;
            if (IsCommand(ReadyCommandName, ref msg))
                result = ProcessReadyCommand(msg);
            else if (IsCommand(ErrorCommandName, ref msg))
                result = ProcessErrorCommand(msg);
            else
                return PushMsgResult.Error;

            if (result == PushMsgResult.Ok)
            {
                msg.Close();
                msg.InitEmpty();
            }

            return result;
        }
    }
}