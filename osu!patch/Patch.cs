using System;

namespace osu_patch
{
	public class Patch
	{
		public bool Enabled { get; set; }

		public delegate bool PatchFunction();

		PatchFunction PatchMethod;

		public string Name { get; }

		public Patch(string patchName, bool enabled, PatchFunction function)
		{
			Enabled = enabled;
			Name = patchName;
			PatchMethod = function;
		}

		public bool Execute()
		{
			if (!Enabled)
				return false;

			try { return PatchMethod(); }
			catch { return false; }
		}
	}
}
