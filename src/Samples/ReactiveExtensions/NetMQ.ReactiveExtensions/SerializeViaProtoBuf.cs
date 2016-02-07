using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using ProtoBuf;

namespace NetMQ.ReactiveExtensions
{
	/// <summary>
	/// Intent: Allow us to serialize using ProtoBuf.
	/// </summary>
	public static class SerializeViaProtoBuf
	{
		public static byte[] SerializeProtoBuf<T>(this T message)
		{
			byte[] result;
			using (var stream = new MemoryStream())
			{
				Serializer.Serialize(stream, message);
				result = stream.ToArray();
			}
			return result;
		}

		public static T DeserializeProtoBuf<T>(this byte[] bytes)
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
