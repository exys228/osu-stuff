using System.Collections.Generic;

namespace osu_patch.Naming
{
	interface INameProvider
	{
		string GetName(string name);
		Dictionary<string, string> GetNamePairs();
	}
}
