using System;
using System.Runtime.Serialization;

namespace osu_patch.Exceptions
{
	class NameProviderException : Exception
	{
		public NameProviderException() { }

		public NameProviderException(string message) : base(message) { }

		public NameProviderException(string message, Exception inner) : base(message, inner) { }

		protected NameProviderException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}