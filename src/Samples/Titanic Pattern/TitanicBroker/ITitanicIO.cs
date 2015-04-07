using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using JetBrains.Annotations;

using NetMQ;

using TitanicCommons;

namespace TitanicProtocol
{
    public interface ITitanicIO
    {
        event EventHandler<TitanicLogEventArgs> LogInfoReady;

        string TitanicDirectory { get; }
        string TitanicQueue { get; }

        // ====== RequestEntry handling

        RequestEntry GetRequestEntry (Guid id);

        IEnumerable<RequestEntry> GetRequestEntries ([NotNull] Func<RequestEntry, bool> predicate);

        IEnumerable<RequestEntry> GetNotClosedRequestEntries ();

        void SaveRequestEntry ([NotNull] RequestEntry entry);

        void SaveNewRequestEntry (Guid id);

        void SaveNewRequestEntry (Guid id, [NotNull] NetMQMessage request);

        void SaveProcessedRequestEntry ([NotNull] RequestEntry entry);

        void CloseRequest (Guid id);

        // ====== Message handling

        NetMQMessage GetMessage (TitanicOperation op, Guid id);

        Task<NetMQMessage> GetMessageAsync (TitanicOperation op, Guid id);

        bool SaveMessage (TitanicOperation op, Guid id, [NotNull] NetMQMessage message);

        Task<bool> SaveMessageAsync (TitanicOperation op, Guid id, [NotNull] NetMQMessage message);

        bool ExistsMessage (TitanicOperation op, Guid id);
    }
}
