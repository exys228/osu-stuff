using System;
using System.Runtime.Serialization;

namespace osu_patch.Exceptions
{
	public class MethodEditorLocateException : Exception
	{
		public MethodEditorLocateException() { }

		public MethodEditorLocateException(string message) : base(message) { }

		public MethodEditorLocateException(string message, Exception inner) : base(message, inner) { }

		protected MethodEditorLocateException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}