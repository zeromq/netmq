/*
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2009 iMatix Corporation
    Copyright (c) 2007-2015 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AsyncIO;
using JetBrains.Annotations;
using NetMQ.Core.Mechanisms;
using NetMQ.Core.Patterns;
using NetMQ.Core.Utils;

namespace NetMQ.Core.Transports
{
    delegate PullMsgResult NextMsgDelegate (ref Msg msg);
    delegate PushMsgResult ProcessMsgDelegate(ref Msg msg);
    
    internal sealed class StreamEngine : IEngine, IProactorEvents
    {
        const int HeartbeatIntervalTimerId = 1;
        const int HeartbeatTimeoutTimerId = 2;
        const int HeartbeatTtlTimerId = 3;
        
        private readonly byte[] NullMechanismBytes = new byte[20]
        {
            (byte) 'N', (byte) 'U', (byte) 'L', (byte) 'L', 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };
        
        private readonly byte[] PlainMechanismBytes = new byte[20]
        {
            (byte) 'P', (byte) 'L', (byte) 'A', (byte) 'I', (byte) 'N', 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };
        
        private readonly byte[] CurveMechanismBytes = new byte[20]
        {
            (byte) 'C', (byte) 'U', (byte) 'R', (byte) 'V', (byte) 'E', 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };
        
        private class StateMachineAction
        {
            public StateMachineAction(Action action, SocketError socketError, int bytesTransferred)
            {
                Action = action;
                SocketError = socketError;
                BytesTransferred = bytesTransferred;
            }

            public Action Action { get; }

            public SocketError SocketError { get; }

            public int BytesTransferred { get; }
        }

        /// <summary>
        /// This enum-type denotes the operational state of this StreamEngine - whether Closed, doing Handshaking, Active, or Stalled.
        /// </summary>
        private enum State
        {
            Closed,
            Handshaking,
            Active,
            Error
        }

        private enum HandshakeState
        {
            Closed,
            SendingGreeting,
            ReceivingGreeting,
            SendingMajorVersion,
            SendingSocketType,
            ReceivingMajorVersion,
            ReceivingResfOfV2Greeting,
            SendingV3Greeting,
            ReceiveV3Greeting
        }

        private enum ReceiveState
        {
            Idle,
            Active,
            Stuck,
        }

        private enum SendState
        {
            Idle,
            Active,
            Error
        }

        private enum Action
        {
            Start,
            InCompleted,
            OutCompleted,
            ActivateOut,
            ActivateIn
        }

        // Size of the greeting message:
        // Preamble (10 bytes) + version (1 byte) + socket type (1 byte).
        private const int GreetingSize = 12;
        private const int PreambleSize = 10;
        private const int GreetingSizeV3 = 64;

        // Position of the version field in the greeting.
        private const int VersionPos = 10;

        //private IOObject io_object;
        private AsyncSocket m_handle;

        private ByteArraySegment m_inpos;
        private int m_insize;
        private DecoderBase m_decoder;
        private bool m_subscriptionRequired;

        private ByteArraySegment m_outpos;
        private int m_outsize;
        private EncoderBase m_encoder;

        // The receive buffer holding the greeting message
        // that we are receiving from the peer.
        private readonly byte[] m_greeting = new byte[64];

        // The number of bytes of the greeting message that
        // we have already received.

        private int m_greetingBytesRead;

        // The send buffer holding the greeting message
        // that we are sending to the peer.
        private readonly ByteArraySegment m_greetingOutputBuffer = new byte[64];

        // The session this engine is attached to.
        private SessionBase m_session;

        // Detached transient session.
        //private SessionBase leftover_session;

        private readonly Options m_options;

        // string representation of endpoint
        private readonly string m_endpoint;

        private bool m_plugged;

        // Socket
        private SocketBase m_socket;

        private IOObject m_ioObject;

        private SendState m_sendingState;
        private ReceiveState m_receivingState;

        private State m_state;
        private HandshakeState m_handshakeState;
        
        private NextMsgDelegate m_nextMsg;
        private ProcessMsgDelegate m_processMsg;
        
        private Mechanism m_mechanism;
        private Msg m_pongMsg;
        
        // queue for actions that happen during the state machine
        private readonly Queue<StateMachineAction> m_actionsQueue;

        private bool m_hasHeartbeatTimer;
        private bool m_hasTtlTimer;
        private bool m_hasTimeoutTimer;
        private int m_heartbeatTimeout;

        public StreamEngine(AsyncSocket handle, Options options, string endpoint)
        {
            m_handle = handle;
            m_insize = 0;
            m_sendingState = SendState.Idle;
            m_receivingState = ReceiveState.Idle;
            m_outsize = 0;
            m_session = null;
            m_options = options;
            m_plugged = false;
            m_endpoint = endpoint;
            m_socket = null;
            m_encoder = null;
            m_decoder = null;
            m_mechanism = null;
            m_actionsQueue = new Queue<StateMachineAction>();
            m_subscriptionRequired = false;
            
            m_nextMsg = RoutingIdMsg;
            m_processMsg = ProcessRoutingIdMsg;
            m_pongMsg = new Msg();
            
            m_hasHeartbeatTimer = false;
            m_hasTtlTimer = false;
            m_hasTimeoutTimer = false;
            
            if (m_options.HeartbeatInterval > 0) {
                m_heartbeatTimeout = m_options.HeartbeatTimeout;
                if (m_heartbeatTimeout == -1)
                    m_heartbeatTimeout = m_options.HeartbeatInterval;
            }
            
            // Set the socket buffer limits for the underlying socket.
            if (m_options.SendBuffer != 0)
            {
                m_handle.SendBufferSize = m_options.SendBuffer;
            }
            if (m_options.ReceiveBuffer != 0)
            {
                m_handle.ReceiveBufferSize = m_options.ReceiveBuffer;
            }
        }

        public void Destroy()
        {
            Debug.Assert(!m_plugged);

            m_mechanism?.Dispose();
            m_encoder?.Dispose();

            if (m_handle != null)
            {
                try
                {
                    m_handle.Dispose();
                }
                catch (SocketException)
                {}
                m_handle = null;
            }
        }

        public void Plug(IOThread ioThread, SessionBase session)
        {
            Debug.Assert(!m_plugged);
            m_plugged = true;

            // Connect to session object.
            Debug.Assert(m_session == null);
            Debug.Assert(session != null);
            m_session = session;
            m_socket = m_session.Socket;

            m_ioObject = new IOObject(null);
            m_ioObject.SetHandler(this);

            // Connect to I/O threads poller object.
            m_ioObject.Plug(ioThread);
            m_ioObject.AddSocket(m_handle);

            FeedAction(Action.Start, SocketError.Success, 0);
        }

        public void Terminate()
        {
            Unplug();
            Destroy();
        }

        private void Unplug()
        {
            Debug.Assert(m_plugged);
            m_plugged = false;
            
            if (m_hasTtlTimer) {
                m_ioObject.CancelTimer(HeartbeatTtlTimerId);
                m_hasTtlTimer = false;
            }

            if (m_hasTimeoutTimer) {
                m_ioObject.CancelTimer(HeartbeatTimeoutTimerId);
                m_hasTimeoutTimer = false;
            }

            if (m_hasHeartbeatTimer) {
                m_ioObject.CancelTimer(HeartbeatIntervalTimerId);
                m_hasHeartbeatTimer = false;
            }

            // remove handle from proactor.
            m_ioObject.RemoveSocket(m_handle);
            
            // Disconnect from I/O threads poller object.
            m_ioObject.Unplug();

            m_state = State.Closed;

            // Disconnect from session object.
            m_session = null;
        }

        private void Error()
        {
            Debug.Assert(m_session != null);
            m_state = State.Error;
            m_socket.EventDisconnected(m_endpoint, m_handle);
            m_session.Flush();
            m_session.Detach();
            Unplug();
            Destroy();
        }

        private void FeedAction(Action action, SocketError socketError, int bytesTransferred)
        {
            Handle(action, socketError, bytesTransferred);

            while (m_actionsQueue.Count > 0)
            {
                var stateMachineAction = m_actionsQueue.Dequeue();
                Handle(stateMachineAction.Action, stateMachineAction.SocketError, stateMachineAction.BytesTransferred);
            }
        }

        private void EnqueueAction(Action action, SocketError socketError, int bytesTransferred)
        {
            m_actionsQueue.Enqueue(new StateMachineAction(action, socketError, bytesTransferred));
        }

        private void Handle(Action action, SocketError socketError, int bytesTransferred)
        {
            switch (m_state)
            {
                case State.Error:
                    break;
                case State.Closed:
                    switch (action)
                    {
                        case Action.Start:
                            if (m_options.RawSocket)
                            {
                                m_encoder = new RawEncoder(Config.OutBatchSize, m_options.Endian);
                                m_decoder = new RawDecoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);
                                m_nextMsg = m_session.PullMsg;
                                m_processMsg = m_session.PushMsg;
                                    
                                Activate();
                            }
                            else
                            {
                                m_state = State.Handshaking;
                                m_handshakeState = HandshakeState.Closed;
                                HandleHandshake(action, socketError, bytesTransferred);
                            }

                            break;
                    }
                    break;
                case State.Handshaking:
                    HandleHandshake(action, socketError, bytesTransferred);
                    break;
                case State.Active:
                    switch (action)
                    {
                        case Action.InCompleted:
                            m_insize = EndRead(socketError, bytesTransferred);

                            ProcessInput();
                            break;
                        case Action.ActivateIn:

                            // if we stuck let's continue, other than that nothing to do
                            if (m_receivingState == ReceiveState.Stuck)
                            {
                                var pushResult = m_decoder.PushMsg(m_processMsg);
                                if (pushResult == PushMsgResult.Ok)
                                {
                                    m_receivingState = ReceiveState.Active;
                                    m_session.Flush();
                                    ProcessInput();    
                                }
                            }
                            break;
                        case Action.OutCompleted:
                            int bytesSent = EndWrite(socketError, bytesTransferred);

                            // IO error has occurred. We stop waiting for output events.
                            // The engine is not terminated until we detect input error;
                            // this is necessary to prevent losing incoming messages.
                            if (bytesSent == -1)
                            {
                                m_sendingState = SendState.Error;
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                BeginSending();
                            }
                            break;
                        case Action.ActivateOut:
                            // if we idle we start sending, other than do nothing
                            if (m_sendingState == SendState.Idle)
                            {
                                m_sendingState = SendState.Active;
                                BeginSending();
                            }
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
            }
        }

        private void BeginSending()
        {
            if (m_outsize == 0)
            {
                m_outpos = null;
                m_outsize = m_encoder.Encode(ref m_outpos, 0);
                
                while (m_outsize < Config.OutBatchSize)
                {
                    Msg msg = new Msg();
                    if (m_nextMsg(ref msg) != PullMsgResult.Ok)
                        break;
                    m_encoder.LoadMsg(ref msg);
                    ByteArraySegment buffer = null;
                    if (m_outpos != null)
                        buffer = m_outpos + m_outsize;
                    var n = m_encoder.Encode(ref buffer, Config.OutBatchSize - m_outsize);
                    if (m_outpos == null)
                        m_outpos = buffer;
                    m_outsize += n;
                }
                
                if (m_outsize == 0)
                {
                    m_sendingState = SendState.Idle;
                }
                else
                {
                    BeginWrite(m_outpos, m_outsize);
                }
            }
            else
            {
                BeginWrite(m_outpos, m_outsize);
            }
        }

        private void HandleHandshake(Action action, SocketError socketError, int bytesTransferred)
        {
            int bytesSent;
            int bytesReceived;

            switch (m_handshakeState)
            {
                case HandshakeState.Closed:
                    switch (action)
                    {
                        case Action.Start:
                            // Send the 'length' and 'flags' fields of the identity message.
                            // The 'length' field is encoded in the long format.

                            m_greetingOutputBuffer[m_outsize++] = 0xff;
                            m_greetingOutputBuffer.PutLong(m_options.Endian, (long)m_options.IdentitySize + 1, 1);
                            m_outsize += 8;
                            m_greetingOutputBuffer[m_outsize++] = 0x7f;

                            m_outpos = new ByteArraySegment(m_greetingOutputBuffer);

                            m_handshakeState = HandshakeState.SendingGreeting;

                            BeginWrite(m_outpos, m_outsize);
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingGreeting:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    m_greetingBytesRead = 0;

                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);

                                    m_handshakeState = HandshakeState.ReceivingGreeting;

                                    BeginRead(greetingSegment, PreambleSize);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceivingGreeting:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                // check if it is an unversioned protocol
                                if (m_greeting[0] != 0xff || (m_greetingBytesRead == 10 && (m_greeting[9] & 0x01) == 0))
                                {
                                    m_encoder = new V1Encoder(Config.OutBatchSize, m_options.Endian);
                                    m_decoder = new V1Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);

                                    // We have already sent the message header.
                                    // Since there is no way to tell the encoder to
                                    // skip the message header, we simply throw that
                                    // header data away.
                                    int headerSize = m_options.IdentitySize + 1 >= 255 ? 10 : 2;
                                    var tmp = new byte[10];
                                    var bufferp = new ByteArraySegment(tmp);

                                    int bufferSize = m_encoder.Encode(ref bufferp, headerSize);
                                    Debug.Assert(bufferSize == headerSize);
                                    
                                    // Make sure the decoder sees the data we have already received.
                                    m_inpos = new ByteArraySegment(m_greeting);
                                    m_insize = m_greetingBytesRead;

                                    // To allow for interoperability with peers that do not forward
                                    // their subscriptions, we inject a phony subscription
                                    // message into the incoming message stream. To put this
                                    // message right after the identity message, we temporarily
                                    // divert the message stream from session to ourselves.
                                    if (m_options.SocketType == ZmqSocketType.Pub ||
                                        m_options.SocketType == ZmqSocketType.Xpub)
                                        m_subscriptionRequired = true;

                                    // handshake is done
                                    Activate();
                                }
                                else if (m_greetingBytesRead < PreambleSize)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, PreambleSize - m_greetingBytesRead);
                                }
                                else
                                {
                                    // The peer is using versioned protocol.
                                    // Send the rest of the greeting.
                                    m_outpos[m_outsize++] = 3; // Protocol version
                                    m_handshakeState = HandshakeState.SendingMajorVersion;
                                    
                                    BeginWrite(m_outpos, m_outsize);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingMajorVersion:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);

                                    m_handshakeState = HandshakeState.ReceivingMajorVersion;
                                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceivingMajorVersion:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                if (m_greetingBytesRead <= VersionPos)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                }
                                else if (m_greeting[VersionPos] == 0 || m_greeting[VersionPos] == 1)
                                {
                                    m_outpos[m_outsize++] = (byte)m_options.SocketType;
                                    m_handshakeState = HandshakeState.SendingSocketType;
                                    
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    m_outpos[m_outsize++] = 0; // Minor version
                                            
                                    switch (m_options.Mechanism)
                                    {
                                        case MechanismType.Null:
                                            m_outpos.PutBytes(NullMechanismBytes, m_outsize);
                                            m_outsize += 20;
                                            break;
                                        case MechanismType.Plain:
                                            m_outpos.PutBytes(PlainMechanismBytes, m_outsize);
                                            m_outsize += 20;
                                            break;
                                        case MechanismType.Curve:
                                            m_outpos.PutBytes(CurveMechanismBytes, m_outsize);
                                            m_outsize += 20;
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                    
                                    m_outpos.Fill(0, m_outsize, 32);
                                    m_outsize += 32;
                                            
                                    m_handshakeState = HandshakeState.SendingV3Greeting;
                                    BeginWrite(m_outpos, m_outsize);
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingSocketType:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    if (m_greetingBytesRead < GreetingSize)
                                    {
                                        var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                        BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                        m_handshakeState = HandshakeState.ReceivingResfOfV2Greeting;
                                    }
                                    else
                                    {
                                        if (m_greeting[VersionPos] == 0)
                                        {
                                            m_encoder = new V1Encoder(Config.OutBatchSize, m_options.Endian);
                                            m_decoder = new V1Decoder(Config.InBatchSize, m_options.MaxMessageSize,
                                                m_options.Endian);
                                            
                                            Activate ();
                                        }
                                        else if (m_greeting[VersionPos] == 1)
                                        {
                                            m_encoder = new V2Encoder(Config.OutBatchSize, m_options.Endian);
                                            m_decoder = new V2Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);

                                            Activate ();
                                        }
                                        else
                                        {
                                            throw new Exception("No socket type for ZMTP 3");
                                        }
                                    }
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceivingResfOfV2Greeting:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                if (m_greetingBytesRead < GreetingSize)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, GreetingSize - m_greetingBytesRead);
                                }
                                else
                                {
                                    if (m_greeting[VersionPos] == 0)
                                    {
                                        m_encoder = new V1Encoder(Config.OutBatchSize, m_options.Endian);
                                        m_decoder = new V1Decoder(Config.InBatchSize, m_options.MaxMessageSize,
                                            m_options.Endian);
                                            
                                        Activate ();
                                    }
                                    else if (m_greeting[VersionPos] == 1)
                                    {
                                        m_encoder = new V2Encoder(Config.OutBatchSize, m_options.Endian);
                                        m_decoder = new V2Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);

                                        Activate ();
                                    }
                                    else
                                    {    
                                        throw new Exception("No V2Greeting for ZMTP 3");
                                    }
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.SendingV3Greeting:
                    switch (action)
                    {
                        case Action.OutCompleted:
                            bytesSent = EndWrite(socketError, bytesTransferred);

                            if (bytesSent == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_outpos.AdvanceOffset(bytesSent);
                                m_outsize -= bytesSent;

                                if (m_outsize > 0)
                                {
                                    BeginWrite(m_outpos, m_outsize);
                                }
                                else
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, GreetingSizeV3 - m_greetingBytesRead);
                                    m_handshakeState = HandshakeState.ReceiveV3Greeting;
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                case HandshakeState.ReceiveV3Greeting:
                    switch (action)
                    {
                        case Action.InCompleted:
                            bytesReceived = EndRead(socketError, bytesTransferred);

                            if (bytesReceived == -1)
                            {
                                Error();
                            }
                            else
                            {
                                m_greetingBytesRead += bytesReceived;

                                if (m_greetingBytesRead < GreetingSizeV3)
                                {
                                    var greetingSegment = new ByteArraySegment(m_greeting, m_greetingBytesRead);
                                    BeginRead(greetingSegment, GreetingSizeV3 - m_greetingBytesRead);
                                }
                                else
                                {
                                    m_encoder = new V2Encoder(Config.OutBatchSize, m_options.Endian);
                                    m_decoder = new V2Decoder(Config.InBatchSize, m_options.MaxMessageSize, m_options.Endian);
                                    
                                    if (m_options.Mechanism == MechanismType.Null
                                        && ByteArrayUtility.AreEqual(m_greeting, 12, NullMechanismBytes, 0, 20))
                                        m_mechanism = new NullMechanism(m_session, m_options);
                                    else if (m_options.Mechanism == MechanismType.Plain
                                             && ByteArrayUtility.AreEqual(m_greeting, 12, PlainMechanismBytes, 0, 20))
                                    {
                                        Error(); // Not yet supported
                                        return;
                                    }
                                    else if (m_options.Mechanism == MechanismType.Curve
                                             && ByteArrayUtility.AreEqual(m_greeting, 12, CurveMechanismBytes, 0, 20))
                                    {
                                        if (m_options.AsServer)
                                            m_mechanism = new CurveServerMechanism(m_session, m_options);
                                        else
                                            m_mechanism = new CurveClientMechanism(m_session, m_options);
                                    }
                                    else {
                                        // Unsupported mechanism
                                        Error();
                                        return;
                                    }
                                    
                                    m_nextMsg = NextHandshakeCommand;
                                    m_processMsg = ProcessHandshakeCommand;
                                    
                                    Activate ();
                                }
                            }
                            break;
                        case Action.ActivateIn:
                        case Action.ActivateOut:
                            // nothing to do
                            break;
                        default:
                            Debug.Assert(false);
                            break;
                    }
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void Activate()
        {
            // Handshaking was successful.
            // Switch into the normal message flow.
            m_state = State.Active;

            m_outsize = 0;

            m_sendingState = SendState.Active;
            BeginSending();

            m_receivingState = ReceiveState.Active;

            if (m_insize == 0)
            {
                m_decoder.GetBuffer(out m_inpos, out m_insize);
                BeginRead(m_inpos, m_insize);
            }
            else
            {
                ProcessInput();
            }
        }

        private void ProcessInput()
        {
            if (m_insize == -1)
            {
                m_insize = 0;
                Error();
                return; 
            }
            
            while (m_insize > 0)
            {
                var result = m_decoder.Decode(m_inpos, m_insize, out var processed);
                m_inpos.AdvanceOffset(processed);
                m_insize -= processed;

                if (result == DecodeResult.Error)
                {
                    Error();
                    return;
                }
                
                if (result == DecodeResult.Processing)
                    break;

                var pushResult = m_decoder.PushMsg(m_processMsg);
                if (pushResult == PushMsgResult.Full)
                {
                    m_receivingState = ReceiveState.Stuck;
                    m_session.Flush();
                    return;
                }
                else if (pushResult == PushMsgResult.Error)
                {
                    Error();
                    return;
                }
            }

            m_session.Flush();
            m_decoder.GetBuffer(out m_inpos, out m_insize);
            BeginRead(m_inpos, m_insize);
        }

        /// <summary>
        /// This method is be called when a message receive operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void InCompleted(SocketError socketError, int bytesTransferred)
        {
            FeedAction(Action.InCompleted, socketError, bytesTransferred);
        }

        public void ActivateIn()
        {
            FeedAction(Action.ActivateIn, SocketError.Success, 0);
        }

        /// <summary>
        /// This method is called when a message Send operation has been completed.
        /// </summary>
        /// <param name="socketError">a SocketError value that indicates whether Success or an error occurred</param>
        /// <param name="bytesTransferred">the number of bytes that were transferred</param>
        public void OutCompleted(SocketError socketError, int bytesTransferred)
        {
            FeedAction(Action.OutCompleted, socketError, bytesTransferred);
        }

        public void ActivateOut()
        {
            FeedAction(Action.ActivateOut, SocketError.Success, 0);
        }

        /// <param name="socketError">the SocketError that resulted from the write - which could be Success (no error at all)</param>
        /// <param name="bytesTransferred">this indicates the number of bytes that were transferred in the write</param>
        /// <returns>the number of bytes transferred if successful, -1 otherwise</returns>
        /// <exception cref="NetMQException">If the socketError is not Success then it must be a valid recoverable error or the number of bytes transferred must be zero.</exception>
        /// <remarks>
        /// If socketError is SocketError.Success and bytesTransferred is > 0, then this returns bytesTransferred.
        /// If bytes is zero, or the socketError is one of NetworkDown, NetworkReset, HostUn, Connection Aborted, TimedOut, ConnectionReset, AccessDenied, or Shutdown, - then -1 is returned.
        /// Otherwise, a NetMQException is thrown.
        /// </remarks>
        private static int EndWrite(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
                return bytesTransferred;

            if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset ||
                socketError == SocketError.AccessDenied || 
                socketError == SocketError.Shutdown)
                return -1;

            throw NetMQException.Create(socketError);
        }

        private void BeginWrite([NotNull] ByteArraySegment data, int size)
        {
            try
            {
                m_handle.Send((byte[])data, data.Offset, size, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                EnqueueAction(Action.OutCompleted, ex.SocketErrorCode, 0);
            }
        }

        /// <param name="socketError">the SocketError that resulted from the read - which could be Success (no error at all)</param>
        /// <param name="bytesTransferred">this indicates the number of bytes that were transferred in the read</param>
        /// <returns>the number of bytes transferred if successful, -1 otherwise</returns>
        /// <exception cref="NetMQException">If the socketError is not Success then it must be a valid recoverable error or the number of bytes transferred must be zero.</exception>
        /// <remarks>
        /// If socketError is SocketError.Success and bytesTransferred is > 0, then this returns bytesTransferred.
        /// If bytes is zero, or the socketError is one of NetworkDown, NetworkReset, HostUn, Connection Aborted, TimedOut, ConnectionReset, AccessDenied, or Shutdown, - then -1 is returned.
        /// Otherwise, a NetMQException is thrown.
        /// </remarks>
        private static int EndRead(SocketError socketError, int bytesTransferred)
        {
            if (socketError == SocketError.Success && bytesTransferred > 0)
                return bytesTransferred;

            if (bytesTransferred == 0 ||
                socketError == SocketError.NetworkDown ||
                socketError == SocketError.NetworkReset ||
                socketError == SocketError.HostUnreachable ||
                socketError == SocketError.ConnectionAborted ||
                socketError == SocketError.TimedOut ||
                socketError == SocketError.ConnectionReset ||
                socketError == SocketError.AccessDenied ||
                socketError == SocketError.Shutdown)
                return -1;

            throw NetMQException.Create(socketError);
        }

        private void BeginRead([NotNull] ByteArraySegment data, int size)
        {
            try
            {
                m_handle.Receive((byte[])data, data.Offset, size, SocketFlags.None);
            }
            catch (SocketException ex)
            {
                EnqueueAction(Action.InCompleted, ex.SocketErrorCode, 0);
            }
        }

        PullMsgResult RoutingIdMsg (ref Msg msg)
        {
            if (m_options.IdentitySize == 0)
                msg.InitEmpty();
            else
            {
                msg.InitPool(m_options.IdentitySize);
                msg.Put(m_options.Identity, 0, m_options.IdentitySize);
            }
            
            m_nextMsg = m_session.PullMsg;
            return PullMsgResult.Ok;
        }

        PushMsgResult ProcessRoutingIdMsg (ref Msg msg)
        {
            if (m_options.RecvIdentity) 
            {
                msg.SetFlags(MsgFlags.Identity);
                m_session.PushMsg(ref msg);
            } 
            else 
            {
                msg.Close();
                msg.InitEmpty();
            }

            if (m_subscriptionRequired)
            {
                Msg subscription = new Msg();
                subscription.InitPool(1);
                subscription.Put((byte)1);
                
                m_session.PushMsg(ref subscription);
            }
            
            m_processMsg = m_session.PushMsg;
            return PushMsgResult.Ok;
        }

        PullMsgResult NextHandshakeCommand (ref Msg msg)
        {
            if (m_mechanism.Status == MechanismStatus.Ready) 
            {
                MechanismReady();
                return PullAndEncode (ref msg);
            }
            else if (m_mechanism.Status == MechanismStatus.Error) 
            {
                return PullMsgResult.Error;
            } 
            else 
            {
                var result = m_mechanism.NextHandshakeCommand(ref msg);

                if (result == PullMsgResult.Ok)
                    msg.SetFlags(MsgFlags.Command);

                return result;
            }
        }

        PushMsgResult ProcessHandshakeCommand (ref Msg msg)
        {
            var result = m_mechanism.ProcessHandshakeCommand(ref msg);
            if (result == PushMsgResult.Ok) 
            {
                if (m_mechanism.Status == MechanismStatus.Ready)
                    MechanismReady();
                else if (m_mechanism.Status == MechanismStatus.Error)
                    return PushMsgResult.Error;
                
                if (m_sendingState == SendState.Idle)
                {
                    m_sendingState = SendState.Active;
                    BeginSending();
                }
            }

            return result;
        }
        
        void MechanismReady ()
        {
            if (m_options.HeartbeatInterval > 0)
            {
                m_ioObject.AddTimer(m_options.HeartbeatInterval, HeartbeatIntervalTimerId);
                m_hasHeartbeatTimer = true;
            }
            
            if (m_options.RecvIdentity) {
                Msg identity = new Msg();
                identity.InitPool(m_mechanism.PeerIdentity.Length);
                identity.Put(m_mechanism.PeerIdentity, 0, m_mechanism.PeerIdentity.Length);
                var pushResult = m_session.PushMsg(ref identity);
                if (pushResult == PushMsgResult.Full) {
                    // If the write is failing at this stage with
                    // an EAGAIN the pipe must be being shut down,
                    // so we can just bail out of the routing id set.
                    return;
                }
                
                m_session.Flush();
            }

            m_nextMsg = PullAndEncode;
            m_processMsg = DecodeAndPush;
        }
        
        PullMsgResult PullAndEncode (ref Msg msg)
        {
            var result = m_session.PullMsg(ref msg); 
            if (result != PullMsgResult.Ok)
                return result;

            return m_mechanism.Encode(ref msg);
        }

        PushMsgResult DecodeAndPush (ref Msg msg)
        {
            var result = m_mechanism.Decode(ref msg);
            if (result != PushMsgResult.Ok)
                return result;
            
            if (m_hasTimeoutTimer) {
                m_hasTimeoutTimer = false;
                m_ioObject.CancelTimer(HeartbeatTimeoutTimerId);
            }
            
            if (m_hasTtlTimer) {
                m_hasTtlTimer = false;
                m_ioObject.CancelTimer(HeartbeatTtlTimerId);
            }
            
            if (msg.HasCommand)
                ProcessCommandMessage(ref msg);

            result = m_session.PushMsg(ref msg);
            if (result == PushMsgResult.Full) 
                m_processMsg = PushOneThenDecodeAndPush;
                
            return result;
        }
        
        PushMsgResult PushOneThenDecodeAndPush (ref Msg msg)
        {
            var result = m_session.PushMsg(ref msg);
            if (result == PushMsgResult.Ok)
                m_processMsg = DecodeAndPush;
            return result;
        }
        
        PullMsgResult ProducePingMessage (ref Msg msg)
        {
            // 16-bit TTL + \4PING == 7
            int pingTtlLength = V3Protocol.PingCommand.Length + 1 + 2;

            msg.InitPool(pingTtlLength);
            msg.SetFlags(MsgFlags.Command);
            
            // Copy in the command message
            msg[0] = (byte) V3Protocol.PingCommand.Length;
            msg.Put(Encoding.ASCII.GetBytes(V3Protocol.PingCommand), 1, V3Protocol.PingCommand.Length);
            
            NetworkOrderBitsConverter.PutUInt16((ushort)m_options.HeartbeatTtl, msg, 1 + V3Protocol.PingCommand.Length);

            var result = m_mechanism.Encode(ref msg);
            m_nextMsg = PullAndEncode;
            
            if (!m_hasTimeoutTimer && m_heartbeatTimeout > 0) 
            {
                m_ioObject.AddTimer(m_heartbeatTimeout, HeartbeatTimeoutTimerId);
                m_hasTimeoutTimer = true;
            }
            
            return result;
        }
        
        PullMsgResult ProducePongMessage (ref Msg msg)
        {
            msg.Move(ref m_pongMsg);
            m_nextMsg = PullAndEncode;
            return m_mechanism.Encode(ref msg);
        }
        
        void ProcessCommandMessage (ref Msg msg)
        {
            byte commandNameSize = msg[0];

            // Malformed command
            if (msg.Size < commandNameSize + 1)
                return;

            string commandName = msg.GetString(Encoding.ASCII, 1, commandNameSize);
            if (commandName == V3Protocol.PingCommand)
                ProcessPingMessage(ref msg);
        }

        private void ProcessPingMessage(ref Msg msg)
        {
            // 16-bit TTL + \4PING == 7
            int pingTtlLength = 1 + V3Protocol.PingCommand.Length + 2;
            int pingMaxCtxLength = 16;

            // Malformed ping command
            if (msg.Size < pingTtlLength)
                return;

            ushort remoteHeartbeatTtl = NetworkOrderBitsConverter.ToUInt16(msg, V3Protocol.PingCommand.Length + 1);
            // The remote heartbeat is in 10ths of a second
            // so we multiply it by 100 to get the timer interval in ms.
            remoteHeartbeatTtl *= 100;

            if (!m_hasTtlTimer && remoteHeartbeatTtl > 0) {
                m_ioObject.AddTimer(remoteHeartbeatTtl, HeartbeatTtlTimerId);
                m_hasTtlTimer = true;
            }
            
            //  As per ZMTP 3.1 the PING command might contain an up to 16 bytes
            //  context which needs to be PONGed back, so build the pong message
            //  here and store it. Truncate it if it's too long.
            //  Given the engine goes straight to out_event, sequential PINGs will
            //  not be a problem.
            int contextLength = Math.Min(msg.Size - pingTtlLength, pingMaxCtxLength);
            m_pongMsg.InitPool(1 + V3Protocol.PongCommand.Length + contextLength);
            m_pongMsg.SetFlags(MsgFlags.Command);
            m_pongMsg[0] = (byte) V3Protocol.PongCommand.Length;
            m_pongMsg.Put(Encoding.ASCII.GetBytes(V3Protocol.PongCommand), 1, V3Protocol.PongCommand.Length);
            if (contextLength > 0)
                m_pongMsg.Put(msg.Slice(pingTtlLength, contextLength), 1 + V3Protocol.PongCommand.Length);

            m_nextMsg = ProducePongMessage;

            if (m_sendingState == SendState.Idle)
            {
                m_sendingState = SendState.Active;
                BeginSending();
            }
        }
        
        /// <summary>
        /// This would be called when a timer expires, although here it only throws NotSupportedException.
        /// </summary>
        /// <param name="id">an integer used to identify the timer (not used here)</param>
        /// <exception cref="NotSupportedException">TimerEvent is not supported on StreamEngine.</exception>
        public void TimerEvent(int id)
        {
            if (id == HeartbeatIntervalTimerId)
            {
                m_nextMsg = ProducePingMessage;
                
                if (m_sendingState == SendState.Idle)
                {
                    m_sendingState = SendState.Active;
                    BeginSending();
                }
                
                m_ioObject.AddTimer(m_options.HeartbeatInterval, HeartbeatIntervalTimerId);
            } 
            else if (id == HeartbeatTtlTimerId) 
            {
                m_hasTtlTimer = false;
                Error();
            } 
            else if (id == HeartbeatTimeoutTimerId) 
            {
                m_hasTimeoutTimer = false;
                Error();
            }
            else
            {
                throw new ArgumentOutOfRangeException();    
            }

            
        }
    }
}