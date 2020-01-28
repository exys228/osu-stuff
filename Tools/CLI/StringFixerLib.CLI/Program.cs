using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

using osu_patch;
using osu_patch.Lib.StringFixer;

using StringFixerLib;

namespace StringFixerLib.CLI
{
	static class Program
	{
		private static ModuleDefMD _module;
		private static Assembly _assembly;

		private static string _modulePath;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData |
															 MetadataFlags.KeepOldMaxStack;

		public static int Main(string[] args)
		{
			if (args.Length < 1)
				return XConsole.WriteLine("StringFixerMini.CLI\n" +
							   "by exys, 2019\n" +
							   "\n" +
							   "Usage:\n" +
							   "StringFixerMini.CLI [module]");

			if (!File.Exists(_modulePath = Path.GetFullPath(args[0])))
				return XConsole.PrintError("Specified module path does not exist");

			try
			{
				_module = ModuleDefMD.Load(_modulePath);
				_assembly = Assembly.LoadFile(_modulePath);
			}
			catch (Exception e) { return XConsole.PrintError("An error occurred while trying to load and process modules! Details:\n" + e); }

			XConsole.PrintInfo($"Loaded module: {_module.Assembly.FullName}.");

			StringFixer.Fix(_module, _assembly);

			string filename = Path.GetFileNameWithoutExtension(_modulePath) + "-string" + Path.GetExtension(_modulePath);

			XConsole.PrintInfo("Finally writing module back (with \"-string\" tag)!");

			_module.Write(
			Path.Combine(
			Path.GetDirectoryName(_modulePath) ?? throw new StringFixerCliException("Path to write module to is null unexpectedly"), filename),
			new ModuleWriterOptions(_module)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});

			Console.ReadKey(true);
			return 0;
		}
	}
}
