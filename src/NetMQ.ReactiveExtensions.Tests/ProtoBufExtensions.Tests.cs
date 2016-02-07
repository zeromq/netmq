using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.ReactiveExtensions.Tests
{
	[TestFixture]
	public static class ProtoBufExtensionsUnitTest
	{
		[Test]
		public static void Should_be_able_to_serialize_an_int()
		{
			const int x = 42;
			var rawBytes = x.ProtoBufSerialize();
			int original = rawBytes.ProtoBufDeserialize<int>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_string()
		{
			const string x = "Hello";
			var rawBytes = x.ProtoBufSerialize();
			string original = rawBytes.ProtoBufDeserialize<string>();
			Assert.AreEqual(x, original);
		}

		[Test]
		public static void Should_be_able_to_serialize_a_decimal()
		{
			const decimal x = 123.4m;
			var rawBytes = x.ProtoBufSerialize();
			decimal original = rawBytes.ProtoBufDeserialize<decimal>();
			Assert.AreEqual(x, original);
		}
	}
}
