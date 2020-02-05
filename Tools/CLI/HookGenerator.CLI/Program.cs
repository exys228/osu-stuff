using dnlib.DotNet;
using System;
using System.IO;

namespace osu_patch.Lib.HookGenerator.CLI
{
	public static class Program
	{
		private static ModuleDefMD _originalModule;

		public static int Main(string[] args)
		{
			if (args.Length < 1)
				return XConsole.PrintInfo("HookGenerator.CLI - generate API-like osu! methods access assembly\n"
								+ "by exys, 2019\n"
								+ "\n"
								+ "Usage:\n"
								+ "HookGenerator.CLI [clean module]");

			string originalModulePath;

			if (!File.Exists(originalModulePath = Path.GetFullPath(args[0])))
				return XConsole.PrintError("Specified module path does not exist!");

			try
			{
				_originalModule = ModuleDefMD.Load(originalModulePath);
			}
			catch (Exception e) { return XConsole.PrintError("An error occurred while trying to load module! Details:\n" + e); }

			XConsole.PrintInfo($"Loaded module: {_originalModule.Assembly.FullName}.");

			var assemblyName = $"OsuHooks-{MD5Helper.Compute(originalModulePath).Substring(0, 8)}";
			var moduleName = $"{assemblyName}.dll";

			var hookModule = new HookGenerator(assemblyName, _originalModule).CreateHookModule();

			XConsole.PrintInfo($"Finally writing {moduleName}!");

			hookModule.Write(Path.Combine(Path.GetDirectoryName(originalModulePath) ?? throw new Exception("Path to write module to is null unexpectedly"), moduleName));
			return 0;
		}
	}
}