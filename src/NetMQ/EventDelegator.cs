using System;
using System.Threading;

namespace NetMQ
{
    /// <summary>
    /// EventDelegatorHelper is an internal utility-class that, for a given EventArgs type,
    /// registers two Actions for it - one to execute when the first handler is added, and the other when the last handler is removed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class EventDelegatorHelper<T> where T : EventArgs
    {
        private readonly Action m_registerToEvent;
        private readonly Action m_unregisterFromEvent;
        private EventHandler<T> m_event;
        private int m_counter;

        /// <summary>
        /// Create a new EventDelegatorHelper with the given Actions.
        /// </summary>
        /// <param name="registerToEvent">an Action to perform when the first handler is registered for the event</param>
        /// <param name="unregisterFromEvent">an Action to perform when the last handler is unregistered from the event</param>
        public EventDelegatorHelper(Action registerToEvent, Action unregisterFromEvent)
        {
            m_registerToEvent = registerToEvent;
            m_unregisterFromEvent = unregisterFromEvent;
        }

        public event EventHandler<T> Event
        {
            add
            {
                m_event += value;

                if (Interlocked.Increment(ref m_counter) == 1)
                    m_registerToEvent();
            }
            remove
            {
                m_event -= value;

                if (Interlocked.Decrement(ref m_counter) == 0)
                    m_unregisterFromEvent();
            }
        }

        /// <summary>
        /// Raise, or "Fire", the Event.
        /// </summary>
        /// <param name="sender">the sender that the event-handler that gets notified of this event will receive</param>
        /// <param name="args">the subclass of EventArgs that the event-handler will receive</param>
        public void Fire(object sender, T args)
        {
            var temp = m_event;
            if (temp != null)
            {
                temp(sender, args);
            }
        }
    }
}
