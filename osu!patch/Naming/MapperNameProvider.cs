using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using DictionaryProcessorLib;
using NameMapperLib;
using osu_patch.Exceptions;

namespace osu_patch.Naming
{
	class MapperNameProvider : INameProvider
	{
		private NameMapper _nameMapper;

		public static MapperNameProvider Instance { get; private set; }

		private bool _renameNames;

		private MapperNameProvider() { }

		public static MapperNameProvider Initialize(ModuleDefMD cleanModule, ModuleDefMD obfModule, TextWriter debugOutput = null, bool renameNames = false)
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
