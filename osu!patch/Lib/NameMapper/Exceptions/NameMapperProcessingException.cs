using System;
using System.Runtime.Serialization;

namespace osu_patch.Lib.NameMapper.Exceptions
{
	public class NameMapperProcessingException : Exception
	{
		public NameMapperProcessingException() { }

		public NameMapperProcessingException(string message) : base(message) { }

		public NameMapperProcessingException(string message, Exception inner) : base(message, inner) { }

		protected NameMapperProcessingException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}