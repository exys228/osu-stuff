using dnlib.DotNet;
using osu_patch.Lib.AssemblyDecoder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EazDecodeLib;

namespace osu_patch.Naming
{
	public class KeyNameProvider : INameProvider
	{
		private AssemblyDecoder _decoder;
		private Dictionary<string, string> _names;

		public static KeyNameProvider Initialize(ModuleDefMD obfModule, string key)
		{
			var nameProvider = new KeyNameProvider() { _decoder = new AssemblyDecoder(new CryptoHelper(key)) };

			nameProvider._decoder.Process(obfModule);
			nameProvider._names = nameProvider._decoder.GetNamePair();

			return nameProvider;
		}

		public string GetName(string name, bool returnOriginal)
		{
			if (_names is null || !_names.TryGetValue(name, out string obfName))
				return returnOriginal ? name : null;

			return obfName;
		}

		public Dictionary<string, string> GetNamePairs() => _names;
	}
}
