namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// This enum-type serves to identify the type of content -- that is,
    /// whether it is a ChangeCipherSpec , a Handshake, or just ApplicationData.
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// This signals a change of cipher-spec.
        /// </summary>
        ChangeCipherSpec = 20,
        //alert(21), 

        /// <summary>
        /// This denotes content that is of the handshaking part of the protocol.
        /// </summary>
        Handshake = 22,

        /// <summary>
        /// This denotes content that is actual application information, as opposed to part of the protocol.
        /// </summary>
        ApplicationData = 23
    }
}
