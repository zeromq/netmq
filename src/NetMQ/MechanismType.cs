namespace NetMQ
{
    /// <summary>
    /// The type of security mechanism
    /// </summary>
    public enum MechanismType
    {
        /// <summary>
        /// No security
        /// </summary>
        Null,
        
        /// <summary>
        /// Username and password mechanism over non-encrypted channel 
        /// </summary>
        Plain,
        
        /// <summary>
        /// Curve encryption mechanism
        /// </summary>
        Curve
    }
}