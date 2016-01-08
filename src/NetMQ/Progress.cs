#if NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ
{
    /// <summary>
    /// Defines a provider for progress updates.
    /// Placeholder for System.IProgress, introduced in .NET 4.5.
    /// This code is enabled for .NET 4.0 builds only, allowing compilation.
    /// </summary>
    /// <typeparam name="T">The type of progress update value.</typeparam>
    public interface IProgress<in T>
    {
        /// <summary>Reports a progress update.</summary>
        /// <param name="value">The value of the updated progress.</param>
        void Report(T value);
    }


    public class Progress<T> : IProgress<T>
    {
        private Action<T> _handler;

        /// <summary>Initializes the <see cref="Progress{T}"/> with the specified callback.</summary>
        /// <param name="handler">
        /// A handler to invoke for each reported progress value.  
        /// This code is enabled for .NET 4.0 builds only, allowing compilation.
        /// </param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="handler"/> is null.</exception>
        public Progress(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }
            _handler = handler;
        }

        public void Report(T value)
        {
            if (_handler != null)
            {
                _handler(value);
            }
        }
    }
}

#endif