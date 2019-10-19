//#define DONT_USE_CACHED_DICT

using de4dot.code;
using de4dot.code.AssemblyClient;
using de4dot.code.deobfuscators;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using osu_patch.Custom;
using osu_patch.Explorers;
using osu_patch.Misc;
using osu_patch.Naming;
using StringFixerMini;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace osu_patch
{
	static class Program
	{
		private static ModuleDefMD _obfOsuModule;
		private static ModuleDefMD _cleanOsuModule;

		private static Assembly _obfOsuAssembly;

		private static ModuleExplorer _obfOsuExplorer;

		private static List<PluginInfo> _loadedPlugins;

		private static string _obfOsuPath = "";
		private static string _cleanOsuPath = "";

		public static string ObfOsuHash = "";

		private static readonly string ExecutingAssemblyLocation;
		private static readonly string PluginsFolderLocation;
		private static readonly string CacheFolderLocation;

		private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
															 MetadataFlags.PreserveUSOffsets |
															 MetadataFlags.PreserveBlobOffsets |
															 MetadataFlags.PreserveExtraSignatureData |
															 MetadataFlags.KeepOldMaxStack;

		static Program()
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
			{
				Exit("F | Unhandled exception! This shouldn't occur. Details:\n" + eventArgs.ExceptionObject); // exit because ReadKey is needed if configuration is debug
				Environment.Exit(1);
			};



#if LIVE_DEBUG
			Environment.CurrentDirectory = @"C:\osu!";
			ExecutingAssemblyLocation = @"C:\osu!\osu!patch";

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
			ExecutingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#endif

			PluginsFolderLocation = Path.Combine(ExecutingAssemblyLocation, "plugins");
			CacheFolderLocation = Path.Combine(ExecutingAssemblyLocation, "cache");
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
				return Exit("F | Specified clean module path does not exist!\n");

			if (!File.Exists(_obfOsuPath = Path.GetFullPath(args[1])))
				return Exit("F | Specified obfuscated module path does not exist!\n");

			try
			{
				_obfOsuModule = ModuleDefMD.Load(_obfOsuPath);
				_cleanOsuModule = ModuleDefMD.Load(_cleanOsuPath);

				_obfOsuAssembly = Assembly.LoadFile(_obfOsuPath);
			}
			catch (Exception ex) { return Exit("F | Unable to load one of the modules! Details:\n" + ex); }

			ObfOsuHash = MD5Helper.Compute(_obfOsuPath); // ORIGINAL!!!!!!! hash, PLEASE PASS UNMODIFIED PEPPY-SIGNED ASSEMBLY AS _obfOsuModule!@!!32R1234 (refer to "Patch on update" patch)

			Message($"I | Loaded assemblies: {_cleanOsuModule.Assembly.FullName} (clean); {_obfOsuModule.Assembly.FullName} (obfuscated).");
			Message("I | MD5 hash of obfuscated assembly: " + ObfOsuHash);

			try
			{
				LoadPlugins(); // Loading plugins
			}
			catch (Exception ex) { return Exit("F | Something really bad happened while trying to process plugins! Details:\n" + ex); }

			try
			{
				CleanControlFlow(); // Cleaning control flow
			}
			catch (Exception ex) { return Exit("F | Unable to deobfuscate control flow of obfuscated assembly! Details:\n" + ex); }

			try
			{
				Message("I | Fixing strings in obfuscated assembly.");

				StringFixer.Fix(_obfOsuModule, _obfOsuAssembly); // Fixing strings
			}
			catch (Exception ex) { return Exit("F | Unable to fix strings of obfuscated assembly! Details:\n" + ex); }

			if (!Directory.Exists(CacheFolderLocation))
			{
				Message("I | Creating cache folder...");
				Directory.CreateDirectory(CacheFolderLocation);
			}

			try
			{
				var nameProvider = InitializeNameProvider(); // Fixing names (SimpleNameProvider/MapperNameProvider)

				_obfOsuExplorer = new ModuleExplorer(_obfOsuModule, nameProvider);
			}
			catch (Exception ex) { return Exit("F | Unable to get clean names for obfuscated assembly! Details:\n" + ex); }

#if DEBUG
			_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), "OsuObfModule-cflow-string-nmapped.exe"), new ModuleWriterOptions(_obfOsuModule)
			{
				MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
			});
#endif

			Message("I | Done! Now patching.");

			bool overallSuccess = true;

			var failedDetails = new List<PatchResult>();

			void ExecutePatchCli(Patch patch)
			{
				Console.Write($"I | {patch.Name}: ");

				PatchResult res = patch.Execute(_obfOsuExplorer);

				switch (res.Result)
				{
					case PatchStatus.Disabled:
						Console.ForegroundColor = ConsoleColor.Gray;
						Message("DISABLED");
						break;

					case PatchStatus.Exception:
					case PatchStatus.Failure:
						Console.ForegroundColor = ConsoleColor.Red;
						Message("FAIL");
						failedDetails.Add(res);
						overallSuccess = false;
						break;

					case PatchStatus.Success:
						Console.ForegroundColor = ConsoleColor.Green;
						Message("DONE");
						break;

					default:
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Message("[???]");
						break;
				}

				Console.ResetColor();
			}

			// Executing local patches.
			foreach (var patch in LocalPatches.PatchList)
				ExecutePatchCli(patch);

			Message("I | Done processing all local patches! Now processing patches from loaded add-ons...");

			// Executing all patches from all loaded plugins from all loaded assemblies (what a hierarchy)
			foreach (var plugin in _loadedPlugins)
			{
				Message($"I | {plugin.AssemblyName}: Processing plugin: {plugin.TypeName}.");

				foreach (var patch in plugin.Type.GetPatches())
					ExecutePatchCli(patch);

				Message($"I | {plugin.AssemblyName}: Done processing: {plugin.TypeName}.");
			}

			Message("I | Done processing all plugins.");

			if (failedDetails.Any())
			{
				Message("I | There's some details about failed patches.");

				foreach (var details in failedDetails)
				{
					details.PrintDetails(Console.Out);
					Message();
				}
			}

			if (!overallSuccess)
			{
				Console.WriteLine("I | There are some failed patches. Do you want to continue?");
				Console.WriteLine("W | In case of self-update pressing 'N' will leave stock version of osu! without patching it!");
				Console.Write("U | (y/n) ");

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

				Message();

				if (exit)
					return Exit("I | Aborted by user.");
			}

			string filename = Path.GetFileNameWithoutExtension(_obfOsuPath) + "-osupatch" + Path.GetExtension(_obfOsuPath);

			Message($"I | Saving assembly as {filename}");

			try
			{
				_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), filename), new ModuleWriterOptions(_obfOsuModule)
				{
					MetadataOptions = { Flags = DEFAULT_METADATA_FLAGS }
				});
			}
			catch (Exception ex) { return Exit("F | Unable to save patched assembly! Details:\n" + ex); }

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
			Message($"I | Now loading all custom add-ons from {Path.GetFileName(PluginsFolderLocation)}/ folder.");

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
							Message($"E | {fileName} is a .NET executable, not a class library!");
							continue;
						}

						var types = assembly.GetTypes().Where(a => a.GetInterfaces().Contains(typeof(IOsuPatchPlugin))).ToList();

						if (!types.Any())
						{
							Message($"E | {fileName} assembly does not contain a valid IOsuPatchPlugin class.");
							continue;
						}

						foreach (var type in types)
						{
							var plugin = (IOsuPatchPlugin)Activator.CreateInstance(type);
							plugin.Load(_obfOsuModule);

							Message($"I | {fileName}: Loaded plugin: {type.Name}");

							_loadedPlugins.Add(new PluginInfo(fileName, type.Name, plugin));
						}
					}
					catch (BadImageFormatException) { Message($"E | {fileName} is not a valid .NET assembly file!"); }
					catch (Exception ex) { Message($"E | Unable to load {fileName}! Details:\n" + ex); }
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
				Message("I | Found cached name dictionary file for this assembly! Loading names...");
				return SimpleNameProvider.Initialize(File.ReadAllBytes(dictFile));
			}
			else
#endif
			{
				Message("I | No cached name dictionary found! Initializing DefaultNameProvider (NameMapper)...");

#if DEBUG
				TextWriter debugOut = Console.Out;
#else
				TextWriter debugOut = null;
#endif

				MapperNameProvider.Initialize(_cleanOsuModule, _obfOsuModule, debugOut);
				File.WriteAllBytes(dictFile, MapperNameProvider.Instance.Pack());
				return MapperNameProvider.Instance;
			}
		}

		private static void CleanControlFlow()
		{
			Message("I | Cleaning control flow of obfuscated assembly");

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

			var obfFile = new ObfuscatedFile(options, new ModuleContext(TheAssemblyResolver.Instance), new NewAppDomainAssemblyClientFactory());

			obfFile.DeobfuscatorContext = new DeobfuscatorContext();
			obfFile.Load(new List<IDeobfuscator> { new de4dot.code.deobfuscators.Unknown.DeobfuscatorInfo().CreateDeobfuscator() });

			obfFile.DeobfuscateBegin();
			obfFile.Deobfuscate();
			obfFile.DeobfuscateEnd();

			_obfOsuModule = obfFile.ModuleDefMD;
		}

		// --- Misc methods

		private static int Message(string msg = "")
		{
			Console.WriteLine(msg);
			return 1;
		}

#if DEBUG
		private static int Exit(string msg = "")
		{
			Console.WriteLine(msg + "\n\n[DEBUG]: Exited.");
			Console.ReadKey(true);
			return 1;
		}
#else
		private static int Exit(string msg = "") => Message(msg);
#endif
	}



	public static class OsuPatchExtensions // bunch of helper methods and shortcuts // wanted to make it internal but may be useful in plugins? not sure
	{
		/// <summary>
		/// Not for editing osu's method bodies, use ModuleExplorer->TypeExplorer->MethodExplorer->MethodEditor instead.
		/// </summary>
		public static void Insert(this IList<Instruction> originalArray, int index, Instruction[] instructions)
		{
			if (instructions == null || instructions.Length == 0)
				throw new ArgumentException("Instructions array is null or empty.");

			if (index < 0)
				throw new ArgumentException($"Expected index >= 0, but received {index}.");

			Array.Reverse(instructions);

			foreach (var ins in instructions)
				originalArray.Insert(index, ins);
		}

		public static MemberRef CreateMethodRef(this ModuleDef module, bool isStatic, Type type, string methodName, Type returnType, params Type[] argsType)
		{
			TypeRefUser typeRef = type.GetTypeRef(module);

			TypeSig returnSig = returnType.GetTypeSig(module);
			TypeSig[] argsSig = new TypeSig[argsType.Length];

			for (int i = 0; i < argsSig.Length; i++)
				argsSig[i] = argsType[i].GetTypeSig(module);

			MethodSig methodSig = isStatic ? MethodSig.CreateStatic(returnSig, argsSig) : MethodSig.CreateInstance(returnSig, argsSig);

			MemberRefUser methodRef = new MemberRefUser(module, methodName, methodSig, typeRef);

			return methodRef;
		}

		public static TypeDef GetTypeDef(this FieldDef field) => field.FieldType.ToTypeDefOrRef().ResolveTypeDef();

		public static TypeSig GetTypeSig(this Type type, ModuleDef module) => type.GetTypeRef(module).ToTypeSig();

		public static TypeRefUser GetTypeRef(this Type type, ModuleDef module)
		{
			string nameSpace = null, typeName = type.FullName;
			int idx = typeName.LastIndexOf('.');

			if (idx >= 0)
			{
				nameSpace = typeName.Substring(0, idx);
				typeName = typeName.Substring(idx + 1);
			}

			return new TypeRefUser(module, nameSpace, typeName, module.Import(type).DefinitionAssembly.ToAssemblyRef());
		}

		public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0) return min;
			else if (val.CompareTo(max) > 0) return max;
			else return val;
		}
	}
}
