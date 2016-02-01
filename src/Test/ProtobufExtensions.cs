using System.IO;
using NUnit.Framework;
using ProtoBuf;

namespace Test
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

	[TestFixture]
	public static class ProtoBufExtensionsUnitTest
	{
		[Test]
		public static void Should_be_able_to_serialize_an_int()
		{
			int x = 42;
			var rawBytes = x.ProtoBufSerialize();
			int original = rawBytes.ProtoBufDeserialize<int>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_string()
		{
			string x = "Hello";
			var rawBytes = x.ProtoBufSerialize();
			string original = rawBytes.ProtoBufDeserialize<string>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_decimal()
		{
			decimal x = 123.4m;
			var rawBytes = x.ProtoBufSerialize();
			decimal original = rawBytes.ProtoBufDeserialize<decimal>();
			Assert.AreEqual(x, original);
		}
	}
}
