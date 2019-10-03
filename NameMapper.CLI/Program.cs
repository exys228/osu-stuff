using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using NameMapper.Exceptions;
using StringFixerMini.CLI;

namespace NameMapper.CLI
{
	class Program
	{
		private static ModuleDefMD _cleanModule;
		private static ModuleDefMD _obfModule;

		private static string _cleanModulePath;
		private static string _obfModulePath;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData;

		static int Main(string[] args)
		{
			if (args.Length < 2)
				return Message("NameMapper.CLI - map names from assembly with deobfuscated names to assembly with obfuscated names\n" +
							   "by exys, 2019\n" +
							   "\n" +
							   "Usage:\n" +
							   "NameMapper.CLI [clean module] [obfuscated module]");

			if (!File.Exists(_cleanModulePath = Path.GetFullPath(args[0])))
				return Message("E | Specified clean module path does not exist");

			if (!File.Exists(_obfModulePath = Path.GetFullPath(args[1])))
				return Message("E | Specified obfuscated module path does not exist");

			try
			{
				_cleanModule = ModuleDefMD.Load(_cleanModulePath);
				_obfModule = ModuleDefMD.Load(_obfModulePath);
			}
			catch (Exception e) { return Message("E | An error occurred while trying to load and process modules! Details:\n" + e); }

			Message($"I | Loaded modules: {_cleanModule.Assembly.FullName} (clean); {_obfModule.Assembly.FullName} (obfuscated).");

			NameMapper nameMapper = new NameMapper(_cleanModule, _obfModule, Console.Out);

			nameMapper.BeginProcessing();

			string filename = Path.GetFileNameWithoutExtension(_obfModulePath) + "-nmapped" + Path.GetExtension(_obfModulePath);

			Message("I | Finally writing module back (with \"-nmapped\" tag)!");

			_obfModule.Write(
			Path.Combine(
			Path.GetDirectoryName(_obfModulePath) ?? throw new NameMapperCliException("Path to write module to is null unexpectedly"), filename),
			new ModuleWriterOptions(_obfModule)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});

			Console.ReadKey(true);

			return 0;
		}

		private static object _msgLock = new object();

		private static int Message(string msg = "", bool newline = true)
		{
			lock (_msgLock)
				Console.Write(msg + (newline ? Environment.NewLine : string.Empty));

			return 1;
		}
	}
}
