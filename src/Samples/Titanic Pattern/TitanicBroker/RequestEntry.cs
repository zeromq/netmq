using System;
using NetMQ;

namespace TitanicProtocol
{
    /// <summary>
    ///     represents an entry of a request made
    /// </summary>
    public class RequestEntry : IEquatable<RequestEntry>
    {
        private byte m_state;
        // introduced strictly for readability (!)
        /// <summary>
        ///     '+' indicates the request has been processed
        /// </summary>
        public static readonly byte Is_Processed = (byte) '+';
        /// <summary>
        ///     '-' indicates the request is pending
        /// </summary>
        public static readonly byte Is_Pending = (byte) '-';
        /// <summary>
        ///     'o' indicates the request has been closed
        /// </summary>
        public static readonly byte Is_Closed = (byte) 'o';

        /// <summary>
        ///     the Guid of the request
        /// </summary>
        public Guid RequestId { get; set; }

        /// <summary>
        ///     the position in a file, 0 based from the beginning of file
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        ///     the request made - only if needed
        /// </summary>
        public NetMQMessage Request { get; set; }

        /// <summary>
        ///     true if the request has been processed and false otherwise
        ///     <para>+ -> processed</para>
        ///     <para>- -> pending</para>
        ///     <para>o -> closed</para>
        /// </summary>
        public byte State
        {
            get { return m_state; }
            // make sure only valid values are used(!)
            set { m_state = value == Is_Processed ? Is_Processed : value == Is_Pending ? Is_Pending : Is_Closed; }
        }

        public RequestEntry ()
        {
            Position = -1;
            Request = null;
            RequestId = Guid.Empty;
            State = Is_Pending;
        }

        public override string ToString ()
        {
            var status = State == Is_Processed ? "processed" : State == Is_Pending ? "pending" : "closed";
            return string.Format ("Id={0} / Position={1} / IsProcessed={2} / Message={3}", RequestId, Position, status, Request);
        }

        public override int GetHashCode ()
        {
            return RequestId.GetHashCode ();
        }

        public override bool Equals (object obj)
        {
            return !ReferenceEquals (obj, null) && Equals (obj as RequestEntry);
        }

        public bool Equals (RequestEntry other)
        {
            if (ReferenceEquals (other, null))
                return false;

            return RequestId == other.RequestId && State == other.State;
        }

        public static bool operator == (RequestEntry one, RequestEntry other)
        {
            if (ReferenceEquals (one, other))
                return true;

            return !ReferenceEquals (one, null) && one.Equals (other);
        }

        public static bool operator != (RequestEntry one, RequestEntry other)
        {
            return !(one == other);
        }
    }
}
