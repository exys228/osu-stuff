using System;
using System.Runtime.Serialization;

namespace NameMapperLib.CLI
{
	public class NameMapperCliException : Exception
	{
		public NameMapperCliException() { }

		public NameMapperCliException(string message) : base(message) { }

		public NameMapperCliException(string message, Exception inner) : base(message, inner) { }

		protected NameMapperCliException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}