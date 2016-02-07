using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace NetMQ.ReactiveExtensions
{
	/// <summary>
	///	Intent: Allow us to serialize an exception (ProtoBuf currently does not support this). See
    ///	http://stackoverflow.com/questions/94488/what-is-the-correct-way-to-make-a-custom-net-exception-serializable. 
	/// </summary>
	public static class SerializeAnException
	{
		#region Serialize an exception.
		public static byte[] SerializeException(this SerializableException exception)
		{
			// Round-trip the exception: Serialize and de-serialize with a BinaryFormatter.
			var bf = new BinaryFormatter();
			using (var ms = new MemoryStream())
			{
				// "Save" object state
				bf.Serialize(ms, exception);

				ms.Seek(0, 0); // Good practice.

				return ms.ToArray();
			}
		}

		public static SerializableException DeSerializeException(this byte[] bytes)
		{
			// Round-trip the exception: Serialize and de-serialize with a BinaryFormatter.
			var bf = new BinaryFormatter();
			using (var ms = new MemoryStream(bytes))
			{
				ms.Seek(0, 0); // Good practice.

				// Replace the original exception with de-serialized one
				SerializableException ex = (SerializableException)bf.Deserialize(ms);
				return ex;
			}
		}
		#endregion
	}

	[Serializable]
	// Important: This attribute is NOT inherited from Exception, and MUST be specified 
	// otherwise serialization will fail with a SerializationException stating that
	// "Type X in Assembly Y is not marked as serializable."
	public class SerializableException : Exception
	{
		public SerializableException()
		{
		}

		public SerializableException(string message)
			: base(message)
		{
		}

		public SerializableException(Exception innerException)
			: base("Outer wrapper to allow exception serialization, see InnerException.", innerException)
		{
		}

		public SerializableException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		// Without this constructor, deserialization will fail
		protected SerializableException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
