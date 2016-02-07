using System.IO;
using ProtoBuf;

namespace NetMQ.ReactiveExtensions
{
	/// <summary>
	/// Intent: extension methods to serialize/deserialize messages for inter-process communication.
	/// </summary>
	public static class ProtoBufExtensions
	{
		public static byte[] ProtoBufSerialize<T>(this T message)
		{
			byte[] result;
			using (var stream = new MemoryStream())
			{
				Serializer.Serialize(stream, message);
				result = stream.ToArray();
			}
			return result;
		}

		public static T ProtoBufDeserialize<T>(this byte[] bytes)
		{
			T result;
			using (var stream = new MemoryStream(bytes))
			{
				result = Serializer.Deserialize<T>(stream);
			}
			return result;
		}
	}	
}
