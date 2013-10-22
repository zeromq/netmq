

namespace NetMQ.Security.V0_1
{
    /// <summary>
    /// This enum serves to identify the type of content -- that is,
    /// whether it is a ChangeCipherSpec , a Handshake, or just ApplicationData.
    /// </summary>
    public enum ContentType
    {
        ChangeCipherSpec = 20,
        //alert(21), 
        Handshake = 22,
        ApplicationData = 23
    }
}
