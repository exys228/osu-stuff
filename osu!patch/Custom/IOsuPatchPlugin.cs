using System.Collections.Generic;
using System.Runtime.CompilerServices;
using dnlib.DotNet;

namespace osu_patch.Custom
{
	public interface IOsuPatchPlugin
	{
		void Load(ModuleDef originalObfOsuModule);

		IEnumerable<Patch> GetPatches();
	}
}
