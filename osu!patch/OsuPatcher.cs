// #define DONT_USE_CACHED_DICT
#define CORE_PATCHES_ONLY

using de4dot.code;
using de4dot.code.AssemblyClient;
using de4dot.code.deobfuscators;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using osu_patch.Explorers;
using osu_patch.Lib.StringFixer;
using osu_patch.Naming;
using osu_patch.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace osu_patch
{
	public static class OsuPatcher
	{
		private static ModuleDefMD _obfOsuModule;
		private static ModuleDefMD _cleanOsuModule;

		private static Assembly _obfOsuAssembly;

		private static ModuleExplorer _obfOsuExplorer;

		private static List<PluginInfo> _loadedPlugins;

		private static string _obfOsuPath = "";
		private static string _cleanOsuPath = "";

		public static string ObfOsuHash = "";

		private static readonly string PluginsFolderLocation;
		private static readonly string CacheFolderLocation;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData |
															 MetadataFlags.KeepOldMaxStack;

		public static ConcurrentQueue<string> FancyOutput;

		static OsuPatcher()
		{
			FancyOutput = new ConcurrentQueue<string>();

			// ReSharper disable once FunctionNeverReturns
			ThreadPool.QueueUserWorkItem(state =>
			{
				while (true)
				{
					if (!FancyOutput.IsEmpty && FancyOutput.TryDequeue(out string msg))
						XConsole.Write(msg);

					Thread.Sleep(1);
				}
			});

#if !DEBUG
			AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
			{
				Exit(XConsole.Fatal("Unhandled exception! This shouldn't occur. Details:\n" + eventArgs.ExceptionObject)); // exit because ReadKey is needed if configuration is debug
				Environment.Exit(1);
			};
#endif


#if LIVE_DEBUG
			Environment.CurrentDirectory = @"C:\osu!";
			var executingAssLocation = @"C:\osu!\osu!patch";

			var proc = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = @"OsuVersionDownloader\OsuVersionDownloader.exe",
					Arguments = "Stable40 osu!.exe",
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					UseShellExecute = false
				}
			};

			proc.Start();
			proc.WaitForExit();

#else
			var executingAssLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#endif

			PluginsFolderLocation = Path.Combine(executingAssLocation, "plugins");
			CacheFolderLocation = Path.Combine(executingAssLocation, "cache");
		}

		public static int Main(string[] args)
		{
			// Console.ReadKey(true);

			if (args.Length < 2)
				return Exit("osu!patch - osu! assembly patcher based on NameMapper\n" +
							   "by exys, 2019\n" +
							   "\n" +
							   "Usage:\n" +
							   "osu!patch [clean module] [obfuscated module]");

			if (!File.Exists(_cleanOsuPath = Path.GetFullPath(args[0])))
				return XConsole.PrintFatal("Specified clean module path does not exist!\n");

			if (!File.Exists(_obfOsuPath = Path.GetFullPath(args[1])))
				return XConsole.PrintFatal("Specified obfuscated module path does not exist!\n");

			try
			{
				_obfOsuModule = ModuleDefMD.Load(_obfOsuPath);
				_cleanOsuModule = ModuleDefMD.Load(_cleanOsuPath);

				_obfOsuAssembly = Assembly.LoadFile(_obfOsuPath);
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to load one of the modules! Details:\n" + ex); }

			ObfOsuHash = MD5Helper.Compute(_obfOsuPath); // ORIGINAL!!!!!!! hash, PLEASE PASS UNMODIFIED PEPPY-SIGNED ASSEMBLY AS _obfOsuModule!@!!32R1234 (refer to "Patch on update" patch)

			XConsole.PrintInfo($"Loaded assemblies: {_cleanOsuModule.Assembly.FullName} (clean); {_obfOsuModule.Assembly.FullName} (obfuscated).");
			XConsole.PrintInfo("MD5 hash of obfuscated assembly: " + ObfOsuHash);

			try
			{
				LoadPlugins(); // Loading plugins
			}
			catch (Exception ex) { return XConsole.PrintFatal("Something really bad happened while trying to process plugins! Details:\n" + ex); }

			try
			{
				CleanControlFlow(); // Cleaning control flow
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to deobfuscate control flow of obfuscated assembly! Details:\n" + ex); }

			try
			{
				XConsole.PrintInfo("Fixing strings in obfuscated assembly.");
				StringFixer.Fix(_obfOsuModule, _obfOsuAssembly); // Fixing strings
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to fix strings of obfuscated assembly! Details:\n" + ex); }

			if (!Directory.Exists(CacheFolderLocation))
			{
				XConsole.PrintInfo("Creating cache folder...");
				Directory.CreateDirectory(CacheFolderLocation);
			}

			try
			{
				var nameProvider = InitializeNameProvider(); // Fixing names (SimpleNameProvider/MapperNameProvider)
				_obfOsuExplorer = new ModuleExplorer(_obfOsuModule, nameProvider);
			}
			catch (Exception ex) { return XConsole.PrintFatal("Unable to get clean names for obfuscated assembly! Details:\n" + ex); }

#if DEBUG
			_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), "OsuObfModule-cflow-string-nmapped.exe"), new ModuleWriterOptions(_obfOsuModule)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});
#endif

			XConsole.PrintInfo("Done! Now patching.");

			bool overallSuccess = true;

			var failedDetails = new List<PatchResult>();

			void ExecutePatchCli(Patch patch)
			{
				XConsole.PrintInfo($"{patch.Name}: ", false);

				PatchResult res = patch.Execute(_obfOsuExplorer);

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
			foreach (var plugin in _loadedPlugins)
			{
				XConsole.PrintInfo($"{plugin.AssemblyName}: Processing plugin: {plugin.TypeName}.");

				foreach (var patch in plugin.Type.GetPatches())
					ExecutePatchCli(patch);

				XConsole.PrintInfo($"{plugin.AssemblyName}: Done processing: {plugin.TypeName}.");
			}
#endif

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

			string filename = Path.GetFileNameWithoutExtension(_obfOsuPath) + "-osupatch" + Path.GetExtension(_obfOsuPath);

			XConsole.PrintInfo($"Saving assembly as {filename}");

			try
			{
				_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), filename), new ModuleWriterOptions(_obfOsuModule)
				{
					MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
				});
			}
			catch (Exception ex) { return Exit(XConsole.Fatal("Unable to save patched assembly! Details:\n" + ex)); }

			_cleanOsuModule.Dispose();
			_obfOsuModule.Dispose();

#if DEBUG
			Console.ReadKey(true);
#endif

#if LIVE_DEBUG
			Process.Start(new ProcessStartInfo
			{
				FileName = "cmd",
				Arguments = "/c timeout /T 1 /NOBREAK & move /Y \"osu!-osupatch.exe\" \"osu!.exe\"",
				WorkingDirectory = Environment.CurrentDirectory,
				WindowStyle = ProcessWindowStyle.Hidden,
				CreateNoWindow = true,
				UseShellExecute = false
			});
#endif

			return overallSuccess ? 0 : 1;
		}

		// --- Separate methods (Main is too big anyways lol)

		private static void LoadPlugins()
		{
			XConsole.PrintInfo($"Now loading all custom add-ons from {Path.GetFileName(PluginsFolderLocation)}/ folder.");

			_loadedPlugins = new List<PluginInfo>();

			if (Directory.Exists(PluginsFolderLocation))
			{
				foreach (var file in Directory.GetFiles(PluginsFolderLocation, "*.dll"))
				{
					var fileName = Path.GetFileName(file);

					try
					{
						Assembly assembly = Assembly.LoadFile(file);

						if (assembly.EntryPoint != null)
						{
							XConsole.PrintError($"{fileName} is a .NET executable, not a class library!");
							continue;
						}

						var types = assembly.GetTypes().Where(a => a.GetInterfaces().Contains(typeof(IOsuPatchPlugin))).ToList();

						if (!types.Any())
						{
							XConsole.PrintError($"{fileName} assembly does not contain a valid IOsuPatchPlugin class.");
							continue;
						}

						foreach (var type in types)
						{
							var plugin = (IOsuPatchPlugin)Activator.CreateInstance(type);
							plugin.Load(_obfOsuModule);

							XConsole.PrintInfo($"{fileName}: Loaded plugin: {type.Name}");

							_loadedPlugins.Add(new PluginInfo(fileName, type.Name, plugin));
						}
					}
					catch (BadImageFormatException) { XConsole.PrintError($"{fileName} is not a valid .NET assembly file!"); }
					catch (Exception ex) { XConsole.PrintError($"Unable to load {fileName}! Details:\n" + ex); }
				}
			}
			else Directory.CreateDirectory(PluginsFolderLocation);
		}

		private static INameProvider InitializeNameProvider()
		{
			var dictFile = Path.Combine(CacheFolderLocation, $"{ObfOsuHash}.dic");

#if !DEBUG || !DONT_USE_CACHED_DICT
			if (File.Exists(dictFile))
			{
				XConsole.PrintInfo("Found cached name dictionary file for this assembly! Loading names...");
				return SimpleNameProvider.Initialize(dictFile);
			}
			else
#endif
			{
				XConsole.PrintInfo("No cached name dictionary found! Initializing DefaultNameProvider (NameMapper)...");

#if DEBUG
				var debugOut = FancyOutput;
#else
				ConcurrentQueue<string> debugOut = null;
#endif

				MapperNameProvider.Initialize(_cleanOsuModule, _obfOsuModule, debugOut);

#if !DEBUG || !DONT_USE_CACHED_DICT
				File.WriteAllBytes(dictFile, MapperNameProvider.Instance.Pack());
#endif

				return MapperNameProvider.Instance;
			}
		}

		private static void CleanControlFlow()
		{
			XConsole.PrintInfo("Cleaning control flow of obfuscated assembly");

			Logger.Instance.MaxLoggerEvent = 0;

			var options = new ObfuscatedFile.Options
			{
				Filename = _obfOsuPath,
				ControlFlowDeobfuscation = true,
				KeepObfuscatorTypes = true,
				RenamerFlags = 0,
				StringDecrypterType = DecrypterType.None,
				MetadataFlags = DEFAULT_METADATA_FLAGS
			};

			var obfFile = new ObfuscatedFile(options, new ModuleContext(TheAssemblyResolver.Instance), new NewAppDomainAssemblyClientFactory())
			{
				DeobfuscatorContext = new DeobfuscatorContext()
			};

			obfFile.Load(new List<IDeobfuscator> { new de4dot.code.deobfuscators.Unknown.DeobfuscatorInfo().CreateDeobfuscator() });

			obfFile.DeobfuscateBegin();
			obfFile.Deobfuscate();
			obfFile.DeobfuscateEnd();

			_obfOsuModule = obfFile.ModuleDefMD;
		}

		// --- Misc methods

		public static ModuleExplorer GetRoot(this IExplorerParent explorerParent)
		{
			while (!(explorerParent is ModuleExplorer))
				explorerParent = explorerParent.GetParent();

			return (ModuleExplorer)explorerParent;
		}

#if DEBUG
		private static int Exit(string msg = "")
		{
			XConsole.WriteLine(msg + "\n" + XConsole.Info("Exited."));
			Console.ReadKey(true);
			return 1;
		}
#else
		private static int Exit(string msg = "") => XConsole.PrintError(msg);
#endif
	}
}
