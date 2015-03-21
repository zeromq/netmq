using NUnit.Framework;

namespace TitanicProtocolTests
{
    [TestFixture]
    public class TitanicIOTests
    {
        /*
         *  void SetConfig (string path)
         *  CheckConfig () -> create the appropriate directories
         *  IEnumerable<RequestEntry> ReadNextNonProcessedRequest ()
         *  IEnumerable<RequestEntry> ReadRequests ()
         *  RequestEntry FindRequest (Guid id)
         *  void WriteNewRequest (Guid id)
         *  void WriteProcessedRequest (RequestEntry entry)
         *  NetMQMessage ReadMessageFromFile (TitanicOperation op, Guid id)
         *  void WriteMessageToFile (TitanicOperation op, Guid id, NetMQMessage message)
         *  bool FileExists (TitanicOperation op, Guid id)
         *  void CloseRequest (Guid id)
         *
         */
    }
}
