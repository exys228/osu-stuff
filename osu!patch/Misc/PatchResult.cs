using System;
using System.IO;

namespace osu_patch.Misc
{
	public class PatchResult
	{
		public string PatchName { get; }

		public PatchStatus Result { get; }

		public string Message { get; }

		public Exception Exception { get; }

		public PatchResult(Patch patch, PatchStatus result, string message = "", Exception ex = null)
		{
			PatchName = patch.Name;
			Result = result;
			Message = message;
			Exception = ex;
		}

		public void PrintDetails(TextWriter writer)
		{
			writer.WriteLine("Patch name: " + PatchName);
			writer.WriteLine("Execution result: " + Result);

			if (!string.IsNullOrEmpty(Message))
				writer.WriteLine("Message: " + Message);

			if (Exception != null)
				writer.WriteLine("Exception details:\n" + Exception);
		}
	}

	public enum PatchStatus
	{
		Disabled,
		Exception,
		Failure,
		Success
	}
}