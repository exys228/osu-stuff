using dnlib.DotNet;
using dnlib.DotNet.Emit;
using OsuPatchCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using HookManagerLib;

namespace HookManager.CLI
{
	public static class Program
	{
		private static ModuleDefMD _originalModule;

		public static int Main(string[] args)
		{
			if (args.Length < 1)
				return Message("HookAssemblyGenerator - generate API-like osu! methods access assembly\n"
								+ "by exys, 2019\n"
								+ "\n"
								+ "Usage:\n"
								+ "HookAssemblyGenerator [clean module]");

			string originalModulePath;

			if (!File.Exists(originalModulePath = Path.GetFullPath(args[0])))
				return Message("E | Specified module path does not exist!");

			try
			{
				_originalModule = ModuleDefMD.Load(originalModulePath);
			}
			catch (Exception e) { return Message("E | An error occurred while trying to load module! Details:\n" + e); }

			Message($"I | Loaded module: {_originalModule.Assembly.FullName}.");

			var assemblyName = $"OsuHooks-{MD5Helper.Compute(originalModulePath).Substring(0, 8)}";
			var moduleName = $"{assemblyName}.dll";

			var hookModule = new HookAssemblyGenerator(assemblyName, _originalModule).CreateHookModule();

			Message($"I | Finally writing {moduleName}!");

			hookModule.Write(Path.Combine(Path.GetDirectoryName(originalModulePath) ?? throw new Exception("Path to write module to is null unexpectedly"), moduleName));
			return 0;
		}

		private static int Message(string msg = "", bool newline = true)
		{
			Console.Write(msg + (newline ? Environment.NewLine : string.Empty));
			return 1;
		}
	}
}