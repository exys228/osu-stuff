using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using NameMapper.Exceptions;

namespace osu_patch.Naming
{
	class MapperNameProvider : INameProvider
	{
		private NameMapper.NameMapper _nameMapper;

		public static MapperNameProvider Instance { get; private set; }

		private bool _renameNames;

		private MapperNameProvider() { }

		public static MapperNameProvider Initialize(ModuleDefMD cleanModule, ModuleDefMD obfModule, TextWriter debugOutput = null, bool renameNames = false)
		{
			var newInstance = new MapperNameProvider
			{
				_renameNames = renameNames,
				_nameMapper = new NameMapper.NameMapper(cleanModule, obfModule, debugOutput, renameNames) { ShowErroredMethods = false }
			};

            newInstance._nameMapper.BeginProcessing();

            if (renameNames)
                obfModule.ResetTypeDefFindCache();

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
			if (_renameNames)
				return name;

			if (_nameMapper is null || !_nameMapper.Processed)
				throw new NameMapperException("NameMapper is not initialized!");

			return _nameMapper.FindName(name);
		}
	}
}
