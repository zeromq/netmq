namespace ZeroMQ.Devices
{
    /// <summary>
    /// Specifies possible running modes for a <see cref="Device"/>.
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
