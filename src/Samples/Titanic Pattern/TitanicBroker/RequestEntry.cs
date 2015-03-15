using System;

namespace TitanicProtocol
{
    /// <summary>
    ///     represents an entry of a request made
    /// </summary>
    internal class RequestEntry : IEquatable<RequestEntry>
    {
        /// <summary>
        ///     the Guid of the request
        /// </summary>
        public Guid RequestId { get; set; }

        /// <summary>
        ///     the position in a file, 0 based from the beginning of file
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        ///     true if the request has been processed and false otherwise
        /// </summary>
        public bool IsProcessed { get; set; }

        public override string ToString ()
        {
            return string.Format ("Id={0} / Position={1} / IsProcessed={2}", RequestId, Position, IsProcessed);
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

            return RequestId == other.RequestId && IsProcessed == other.IsProcessed;
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
