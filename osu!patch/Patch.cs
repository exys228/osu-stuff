using osu_patch.Explorers;
using System;

namespace osu_patch
{
	public class Patch
	{
		public string Name { get; }

		public bool Enabled { get; set; }

		private readonly PatchFunction _patchMethod;

		public Patch(string patchName, bool enabled, PatchFunction function)
		{
			Enabled = enabled;
			Name = patchName;
			_patchMethod = function;
		}

		public PatchResult Execute(ModuleExplorer exp)
		{
			if (!Enabled)
				return Result(PatchStatus.Disabled);

#if DEBUG
			return _patchMethod(this, exp);
#else
			try
			{
				return _patchMethod(this, exp);
			}
			catch (Exception ex)
			{
				return Result(PatchStatus.Exception, ex: ex);
			}
#endif
		}

		public PatchResult Result(PatchStatus status = PatchStatus.Success, string message = "", Exception ex = null) =>
			new PatchResult(this, status, message, ex);
	}

	public delegate PatchResult PatchFunction(Patch parent, ModuleExplorer exp);
}
