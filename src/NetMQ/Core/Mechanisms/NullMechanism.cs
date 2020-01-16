using System.Text;

namespace NetMQ.Core.Mechanisms
{
    internal class NullMechanism : Mechanism
    {
        const string ReadyCommandName = "READY";
        private const string ErrorCommandName = "ERROR";
        private const int ErrorReasonLengthSize = 1;
        
        bool m_readyCommandSent;
        bool m_errorCommandSent;
        bool m_readyCommandReceived;
        bool m_errorCommandReceived;

        public NullMechanism(SessionBase session, Options options) : base(session, options)
        {
            
        }

        public override MechanismState State
        {
            get
            {
                if (m_readyCommandSent && m_readyCommandReceived)
                    return MechanismState.Ready;

                bool commandSent = m_readyCommandSent || m_errorCommandSent;
                bool commandReceived = m_readyCommandReceived || m_errorCommandReceived;
                return commandSent && commandReceived ? MechanismState.Error : MechanismState.Handshaking;
            }
        }

        PushMsgResult ProcessReadyCommand(byte[] commandData, int offset, int count)
        {
            m_readyCommandReceived = true;
            if (!ParseMetadata(commandData, offset + ReadyCommandName.Length + 1, count - ReadyCommandName.Length - 1))
                return PushMsgResult.Error;

            return PushMsgResult.Ok;
        }
        
        PushMsgResult ProcessErrorCommand(byte[] commandData, int offset, int count)
        {
            int fixedPrefixSize = ErrorCommandName.Length + 1 + ErrorReasonLengthSize;
            if (count < fixedPrefixSize)
                return PushMsgResult.Error;

            int errorReasonLength = commandData[offset + ErrorCommandName.Length + 1];
            if (errorReasonLength > count - fixedPrefixSize)
                return PushMsgResult.Error;

            string errorReason = Encoding.ASCII.GetString(commandData, offset + fixedPrefixSize, errorReasonLength);
            
            // TODO: handle error, nothing todo at the moment as monitoring and zap are not yet implemented
            
            m_errorCommandReceived = true;
            return PushMsgResult.Ok;
        }

        public override PullMsgResult NextHandshakeCommand(ref Msg msg)
        {
            if (m_readyCommandSent || m_errorCommandSent)
                return PullMsgResult.Empty;

            MakeCommandWithBasicProperties(ref msg, ReadyCommandName);
            m_readyCommandSent = true;

            return PullMsgResult.Ok;
        }
        
        bool IsCommand(string command, ref Msg msg)
        {
            if (msg.Size >= ReadyCommandName.Length + 1)
            {
                string msgCommand = Encoding.ASCII.GetString(msg.Data, msg.Offset + 1, msg[0]);
                return msgCommand == command;
            }

            return false;
        }
        
        public override PushMsgResult ProcessHandshakeCommand(ref Msg msg)
        {
            if (m_readyCommandReceived || m_errorCommandReceived) 
                return PushMsgResult.Error;
            
            PushMsgResult result;
            if (IsCommand(ReadyCommandName, ref msg))
                result = ProcessReadyCommand(msg.Data, msg.Offset, msg.Size);
            else if (IsCommand(ErrorCommandName, ref msg))
                result = ProcessErrorCommand(msg.Data, msg.Offset, msg.Size);
            else
                return PushMsgResult.Error;

            if (result == PushMsgResult.Ok) {
                msg.Close();
                msg.InitEmpty();
            }
            
            return result;
        }
    }
}