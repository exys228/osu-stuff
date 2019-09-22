using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;

namespace osu_patch.Naming
{
	class DefaultNameProvider : INameProvider
	{
		private NameMapper.NameMapper _nameMapper;
		public static DefaultNameProvider Instance { get; private set; }

		private DefaultNameProvider() { }

		public static DefaultNameProvider Initialize(ModuleDefMD cleanModule, ModuleDefMD obfuscatedModule, TextWriter debugOutput = null)
		{
			var newInstance = new DefaultNameProvider();

			newInstance._nameMapper = new NameMapper.NameMapper(cleanModule, obfuscatedModule, debugOutput, false);
			newInstance._nameMapper.BeginProcessing();

			return Instance = newInstance;
		}

		public byte[] Pack()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter w = new BinaryWriter(ms))
				{
					var namePairs = _nameMapper.GetNamePairs();

					w.Write(namePairs.Count);

					foreach (var kvp in _nameMapper.GetNamePairs())
					{
						w.Write(kvp.Key);
						w.Write(kvp.Value);
					}

					return ms.ToArray();
				}
			}
		}

		public Dictionary<string, string> GetNamePairs() => _nameMapper.GetNamePairs();

		public string GetName(string name)
		{
			if (_nameMapper != null && !_nameMapper.Processed)
				throw new Exception("NameMapper is not initialized!");

			return _nameMapper?.FindName(name);
		}
	}
}
