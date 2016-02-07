using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using NUnit.Framework;
using ProtoBuf;
// ReSharper disable SuggestVarOrType_SimpleTypes

namespace NetMQ.ReactiveExtensions.Tests
{
	public class SerializeAnException_Tests
	{
		[Test]
		public static void Can_Serialize_An_Exception()
		{
			var ex1 = new Exception("My Inner Exception 2");
			var ex2 = new Exception("My Exception 1", ex1);

			SerializableException originalException = new SerializableException(ex2);

			// Save the full ToString() value, including the exception message and stack trace.

			var rawBytes = originalException.SerializeException();
			SerializableException newException = rawBytes.DeSerializeException();

			string originalExceptionAsString = originalException.ToString();
			string newExceptionAsString = newException.ToString();

			// Double-check that the exception message and stack trace (owned by the base Exception) are preserved
			Assert.AreEqual(originalExceptionAsString, newExceptionAsString);
		}
	}
}
