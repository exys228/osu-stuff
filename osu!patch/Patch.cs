using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.IO;

namespace osu_patch
{
	public class Patch
	{
		public string Name { get; }

		public bool Enabled { get; set; } = true; // Enabled by default, overriden by config.

		private readonly PatchFunction _patchMethod;

		public Patch(string patchName, PatchFunction function)
		{
			Name = patchName;
			_patchMethod = function;
		}

		public PatchResult Execute(OsuPatcher patcher)
		{
			if (!Enabled)
				return Result(PatchStatus.Disabled);

#if DEBUG
			return _patchMethod(patcher, this, patcher.Explorer);
#else
			try
			{
				return _patchMethod(patcher, this, patcher.Explorer);
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

	public delegate PatchResult PatchFunction(OsuPatcher patcher, Patch parent, ModuleExplorer exp);
}
