using System.Collections.Generic;
using dnlib.DotNet;

namespace osu_patch.Plugins
{
	public interface IOsuPatchPlugin
	{
		void Load(ModuleDef originalObfOsuModule);

		IEnumerable<Patch> GetPatches();
	}
}
