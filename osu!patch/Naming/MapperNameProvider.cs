using dnlib.DotNet;
using osu_patch.Exceptions;
using osu_patch.Lib.DictionaryProcessor;
using osu_patch.Lib.NameMapper;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace osu_patch.Naming
{
	public class MapperNameProvider : INameProvider
	{
		private NameMapper _nameMapper;

		public static MapperNameProvider Instance { get; private set; }

		private bool _renameNames;

		private MapperNameProvider() { }

		public static MapperNameProvider Initialize(ModuleDefMD cleanModule, ModuleDefMD obfModule, ConcurrentQueue<string> debugOutput = null, bool renameNames = false)
		{
			var newInstance = new MapperNameProvider
			{
				_renameNames = renameNames,
				_nameMapper = new NameMapper(cleanModule, obfModule, debugOutput, renameNames) { ShowErroredMethods = false }
			};

			newInstance._nameMapper.BeginProcessing();

			if (renameNames)
				obfModule.ResetTypeDefFindCache();

			return Instance = newInstance;
		}

		public byte[] Pack() =>
			DictionaryProcessor.Pack(_nameMapper.GetNamePairs());

		public Dictionary<string, string> GetNamePairs() =>
			_nameMapper.GetNamePairs();

		public string GetName(string name)
		{
			if (_renameNames)
				return name;

			if (_nameMapper is null || !_nameMapper.Processed)
				throw new NameProviderException("NameMapper is not initialized!");

			return _nameMapper.FindName(name);
		}
	}
}
