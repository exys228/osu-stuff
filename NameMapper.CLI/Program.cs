using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace NameMapper.CLI
{
	class Program
	{
		private static ModuleDefMD _cleanModule;
		private static ModuleDefMD _obfuscatedModule;

		private static string _cleanModulePath;
		private static string _obfuscatedModulePath;

		static int Main(string[] args)
		{
			if (args.Length < 2)
				return Message("dotnetmap - map names from assembly with deobfuscated names to assembly with obfuscated names\n" +
							   "by exys, 2019\n" +
							   "\n" +
				               "Usage:\n" +
				               "dotnetmap [clean module] [obfuscated module]");

			if (!File.Exists(_cleanModulePath = Path.GetFullPath(args[0])))
				return Message("E | Specified clean module path does not exist");

			if (!File.Exists(_obfuscatedModulePath = Path.GetFullPath(args[1])))
				return Message("E | Specified obfuscated module path does not exist");

			try
			{
				_cleanModule = ModuleDefMD.Load(_cleanModulePath);
				_obfuscatedModule = ModuleDefMD.Load(_obfuscatedModulePath);
			}
			catch (Exception e) { return Message("E | An error occurred while trying to load and process modules! Details:\n" + e); }

			Message($"I | Loaded modules: {_cleanModule.Assembly.FullName} (clean); {_obfuscatedModule.Assembly.FullName} (obfuscated).");

			NameMapper nameMapper = new NameMapper(_cleanModule, _obfuscatedModule, Console.Out);

			nameMapper.BeginProcessing();

			string filename = Path.GetFileNameWithoutExtension(_obfuscatedModulePath) + "-nmapped" + Path.GetExtension(_obfuscatedModulePath);

			Message("I | Finally writing module back (with \"-nmapped\" tag)!");

			_obfuscatedModule.Write(
			Path.Combine(
			Path.GetDirectoryName(_obfuscatedModulePath) ?? throw new Exception("Path to write module to is null unexpectedly"), filename),
			new ModuleWriterOptions(_obfuscatedModule)
			{
				MetadataOptions = { Flags = MetadataFlags.PreserveAll }
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
