using System;

namespace NetMQ
{
    /// <summary>
    /// This flags enum-type is simply an indication of the direction of the poll-event,
    /// and can be None, PollIn, PollOut, or PollError.
    /// </summary>
    [Flags]
    public enum PollEvents
    {
        None = 0x0,
        PollIn = 0x1,
        PollOut = 0x2,
        PollError = 0x4
    }

    public static class PollEventsExtensions
    {
        public static bool HasIn(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollIn) == PollEvents.PollIn;
        }

        public static bool HasOut(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollOut) == PollEvents.PollOut;
        }

        public static bool HasError(this PollEvents pollEvents)
        {
            return (pollEvents & PollEvents.PollError) == PollEvents.PollError;
        }
    }
}