using NUnit.Framework;

namespace MajordomoTests
{
    [TestFixture]
    public class MDPBrokerTests
    {
        // broker expects from
        //      CLIENT  ->  [sender adr][e][protocol header][service name][request]
        //      WORKER  ->  [sender adr][e][protocol header][mdp command][reply]

        // send from broker to
        //      CLIENT  ->  [CLIENT ADR][e][protocol header] / stripped by broker process / [SERVICENAME][DATA]
        //      WORKER  ->  READY       [WORKER ADR][e][protocol header] / stripped by broker process / [mdp command][service name]
        //                  REPLY       [WORKER ADR][e][protocol header] / stripped by broker process / [mdp command][client adr][e][reply]
        //                  HEARTBEAT   [WORKER ADR][e][protocol header] / stripped by broker process / [mdp command]

        /*
         * ProcessWorkerMessage
         *      READY
         *      REPLY
         *      HEARTBEAT
         * ProcessClientMessage
         *      REQ
         *      MMI
         * Purge
         * SendHeartBeat
         */
    }
}
