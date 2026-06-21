// #define DONT_USE_CACHED_DICT
// #define CORE_PATCHES_ONLY

using de4dot.code;
using de4dot.code.AssemblyClient;
using de4dot.code.deobfuscators;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
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
using System.Runtime.InteropServices;
using System.Threading;

namespace osu_patch
{
	public class OsuPatcher
	{
		public ModuleDefMD Module;
		public ModuleDefMD SelfModule;

		public Assembly Assembly;

		public ModuleExplorer Explorer;

		public List<PluginInfo> LoadedPlugins;

		public string OsuPath = "";
		public string OsuHash = "";

		private readonly string PluginsFolderLocation;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData |
															 MetadataFlags.KeepOldMaxStack;

		public ConcurrentQueue<string> FancyOutput;

		public OsuPatcher(string path)
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
			var executingAssLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

			PluginsFolderLocation = Path.Combine(executingAssLocation, "plugins");

			OsuPath = path;

			ModuleContext modCtx = ModuleDef.CreateModuleContext();
			AssemblyResolver asmResolver = (AssemblyResolver)modCtx.AssemblyResolver;
			asmResolver.EnableTypeDefCache = true;

			SelfModule = ModuleDefMD.Load(Assembly.GetEntryAssembly().Location);
			Module = ModuleDefMD.Load(OsuPath, modCtx);
			((AssemblyResolver)Module.Context.AssemblyResolver).AddToCache(Module);

			Assembly = Assembly.LoadFile(OsuPath);

			OsuHash = MD5Helper.Compute(OsuPath);
		}

		public void InitializeExplorer(INameProvider provider)
		{
			Explorer = new ModuleExplorer(Module, SelfModule, provider); // Fixing names (SimpleNameProvider/MapperNameProvider/KeyNameProvider)
		}

		public void FixStrings()
		{
			StringFixer.Fix(Module, Assembly);
		}

		public void Write(string path)
		{
			Module.Write(path, new ModuleWriterOptions(Module)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});
		}

		public void Write(Stream stream)
		{
			Module.Write(stream, new ModuleWriterOptions(Module)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});
		}

		public void WriteNoMetadata(Stream stream)
		{
			Module.Write(stream);
		}

		public void WriteNoMetadata(string path)
		{
			Module.Write(path);
		}

		public void LoadPlugins()
		{
			XConsole.PrintInfo($"Now loading all custom add-ons from {Path.GetFileName(PluginsFolderLocation)}/ folder.");

			LoadedPlugins = new List<PluginInfo>();

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
							plugin.Load(Module);

							XConsole.PrintInfo($"{fileName}: Loaded plugin: {type.Name}");

							LoadedPlugins.Add(new PluginInfo(fileName, type.Name, plugin));
						}
					}
					catch (BadImageFormatException) { XConsole.PrintError($"{fileName} is not a valid .NET assembly file!"); }
					catch (Exception ex) { XConsole.PrintError($"Unable to load {fileName}! Details:\n" + ex); }
				}
			}
			else Directory.CreateDirectory(PluginsFolderLocation);
		}

		public void CleanControlFlow()
		{
			Logger.Instance.MaxLoggerEvent = 0;

			var options = new ObfuscatedFile.Options
			{
				Filename = OsuPath,
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

			Module = obfFile.ModuleDefMD;
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
