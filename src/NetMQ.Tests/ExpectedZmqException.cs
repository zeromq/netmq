using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.Tests
{
	public class ExpectedZmqException : NUnit.Framework.ExpectedExceptionAttribute
	{
		public override bool Match(object obj)
		{
			return base.Match(obj);
		} 
	}
}
