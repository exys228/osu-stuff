using dnlib.DotNet;
using dnlib.DotNet.Writer;
using osu_patch;
using osu_patch.Lib.NameMapper;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace NameMapperLib.CLI
{
	static class Program
	{
		private static ModuleDefMD _cleanModule;
		private static ModuleDefMD _obfModule;

		private static string _cleanModulePath;
		private static string _obfModulePath;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData |
															 MetadataFlags.KeepOldMaxStack;

		public static int Main(string[] args)
		{
			if (args.Length < 2)
				return XConsole.WriteLine("NameMapper.CLI - map names from assembly with deobfuscated names to assembly with obfuscated names\n" +
							   "by exys, 2019\n" +
							   "\n" +
							   "Usage:\n" +
							   "NameMapper.CLI [clean module] [obfuscated module]");

			if (!File.Exists(_cleanModulePath = Path.GetFullPath(args[0])))
				return XConsole.PrintError("Specified clean module path does not exist");

			if (!File.Exists(_obfModulePath = Path.GetFullPath(args[1])))
				return XConsole.PrintError("Specified obfuscated module path does not exist");

			try
			{
				_cleanModule = ModuleDefMD.Load(_cleanModulePath, ModuleDef.CreateModuleContext());
				_obfModule = ModuleDefMD.Load(_obfModulePath, ModuleDef.CreateModuleContext());
			}
			catch (Exception e) { return XConsole.PrintError("An error occurred while trying to load and process modules! Details:\n" + e); }

			XConsole.PrintInfo($"Loaded modules: {_cleanModule.Assembly.FullName} (clean); {_obfModule.Assembly.FullName} (obfuscated).");

			var fancyOut = new ConcurrentQueue<string>();

			ThreadPool.QueueUserWorkItem(state =>
			{
				while (true)
				{
					if (!fancyOut.IsEmpty && fancyOut.TryDequeue(out string msg))
						XConsole.Write(msg);

					Thread.Sleep(1);
				}
			});

			NameMapper nameMapper = new NameMapper(_cleanModule, _obfModule, fancyOut);
			nameMapper.BeginProcessing();

			string filename = Path.GetFileNameWithoutExtension(_obfModulePath) + "-nmapped" + Path.GetExtension(_obfModulePath);

			XConsole.PrintInfo("Finally writing module back (with \"-nmapped\" tag)!");

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
	}
}
