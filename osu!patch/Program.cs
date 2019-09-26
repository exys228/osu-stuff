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
using StringFixerMini;
using LdstrOccurence = System.Tuple<dnlib.DotNet.Emit.CilBody, int>;

namespace osu_patch
{
	static class CMain
	{
		public static ModuleDefMD ObfOsuModule;
		public static ModuleDefMD CleanOsuModule;

		public static Assembly ObfOsuAssembly;

		public static ModuleExplorer ObfOsuExplorer;

		public static string ObfOsuPath = "";
		public static string CleanOsuPath = "";

		public static string ObfOsuHash = "";

		public static readonly string ExecutingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		static int Main(string[] args)
		{
			// Console.ReadKey(true);

			if (args.Length < 2 || !File.Exists(CleanOsuPath = Path.GetFullPath(args[0])) || !File.Exists(ObfOsuPath = Path.GetFullPath(args[1])))
				return 1;

			try
			{
				ObfOsuModule = ModuleDefMD.Load(ObfOsuPath);
				CleanOsuModule = ModuleDefMD.Load(CleanOsuPath);

				ObfOsuAssembly = Assembly.LoadFile(ObfOsuPath);
			}
			catch { return 1; }

			ObfOsuHash = MD5Helper.Compute(ObfOsuPath); // ORIGINAL!!!!!! hash, PLEASE PASS UNMODIFIED PEPPY-SIGNED ASSEMBLY AS ObfOsuModule!@!!32R1234 (refer to "Patcher addon" patch)

			// --- Cleaning control flow!

			var options = new ObfuscatedFile.Options
			{
				Filename = ObfOsuPath, // will this work or do i need filename ONLY? yes it will ok
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

			ObfOsuModule = obfFile.ModuleDefMD;

			// ---

			// fixin' strings real quick
			StringFixer.Fix(ObfOsuModule, ObfOsuAssembly);

#if DEBUG
			ObfOsuModule.Write(Path.Combine(Path.GetDirectoryName(ObfOsuPath), "OsuObfModule-cflow-string.exe"), new ModuleWriterOptions(ObfOsuModule)
			{
				MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
			});
#endif

			Message($"[MAIN]: Loaded assemblies: {CleanOsuModule.Assembly.FullName} (clean); {ObfOsuModule.Assembly.FullName} (obfuscated).");

			var cacheFolderName = Path.Combine(ExecutingAssemblyLocation, "cache");

			if (!Directory.Exists(cacheFolderName))
			{
				Message("[MAIN] Creating cache folder...");
				Directory.CreateDirectory(cacheFolderName);
			}

			var dictFile = Path.Combine(cacheFolderName, $"{ObfOsuHash}.dic");

#if !DEBUG
			if (File.Exists(dictFile))
			{
				Message("[MAIN]: Found cached namedict file for this assembly! Loading names...");

				var nameProvider = SimpleNameProvider.Initialize(File.ReadAllBytes(dictFile));
				ObfOsuExplorer = new ModuleExplorer(ObfOsuModule, nameProvider);
			}
			else
#endif
			{
				Message("[MAIN]: No cached dict found! Initializing DefaultNameProvider (NameMapper)...");

#if DEBUG
				TextWriter debugOut = Console.Out;
#else
				TextWriter debugOut = null;
#endif

				DefaultNameProvider.Initialize(CleanOsuModule, ObfOsuModule, debugOut);
				File.WriteAllBytes(dictFile, DefaultNameProvider.Instance.Pack());
				ObfOsuExplorer = new ModuleExplorer(ObfOsuModule);
			}

			Message("[MAIN]: Done! Now patching.");

			bool overallSuccess = true;

			foreach (var patch in Patches.PatchList)
			{
				Console.Write($"[MAIN]: {patch.Name}: ");
				bool success = patch.Execute();

				if (!success)
				{
					if (!patch.Enabled)
					{
						Console.ForegroundColor = ConsoleColor.Gray;
						Message("DISABLED");
					}
					else
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Message("FAIL");
						overallSuccess = false;
					}
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Message("DONE");
				}

				Console.ResetColor();
			}

			string filename = Path.GetFileNameWithoutExtension(ObfOsuPath) + "-osupatch" + Path.GetExtension(ObfOsuPath);

			Message($"[MAIN]: Saving assembly as {filename}");

			ObfOsuModule.Write(Path.Combine(Path.GetDirectoryName(ObfOsuPath), filename), new ModuleWriterOptions(ObfOsuModule)
			{
				MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
			});

			CleanOsuModule.Dispose();
			ObfOsuModule.Dispose();

#if DEBUG
				Console.ReadKey(true);
#endif

			return overallSuccess ? 0 : 1;
		}

		private static int Message(string msg)
		{
			Console.WriteLine(msg);
			return 1;
		}

		private static int[] Range(int startIndex, int count)
		{
			int[] result = new int[count];

			for (int i = 0; i < count; i++)
			{
				result[i] = startIndex++;
			}

			return result;
		}

		public static OsuFindCollection FindTypeMethod(string typeName, string methodName)
		{
			try
			{
				var type = ObfOsuExplorer[typeName];
				var method = type[methodName];

				return new OsuFindCollection(type.Type, method.Method, true);
			}
			catch { return new OsuFindCollection(null, null, false); }
		}
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

		public static MemberRef CreateMethodRef(this ModuleDefMD module, bool isStatic, Type type, string methodName, Type returnType, params Type[] argsType)
		{
			TypeRefUser typeRef = type.GetTypeRef();

			TypeSig returnSig = returnType.GetTypeSig();
			TypeSig[] argsSig = new TypeSig[argsType.Length];

			for (int i = 0; i < argsSig.Length; i++)
				argsSig[i] = argsType[i].GetTypeSig();

			MethodSig methodSig = isStatic ? MethodSig.CreateStatic(returnSig, argsSig) : MethodSig.CreateInstance(returnSig, argsSig);

			MemberRefUser methodRef = new MemberRefUser(CMain.ObfOsuModule, methodName, methodSig, typeRef);

			return methodRef;
		}

		public static TypeDef GetTypeDef(this FieldDef field) => field.FieldType.ToTypeDefOrRef().ResolveTypeDef();

		public static TypeSig GetTypeSig(this Type type) => GetTypeRef(type).ToTypeSig();

		public static TypeRefUser GetTypeRef(this Type type)
		{
			string nameSpace = null, typeName = type.FullName;
			int idx = typeName.LastIndexOf('.');

			if (idx >= 0)
			{
				nameSpace = typeName.Substring(0, idx);
				typeName = typeName.Substring(idx + 1);
			}

			return new TypeRefUser(CMain.ObfOsuModule, nameSpace, typeName, CMain.ObfOsuModule.Import(type).DefinitionAssembly.ToAssemblyRef());
		}

		public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0) return min;
			else if (val.CompareTo(max) > 0) return max;
			else return val;
		}
	}
}
