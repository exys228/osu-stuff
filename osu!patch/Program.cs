using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using osu_patch.Explorers;
using osu_patch.Naming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using de4dot.code;
using de4dot.code.AssemblyClient;
using de4dot.code.deobfuscators;
using osu_patch.Misc;
using StringFixerMini;

using LdstrOccurence = System.Tuple<dnlib.DotNet.Emit.CilBody, int>;

namespace osu_patch
{
	static class CMain
	{
		private static ModuleDefMD _obfOsuModule;
		private static ModuleDefMD _cleanOsuModule;

		private static Assembly _obfOsuAssembly;

		private static ModuleExplorer _obfOsuExplorer;

		private static string _obfOsuPath = "";
		private static string _cleanOsuPath = "";

		public static string ObfOsuHash = "";

		private static readonly string ExecutingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                Message("[MAIN]: Unhandled exception! This shouldn't occur. Details:\n" + eventArgs.ExceptionObject);

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

			ObfOsuHash = MD5Helper.Compute(_obfOsuPath); // ORIGINAL!!!!!! hash, PLEASE PASS UNMODIFIED PEPPY-SIGNED ASSEMBLY AS _obfOsuModule!@!!32R1234 (refer to "Patcher addon" patch)

			// --- Cleaning control flow!

			try
			{
				var options = new ObfuscatedFile.Options
				{
					Filename = _obfOsuPath, // will this work or do i need filename ONLY? yes it will ok
					ControlFlowDeobfuscation = true,
					KeepObfuscatorTypes = true,
					RenamerFlags = 0,
					StringDecrypterType = DecrypterType.None,
					MetadataFlags = MetadataFlags.PreserveRids |
									MetadataFlags.PreserveUSOffsets |
									MetadataFlags.PreserveBlobOffsets |
									MetadataFlags.PreserveExtraSignatureData
				};

				var obfFile = new ObfuscatedFile(options, new ModuleContext(TheAssemblyResolver.Instance), new NewAppDomainAssemblyClientFactory());

				obfFile.DeobfuscatorContext = new DeobfuscatorContext();
				obfFile.Load(new List<IDeobfuscator> { new de4dot.code.deobfuscators.Unknown.DeobfuscatorInfo().CreateDeobfuscator() });

				obfFile.DeobfuscateBegin();
				obfFile.Deobfuscate();
				obfFile.DeobfuscateEnd();

				_obfOsuModule = obfFile.ModuleDefMD;
			}
			catch (Exception ex) { return Exit("F | Unable to deobfuscate control flow of obfuscated assembly! Details:\n" + ex); }

			// ---

			// fixin' strings real quick
			try
			{
				StringFixer.Fix(_obfOsuModule, _obfOsuAssembly);
			}
			catch (Exception ex) { return Exit("F | Unable to fix strings of obfuscated assembly! Details:\n" + ex); }

#if DEBUG
			_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), "OsuObfModule-cflow-string.exe"), new ModuleWriterOptions(_obfOsuModule)
			{
				MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
			});
#endif

			Message($"[MAIN]: Loaded assemblies: {_cleanOsuModule.Assembly.FullName} (clean); {_obfOsuModule.Assembly.FullName} (obfuscated).");

			// --- Working with names

			var cacheFolderName = Path.Combine(ExecutingAssemblyLocation, "cache");

			if (!Directory.Exists(cacheFolderName))
			{
				Message("[MAIN] Creating cache folder...");
				Directory.CreateDirectory(cacheFolderName);
			}

			var dictFile = Path.Combine(cacheFolderName, $"{ObfOsuHash}.dic");

			try
			{
// #if !DEBUG
				if (File.Exists(dictFile))
				{
					Message("[MAIN]: Found cached namedict file for this assembly! Loading names...");

					var nameProvider = SimpleNameProvider.Initialize(File.ReadAllBytes(dictFile));
					_obfOsuExplorer = new ModuleExplorer(_obfOsuModule, nameProvider);
				}
				else
// #endif
				{
					Message("[MAIN]: No cached dict found! Initializing DefaultNameProvider (NameMapper)...");

#if DEBUG
					TextWriter debugOut = Console.Out;
#else
					TextWriter debugOut = null;
#endif

					MapperNameProvider.Initialize(_cleanOsuModule, _obfOsuModule, debugOut);
					File.WriteAllBytes(dictFile, MapperNameProvider.Instance.Pack());
					_obfOsuExplorer = new ModuleExplorer(_obfOsuModule);
				}
			}
			catch (Exception ex) { return Exit("F | Unable to get clean names for obfuscated assembly! Details:\n" + ex); }

			// ---

			Message("[MAIN]: Done! Now patching.");

			bool overallSuccess = true;

			var failedDetails = new List<PatchResult>();

			foreach (var patch in Patches.PatchList)
			{
				Console.Write($"[MAIN]: {patch.Name}: ");

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

			if (failedDetails.Any())
				Message("[MAIN]: There's some details about failed patches.");

			foreach (var details in failedDetails)
			{
				details.PrintDetails(Console.Out);
				Message();
			}

            if (!overallSuccess)
            {
                Console.WriteLine("[MAIN]: There are some failed patches. Do you want to continue?");
                Console.WriteLine("[MAIN]: Warning: in case of self-update pressing N will leave stock version of osu! without patching it!");
                Console.Write("Waiting for user input: (y/n) ");

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

                if(exit)
                    return Exit("[MAIN]: Aborted by user.");
            }

			string filename = Path.GetFileNameWithoutExtension(_obfOsuPath) + "-osupatch" + Path.GetExtension(_obfOsuPath);

			Message($"[MAIN]: Saving assembly as {filename}");

			try
			{
				_obfOsuModule.Write(Path.Combine(Path.GetDirectoryName(_obfOsuPath), filename), new ModuleWriterOptions(_obfOsuModule)
				{
					MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
				});
			}
			catch (Exception ex) { return Exit("F | Unable to save patched assembly! Details:\n" + ex); }

			_cleanOsuModule.Dispose();
			_obfOsuModule.Dispose();

#if DEBUG
				Console.ReadKey(true);
#endif

			return overallSuccess ? 0 : 1;
		}

        private static int Message(string msg = "")
        {
            Console.WriteLine(msg);
            return 1;
        }

#if DEBUG
        private static int Exit(string msg = "")
        {
            Console.WriteLine(msg + "\n-=-=-=-=-=-=-=-=-=-=- EXIT EXIT EXIT");
            Console.ReadKey(true);
            return 1;
        }
#else
        private static int Exit(string msg = "") => Message(msg);
#endif
    }

    public static class OsuPatchExtensions
	{
		/// <summary>
		/// o no so old much deprecated use ModuleExplorer->TypeExplorer->MethodExplorer->MethodEditor when possible
		/// </summary>
		public static void Insert(this IList<Instruction> originalArray, int index, Instruction[] instructions)
		{
			if (instructions == null || instructions.Length == 0)
				throw new ArgumentException("Instructions array is null or empty.");

			if (index < 0)
				throw new ArgumentException($"Expected index >= 0, but received {index}.");
			
			Array.Reverse(instructions);

			for (int i = 0; i < instructions.Length; i++)
				originalArray.Insert(index, instructions[i]);
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
