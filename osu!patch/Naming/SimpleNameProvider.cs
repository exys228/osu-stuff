using osu_patch.Exceptions;
using osu_patch.Lib.DictionaryProcessor;
using System.Collections.Generic;

namespace osu_patch.Naming
{
	public class SimpleNameProvider : INameProvider
	{
		private Dictionary<string, string> _names;

		private SimpleNameProvider() { }

		public static SimpleNameProvider Initialize(IDictionary<string, string> names)
			=> new SimpleNameProvider { _names = new Dictionary<string, string>(names) };

		public static SimpleNameProvider Initialize(string path)
			=> new SimpleNameProvider { _names = DictionaryProcessor.Unpack(path) };

		public Dictionary<string, string> GetNamePairs()
			=> new Dictionary<string, string>(_names);

		public string GetName(string name)
		{
			if (_names is null || !_names.TryGetValue(name, out string obfName))
				return null;

			return obfName;
		}
	}
}
