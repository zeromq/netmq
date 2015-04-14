namespace TitanicCommons
{
    /// <summary>
    ///     must be implemented by any object used in generic api of client
    /// </summary>
    public interface ITitanicConvert<out T>
    {
        /// <summary>
        ///     serializes the object on which it is invoked to an array of bytes
        /// </summary>
        /// <typeparam name="T">type to work with</typeparam>
        /// <returns>an array of bytes representing the instance</returns>
        byte[] ConvertToBytes ();

        /// <summary>
        ///     deserializes a previous serialized object of type 'T' 
        /// </summary>
        /// <typeparam name="T">the type which has been serialized</typeparam>
        /// <param name="b">byte array containing the serialized entity</param>
        /// <returns>an instance generated</returns>
        T GenerateFrom (byte[] b);
    }
}
