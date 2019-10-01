using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu_patch.Exceptions;

namespace osu_patch.Naming
{
	class SimpleNameProvider : INameProvider
	{
		private Dictionary<string, string> _names;

		private SimpleNameProvider() { }

		public static SimpleNameProvider Initialize(IDictionary<string, string> names)
			=> new SimpleNameProvider { _names = new Dictionary<string, string>(names) };

		public static SimpleNameProvider Initialize(byte[] dictBytes)
		{
			using (MemoryStream ms = new MemoryStream(dictBytes))
			{
				using (BinaryReader r = new BinaryReader(ms))
				{
					var capacity = r.ReadInt32();
					var names = new Dictionary<string, string>(capacity);

					for (int i = 0; i < capacity; i++)
						names.Add(r.ReadString(), r.ReadString());

					return new SimpleNameProvider { _names = names }; ;
				}
			}
		}

		public Dictionary<string, string> GetNamePairs()
			=> new Dictionary<string, string>(_names);

		public string GetName(string name)
        {
            string obfName = null;

            if(_names is null || !_names.TryGetValue(name, out obfName))
                throw new NameProviderException("Unable to find name: " + name);

            return obfName;
        }
	}
}
