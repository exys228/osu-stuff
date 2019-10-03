using System;
using System.Runtime.Serialization;

namespace osu_patch.Exceptions
{
	public class ExplorerFindException : Exception
	{
		public ExplorerFindException() { }

		public ExplorerFindException(string message) : base(message) { }

		public ExplorerFindException(string message, Exception inner) : base(message, inner) { }

		protected ExplorerFindException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}