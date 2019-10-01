using System;
using System.Runtime.Serialization;

namespace NameMapper.Exceptions
{
	public class NameMapperException : Exception
	{
		public NameMapperException() { }

		public NameMapperException(string message) : base(message) { }

		public NameMapperException(string message, Exception inner) : base(message, inner) { }

		protected NameMapperException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}