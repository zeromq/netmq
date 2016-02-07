using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProtoBuf;

namespace NetMQ.ReactiveExtensions.Tests
{
	[TestFixture]
	public static class SerializeViaProtoBuf_Tests
	{
		[Test]
		public static void Should_be_able_to_serialize_an_int()
		{
			const int x = 42;
			var rawBytes = x.SerializeProtoBuf();
			int original = rawBytes.DeserializeProtoBuf<int>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_string()
		{
			const string x = "Hello";
			var rawBytes = x.SerializeProtoBuf();
			string original = rawBytes.DeserializeProtoBuf<string>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_decimal()
		{
			const decimal x = 123.4m;
			var rawBytes = x.SerializeProtoBuf();
			decimal original = rawBytes.DeserializeProtoBuf<decimal>();
			Assert.AreEqual(x, original);
		}
	}
}
