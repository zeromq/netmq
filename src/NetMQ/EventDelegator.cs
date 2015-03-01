using System;

namespace NetMQ
{
    internal class EventDelegatorHelper<T> where T : EventArgs
    {
        private readonly Action m_registerToEvent;
        private readonly Action m_unregisterFromEvent;
        private EventHandler<T> m_event;
        private int m_counter;

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

                if (m_counter == 0)
                {
                    m_registerToEvent();
                }

                m_counter++;
            }
            remove
            {
                m_event -= value;

                m_counter++;

                if (m_counter == 0)
                {
                    m_unregisterFromEvent();
                }
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
