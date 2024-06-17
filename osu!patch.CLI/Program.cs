using dnlib.DotNet;
using dnlib.DotNet.Writer;
using osu_patch.Explorers;
using osu_patch.Lib.StringFixer;
using osu_patch.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace osu_patch
{
	public class Program
	{
		private static string _configFileLocation;
		private static OsuPatcher _patcher;

		// Basic wrapper
		public static int Main(string[] args)
		{
			args = new string[] { @"E:\pr\osu-stuff\osu!patch.CLI\bin\Debug\osu!.exe" };
			if (args.Length != 1)
				return Exit("osu!patch - osu! assembly patcher\n" +
							   "by exys, 2019 - 2020\n" +
							   "\n" +
							   "Usage:\n" +
							   "osu!patch [obfuscated module]");

			var path = string.Empty;

			if (!File.Exists(path = Path.GetFullPath(args[0])))
				return XConsole.PrintFatal("Specified obfuscated module path does not exist!\n");

			try
			{
				_patcher = new OsuPatcher(path);
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to load one of the modules! Details:\n" + ex); }

			var executingAssLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			_configFileLocation = Path.Combine(executingAssLocation, "config.cfg");

			XConsole.PrintInfo($"Loaded assembly: {_patcher.Assembly.FullName}");
			XConsole.PrintInfo("MD5 hash of obfuscated assembly: " + _patcher.OsuHash);

			try
			{
				_patcher.LoadPlugins(); // Loading plugins
			}
			catch (Exception ex) { return XConsole.PrintFatal("Something really bad happened while trying to process plugins! Details:\n" + ex); }

			try
			{
				XConsole.PrintInfo("Cleaning control flow of obfuscated assembly");
				_patcher.CleanControlFlow();
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to deobfuscate control flow of obfuscated assembly! Details:\n" + ex); }

			try
			{
				XConsole.PrintInfo("Fixing strings in obfuscated assembly.");
				_patcher.FixStrings(); // Fixing strings
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to fix strings of obfuscated assembly! Details:\n" + ex); }

			try
			{
				_patcher.InitializeExplorer(null);
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to get clean names for obfuscated assembly! Details:\n" + ex); }

#if DEBUG
			_patcher.Write(Path.Combine(Path.GetDirectoryName(_patcher.OsuPath), "OsuObfModule-cflow-string-nmapped.exe"));
#endif

			XConsole.PrintInfo("Done! Now patching.");

			var overallSuccess = true;

			var failedDetails = new List<PatchResult>();

			void ExecutePatchCli(Patch patch)
			{
				XConsole.PrintInfo($"{patch.Name}: ", false);

				if (File.Exists(_configFileLocation)) // incase if config file not initialized
					patch.Enabled = GetConfigEnabled(patch.Name);

				PatchResult res = patch.Execute(_patcher);

				switch (res.Result)
				{
					case PatchStatus.Disabled:
						Console.ForegroundColor = ConsoleColor.Gray;
						XConsole.WriteLine("DISABLED");
						break;

					case PatchStatus.Exception:
					case PatchStatus.Failure:
						Console.ForegroundColor = ConsoleColor.Red;
						XConsole.WriteLine("FAIL");
						failedDetails.Add(res);
						overallSuccess = false;
						break;

					case PatchStatus.Success:
						Console.ForegroundColor = ConsoleColor.Green;
						XConsole.WriteLine("DONE");
						break;

					default:
						Console.ForegroundColor = ConsoleColor.DarkGray;
						XConsole.WriteLine("[???]");
						break;
				}

				Console.ResetColor();
			}

			// Executing local patches.
			foreach (var patch in LocalPatches.PatchList)
				ExecutePatchCli(patch);

			XConsole.PrintInfo("Done processing all local patches! Now processing patches from loaded add-ons...");

#if !CORE_PATCHES_ONLY
			// Executing all patches from all loaded plugins from all loaded assemblies (what a hierarchy)
			foreach (var plugin in _patcher.LoadedPlugins)
			{
				XConsole.PrintInfo($"{plugin.AssemblyName}: Processing plugin: {plugin.TypeName}.");

				foreach (var patch in plugin.Type.GetPatches())
				{
					ExecutePatchCli(patch);
				}

				XConsole.PrintInfo($"{plugin.AssemblyName}: Done processing: {plugin.TypeName}.");
			}
#endif
			if (!File.Exists(_configFileLocation))
			{
				XConsole.PrintInfo("Creating config file...");
				var configLines = string.Empty;
				foreach (var plugin in _patcher.LoadedPlugins)
				{
					foreach (var patch in plugin.Type.GetPatches())
						configLines += patch.Name + " = " + "Enabled\n"; // probably bad implementation of config but simplest i can imagine
				}

				File.WriteAllText(_configFileLocation, configLines);
				XConsole.PrintInfo("Config file created!");
			}

			XConsole.PrintInfo("Done processing all plugins.");

			if (failedDetails.Any())
			{
				XConsole.PrintInfo("There's some details about failed patches.");

				foreach (var details in failedDetails)
				{
					details.PrintDetails(Console.Out);
					XConsole.WriteLine();
				}
			}

			if (!overallSuccess)
			{
				XConsole.PrintInfo("There are some failed patches. Do you want to continue?");
				XConsole.PrintWarn("In case of self-update pressing 'N' will leave stock version of osu! without patching it!");
				XConsole.Write(XConsole.PAD + "Continue? (y/n) ");

				var exit = false;

				while (true)
				{
					var key = Console.ReadKey(true).Key;

					if (key == ConsoleKey.Y)
						break;

					if (key == ConsoleKey.N)
					{
						exit = true;
						break;
					}
				}

				XConsole.WriteLine();

				if (exit)
					return Exit(XConsole.Info("Aborted by user."));
			}

			var filename = Path.GetFileNameWithoutExtension(_patcher.OsuPath) + "-osupatch" + Path.GetExtension(_patcher.OsuPath);

			XConsole.PrintInfo($"Saving assembly as {filename}");

			try
			{
				_patcher.Write(Path.Combine(Path.GetDirectoryName(_patcher.OsuPath), filename));
			}
			catch (Exception ex) { return Exit(XConsole.Fatal("Unable to save patched assembly! Details:\n" + ex)); }

			_patcher.Module.Dispose();

#if DEBUG
			Console.ReadKey(true);
#endif

			return overallSuccess ? 0 : 1;
		}
		public static bool GetConfigEnabled(string key)
		{
			var lines = File.ReadAllLines(_configFileLocation);
			foreach (var line in lines)
			{
				var split = line.Split('=');
				if (split[0].Trim() == key)
				{
					return split[1].Trim() == "Enabled";
				}
			}
			return false;
		}

		private static int Exit(string msg = "")
		{
			XConsole.WriteLine(msg + "\n" + XConsole.Info("Exited."));
			Console.ReadKey(true);
			return 1;
		}
	}
}