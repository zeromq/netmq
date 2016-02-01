using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Zyre
{
    public class ZrePeer : IDisposable
    {
        private const int PeerEvasive = 10000; // 10 seconds' silence is evasive
        private const int PeerExpired = 30000; // 30 seconds' silence is expired
        private const ushort UshortMax = ushort.MaxValue;
        private const byte UbyteMax = byte.MaxValue;

        private DealerSocket m_mailbox;                 // Socket through to peer
        private readonly Guid m_uuid;                   // Identity guid, 16 bytes
        private string m_endpoint;                      // Endpoint connected to
        private string m_name;                          // Peer's public name
        private long m_evasiveAt;                       // Peer is being evasive
        private long m_expiredAt;                       // Peer has expired by now
        private bool m_connected;                       // Peer will send messages
        private bool m_ready;                           // Peer has said Hello to us
        private byte m_status;                          // Our status counter
        private ushort m_sentSequence;                  // Outgoing message sequence
        private ushort m_wantSequence;                  // Incoming message sequence
        private Dictionary<string, string> m_headers;   // Peer headers 
        
        private ZrePeer(Guid uuid)
        {
            m_uuid = uuid;
            m_ready = false;
            m_connected = false;
            m_sentSequence = 0;
            m_wantSequence = 0;
            m_headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Construct new ZrePeer object
        /// </summary>
        /// <param name="container">The dictionary of peers</param>
        /// <param name="guid">The identity for this peer</param>
        /// <returns></returns>
        public static ZrePeer NewPeer(Dictionary<Guid, ZrePeer> container, Guid guid)
        {
            var peer = new ZrePeer(guid);
            container[guid] = peer; // overwrite any existing entry for same uuid
            return peer;
        }

        /// <summary>
        /// Disconnect this peer and Dispose this class.
        /// </summary>
        public void Destroy()
        {
            Debug.Assert(m_mailbox != null, "Mailbox must not be null");
            Disconnect();
            Dispose();
        }

        /// <summary>
        /// Connect peer mailbox
        /// Configures a DealerSocket mailbox connected to peer's router endpoint
        /// </summary>
        /// <param name="replyTo"></param>
        /// <param name="endpoint"></param>
        public void Connect(Guid replyTo, string endpoint)
        {
            Debug.Assert(!m_connected);
            //  Create new outgoing socket (drop any messages in transit)
            m_mailbox = new DealerSocket(endpoint) // default action is to connect to the peer node
            {
                Options =
                {
                    //  Set our own identity on the socket so that receiving node
                    //  knows who each message came from. Note that we cannot use
                    //  the UUID directly as the identity since it may contain a
                    //  zero byte at the start, which libzmq does not like for
                    //  historical and arguably bogus reasons that it nonetheless
                    //  enforces.
                    Identity = GetIdentity(replyTo), 

                    //  Set a high-water mark that allows for reasonable activity
                    SendHighWatermark = PeerExpired * 100,

                    // SendTimeout = TimeSpan.Zero Instead of this, ZreMsg.Send() uses 
                }
            };
            m_endpoint = endpoint;
            m_connected = true;
            m_ready = false;
        }

        public static byte[] GetIdentity(Guid replyTo)
        {
            var result = new byte[17];
            result[0] = 1;
            var uuidBytes = replyTo.ToByteArray();
            Buffer.BlockCopy(uuidBytes, 0, result, 1, 16);
            return result;
        }

        /// <summary>
        /// Disconnect peer mailbox 
        /// No more messages will be sent to peer until connected again
        /// </summary>
        public void Disconnect()
        {
            m_mailbox.Dispose();
            m_mailbox = null;
            m_endpoint = null;
            m_connected = false;
            m_ready = false;
        }

        /// <summary>
        /// Send message to peer
        /// </summary>
        /// <param name="msg">the message</param>
        /// <returns>always true</returns>
        public bool Send(ZreMsg msg)
        {
            if (m_connected)
            {
                msg.Sequence = m_sentSequence++;
                msg.Send(m_mailbox);
            }
            return true;
        }

        /// <summary>
        /// Return peer connected status
        /// </summary>
        public bool Connected
        {
            get { return m_connected; }
        }

        /// <summary>
        /// Return peer identity string
        /// </summary>
        public Guid Uuid
        {
            get { return m_uuid; }
        }

        /// <summary>
        /// Return peer connection endpoint
        /// </summary>
        public string Endpoint
        {
            get { return m_endpoint; }
        }

        /// <summary>
        /// Register activity at peer
        /// </summary>
        public void Refresh()
        {
            m_evasiveAt = CurrentTimeMilliseconds() + PeerEvasive;
            m_expiredAt = CurrentTimeMilliseconds() + PeerExpired;
        }

        /// <summary>
        /// Milliseconds since January 1, 1970 UTC
        /// </summary>
        /// <returns></returns>
        public long CurrentTimeMilliseconds()
        {
            return (DateTime.UtcNow.Ticks - 621355968000000000) / 10000;
        }

        /// <summary>
        /// Return peer future expired time
        /// </summary>
        public long EvasiveAt
        {
            get { return m_evasiveAt; }
        }

        /// <summary>
        /// Return peer future evasive time
        /// </summary>
        public long ExpiredAt
        {
            get { return m_expiredAt; }
        }

        /// <summary>
        /// Return peer name
        /// </summary>
        public string Name
        {
            get { return m_name ?? ""; }
        }

        /// <summary>
        /// Return peer cycle
        /// This gives us a state change count for the peer, which we can
        /// check against its claimed status, to detect message loss.
        /// </summary>
        public byte Status
        {
            get { return m_status; }
        }

        /// <summary>
        /// Increment status
        /// </summary>
        public void IncrementStatus()
        {
            m_status = m_status == UbyteMax ? (byte)0 : m_status++;
        }

        /// <summary>
        /// Set peer name
        /// </summary>
        /// <param name="name"></param>
        public void SetName(string name)
        {
            m_name = name;
        }

        /// <summary>
        /// Set peer status
        /// </summary>
        /// <param name="status"></param>
        public void SetStatus(byte status)
        {
            m_status = status;
        }

        /// <summary>
        /// Return peer ready state
        /// </summary>
        public bool Ready
        {
            get { return m_ready; }
        }

        /// <summary>
        /// Set peer ready
        /// </summary>
        /// <param name="ready"></param>
        public void SetReady(bool ready)
        {
            m_ready = ready;
        }

        /// <summary>
        /// Get peer header value
        /// </summary>
        /// <param name="key">The he</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string Header(string key, string defaultValue)
        {
            string value;
            if (!m_headers.TryGetValue(key, out value))
            {
                return defaultValue;
            }
            return value;
        }

        /// <summary>
        /// Get peer headers table
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetHeaders()
        {
            return m_headers;
        }

        /// <summary>
        /// Set peer headers from provided dictionary
        /// </summary>
        /// <returns></returns>
        public void SetHeaders(Dictionary<string, string> headers)
        {
            m_headers = headers;
        }

        /// <summary>
        /// Check if messages were lost from peer, returns true if they were
        /// </summary>
        /// <param name="msg"></param>
        /// <returns>true if we have lost one or more message</returns>
        public bool MessagesLost(ZreMsg msg)
        {
            Debug.Assert(msg != null);
            if (msg.Id == ZreMsg.MessageId.Hello)
            {
                //  HELLO always MUST have sequence = 1
                m_wantSequence = 1;
            }
            else
            {
                m_wantSequence = m_wantSequence == UshortMax ? (ushort)0 : m_wantSequence++;
            }
            return m_wantSequence != msg.Sequence;
        }

        public override string ToString()
        {
            var name = string.IsNullOrEmpty(m_name) ? "NotSet" : m_name;
            return string.Format("name:{0} router endpoint:{1} connected:{2} ready:{3} status:{4}", name, m_endpoint, m_connected, m_ready, m_status);
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Release any contained resources.
        /// </summary>
        /// <param name="disposing">true if managed resources are to be released</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (m_mailbox != null)
            {
                m_mailbox.Dispose();
            }
        }
    }
}
