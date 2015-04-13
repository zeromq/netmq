using System;
using System.Text;

namespace TitanicCommons
{
    public interface ITitanicClient : IDisposable
    {
        /// <summary>
        ///     the API will publish log information over this event
        /// </summary>
        event EventHandler<TitanicLogEventArgs> LogInfoReady;

        /// <summary>
        ///     send a message and get a reply
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <param name="request">the actual request data as an array of bytes</param>
        /// <param name="retries">number of retries until the request is deemed to have failed, default == 3</param>
        /// <param name="waitInBetween">milliseconds to wait in between retries, default == 2500</param>
        /// <returns>the reply as tuple containing a byte array, the result, and the status</returns>
        /// <remarks>
        ///     will return <c>default (byte[])</c> if the result is invalid
        /// </remarks>
        Tuple<byte[], TitanicReturnCode> GetResult (string serviceName, byte[] request, int retries = 3, int waitInBetween = 2500);

        /// <summary>
        ///     send a message and get a reply
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <param name="request">the actual request data as a string</param>
        /// <param name="retries">number of retries until the request is deemed to have failed, default == 3</param>
        /// <param name="waitInBetween">milliseconds to wait in between retries, default == 2500</param>
        /// <returns>the reply as an array of bytes</returns>
        /// <returns>the reply as tuple containing a byte array, the result, and the status</returns>
        /// <remarks>
        ///     will return <c>default (byte[])</c> if the result is invalid
        /// </remarks>
        Tuple<byte[], TitanicReturnCode> GetResult (string serviceName, string request, int retries = 3, int waitInBetween = 2500);

        /// <summary>
        ///     send a message and get a reply
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <param name="request">the actual request data as a string</param>
        /// <param name="enc">the encoding to use for en- and decoding the string</param>
        /// <param name="retries">number of retries until the request is deemed to have failed, default == 3</param>
        /// <param name="waitInBetween">milliseconds to wait in between retries, default == 2500</param>
        /// <returns>the reply as tuple containing a string, the result, and the status</returns>
        /// <remarks>
        ///     will return <c>default (string)</c> if the result is invalid
        /// </remarks>
        Tuple<string, TitanicReturnCode> GetResult (string serviceName,
                                                    string request,
                                                    Encoding enc,
                                                    int retries = 3,
                                                    int waitInBetween = 2500);

        /// <summary>
        ///     send a message and get a reply
        /// </summary>
        /// <param name="serviceName">the service requested</param>
        /// <param name="request">the actual request data as an array of bytes</param>
        /// <param name="retries">number of retries until the request is deemed to have failed, default == 3</param>
        /// <param name="waitInBetween">milliseconds to wait in between retries, default == 2500</param>
        /// <returns>the reply as tuple containing the specified return type, the result, and the status</returns>
        /// <remarks>
        ///     will return <c>default (TOut)</c> if the result is invalid
        /// </remarks>
        Tuple<TOut, TitanicReturnCode> GetResult<TIn, TOut> (string serviceName,
                                                             TIn request,
                                                             int retries = 3,
                                                             int waitInBetween = 2500)
            where TIn : ITitanicConvert<TIn>
            where TOut : ITitanicConvert<TOut>, new ();

        /// <summary>
        ///     sending a request
        /// </summary>
        /// <param name="serviceName">the service to send the request to</param>
        /// <param name="request">the request data as an array of bytes</param>
        /// <returns>the GUID of the request as a string</returns>
        Guid Request (string serviceName, byte[] request);

        /// <summary>
        ///     sending a request
        /// </summary>
        /// <param name="serviceName">the service to send the request to</param>
        /// <param name="request">the request data as string</param>
        /// <returns>the GUID of the request as a string</returns>
        Guid Request (string serviceName, string request);

        /// <summary>
        ///     sending a request
        /// </summary>
        /// <param name="serviceName">the service to send the request to</param>
        /// <param name="request">the request data</param>
        /// <returns>the GUID of the request as a string</returns>
        Guid Request<T> (string serviceName, T request) where T : ITitanicConvert<T>;

        /// <summary>
        ///     gets a reply for a previous request
        /// </summary>
        /// <param name="requestId">the previous request's id</param>
        /// <param name="waitFor">max milliseconds to wait for the reply</param>
        /// <returns>a tuple containing the result and a status of the reply</returns>
        Tuple<byte[], TitanicReturnCode> Reply (Guid requestId, TimeSpan waitFor);

        /// <summary>
        ///     gets a reply for a previous request
        /// </summary>
        /// <param name="requestId">the previous request's id</param>
        /// <param name="retries">the number of retires to perform for retrieving the reply</param>
        /// <param name="waitBetweenRetries">milliseconds to wait in between the retrieval tries</param>
        /// <returns>a tuple containing the result and a status of the reply</returns>
        Tuple<byte[], TitanicReturnCode> Reply (Guid requestId, int retries, TimeSpan waitBetweenRetries);

        /// <summary>
        ///     gets a reply for a previous request
        /// </summary>
        /// <param name="requestId">the previous request's id</param>
        /// <param name="waitFor">max milliseconds to wait for the reply</param>
        /// <returns>a tuple containing the result and a status of the reply</returns>
        Tuple<T, TitanicReturnCode> Reply<T> (Guid requestId, TimeSpan waitFor) where T : ITitanicConvert<T>, new ();

        /// <summary>
        ///     gets a reply for a previous request
        /// </summary>
        /// <param name="requestId">the previous request's id</param>
        /// <param name="retries">the number of retires to perform for retrieving the reply</param>
        /// <param name="waitBetweenRetries">milliseconds to wait in between the retrieval tries</param>
        /// <returns>a tuple containing the result and a status of the reply</returns>
        Tuple<T, TitanicReturnCode> Reply<T> (Guid requestId, int retries, TimeSpan waitBetweenRetries)
            where T : ITitanicConvert<T>, new ();

        /// <summary>
        ///     closes a previous made request and the assigned reply
        /// </summary>
        /// <param name="requestId">the request's id</param>
        void CloseRequest (Guid requestId);
    }
}
