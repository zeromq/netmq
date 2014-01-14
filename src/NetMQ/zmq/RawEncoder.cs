using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NetMQ.zmq
{
    public class RawEncoder : EncoderBase
    {
        IMsgSource m_msgSource;
        Msg m_inProgress;

        private const int RawMessageSizeReadyState = 1;
        private const int RawMessageReadyState = 2;


        public RawEncoder(int bufferSize, IMsgSource msgSource, Endianness endianness) :
            base(bufferSize, endianness)
        {
            m_msgSource = msgSource;

            NextStep(null, 0, RawMessageReadyState, true);
        }

        public override void SetMsgSource(IMsgSource msgSource)
        {
            m_msgSource = msgSource;
        }

        protected override bool Next()
        {
            switch (State)
            {
                case RawMessageSizeReadyState:
                    return RawMessageSizeReady();
                case RawMessageReadyState:
                    return RawMessageReady();
                default:
                    return false;
                    break;
            }
        }

        bool RawMessageSizeReady()
        {
            //  Write message body into the buffer.
            NextStep(m_inProgress.Data, m_inProgress.Size,
                RawMessageReadyState, !m_inProgress.Flags.HasFlag(MsgFlags.More));
            return true;
        }

        bool RawMessageReady()
        {
            //  Destroy content of the old message.
            m_inProgress = null;

            //  Read new message. If there is none, return false.
            //  Note that new state is set only if write is successful. That way
            //  unsuccessful write will cause retry on the next state machine
            //  invocation.
            if (m_msgSource == null)
            {
                return false;
            }

            m_inProgress = m_msgSource.PullMsg();

            if (m_inProgress == null)
                return false;

            m_inProgress.ResetFlags(MsgFlags.Shared | MsgFlags.More | MsgFlags.Identity);

            NextStep(null, 0, RawMessageSizeReadyState, true);

            return true;
        }
    }
}
