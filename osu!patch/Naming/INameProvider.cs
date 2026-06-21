using System.Collections.Generic;

namespace osu_patch.Naming
{
	public interface INameProvider
	{
		string GetName(string name, bool returnOriginal = false);
		Dictionary<string, string> GetNamePairs();
	}
}
