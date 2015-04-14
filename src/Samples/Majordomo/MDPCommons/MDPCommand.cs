namespace MDPCommons
{
    /// <summary>
    ///     the available MDP commands 
    /// 
    ///     amended it <c>Kill</c> in order to force a worker to stop gracefully
    /// </summary>
    public enum MDPCommand
    {
        Kill = 0x00,
        Ready = 0x01,
        Request = 0x02,
        Reply = 0x03,
        Heartbeat = 0x04,
        Disconnect = 0x05
    }
}