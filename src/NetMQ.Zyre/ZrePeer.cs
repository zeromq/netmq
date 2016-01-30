using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.Zyre
{
    public class ZrePeer
    {
        private const int PeerEvasive = 10000; // 10 seconds' silence is evasive
        private const int PeerExpired = 30000; // 30 seconds' silence is expired
        private const int ReapInterval = 1000; // Once per second

        private DealerSocket m_mailbox;                 // Socket through to peer
        private readonly string m_uuid;                 // Identity string, 16 bytes
        private string m_endpoint;                      // Endpoint connected to
        private string m_name;                          // Peer's public name
        private string m_origin;                        // Origin node's public name
        private long m_evasiveAt;                       // Peer is being evasive
        private long m_expiredAt;                       // Peer has expired by now
        private bool m_connected;                       // Peer will send messages
        private bool m_ready;                           // Peer has said Hello to us
        private byte m_status;                           // Our status counter
        private ushort m_sentSequence;                  // Outgoing message sequence
        private ushort m_wantSequence;                  // Incoming message sequence
        private Dictionary<string, string> m_headers;   // Peer headers 
        
        private ZrePeer(string uuid)
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
        /// <param name="uuid">The identity for this peer</param>
        /// <param name="container">The dictionary of peers</param>
        /// <returns></returns>
        public static ZrePeer NewPeer(string uuid, Dictionary<string, ZrePeer> container)
        {
            var peer = new ZrePeer(uuid);
            container[uuid] = peer; // overwrite any existing entry for same uuid
            return peer;
        }

        /// <summary>
        /// Disconnect this peer. zeromq/zyre also frees peer memory, but our GC will take care of that
        /// </summary>
        public void Destroy()
        {
            Debug.Assert(m_mailbox != null, "Mailbox must not be null");
            Disconnect();
        }

        /// <summary>
        /// Connect peer mailbox (a DealerSocket)
        /// Configures mailbox and connects to peer's router endpoint
        /// </summary>
        /// <param name="replyTo"></param>
        /// <param name="endpoint"></param>
        public void Connect(string endpoint)
        {
            //  Create new outgoing socket (drop any messages in transit)
            m_mailbox = new DealerSocket(m_endpoint) // default action is to connect to the peer node
            {
                Options =
                {
                    //  Set our own identity on the socket so that receiving node knows who each message came from.
                    Identity = Encoding.ASCII.GetBytes(m_uuid),

                    //  Set a high-water mark that allows for reasonable activity
                    SendHighWatermark = PeerExpired * 100,
                }
            };
            m_endpoint = endpoint;
            m_connected = true;
            m_ready = false;
        }

        /// <summary>
        /// Disconnect peer mailbox 
        /// No more messages will be sent to peer until connected again
        /// </summary>
        public void Disconnect()
        {
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
        public string Uuid
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
        public long PeerEvasiveAt
        {
            get { return m_evasiveAt; }
        }

        /// <summary>
        /// Return peer future evasive time
        /// </summary>
        public long PeerExpiredAt
        {
            get { return m_expiredAt; }
        }

        /// <summary>
        /// Return peer name
        /// </summary>
        public string PeerName
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
            if (msg.Id == ZreMsg.MessageId.Hello)
            {
                //  HELLO always MUST have sequence = 1
                m_wantSequence = 1;
            }
            else
            {
                m_wantSequence++;
            }
            return m_wantSequence != msg.Sequence;
        }
    }
}
