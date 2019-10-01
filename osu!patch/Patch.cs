using System;
using dnlib.DotNet;
using osu_patch.Explorers;
using osu_patch.Misc;

namespace osu_patch
{
	class Patch
	{
		public string Name { get; }

		public bool Enabled { get; set; }

		private PatchFunction _patchMethod;

		public Patch(string patchName, bool enabled, PatchFunction function)
		{
			Enabled = enabled;
			Name = patchName;
			_patchMethod = function;
		}

		public PatchResult Execute(ModuleExplorer exp)
		{
			if (!Enabled)
				return new PatchResult(this, PatchStatus.Disabled);

            try
            {
                return _patchMethod(this, exp);
            }
            catch (Exception ex)
            {
                return new PatchResult(this, PatchStatus.Exception, ex: ex);
            }
        }
	}

	delegate PatchResult PatchFunction(Patch parent, ModuleExplorer exp);
}
