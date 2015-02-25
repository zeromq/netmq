namespace NetMQ.Devices
{
    /// <summary>
    /// This enum-type specifies possible running modes for a <see cref="IDevice"/>.
    /// This is either Blocking, or Threaded.
    /// </summary>
    public enum DeviceMode
    {
        /// <summary>
        /// The device runs in the current thread.
        /// </summary>
        Blocking,

        /// <summary>
        /// The device runs in a self-managed thread.
        /// </summary>
        Threaded
    }
}