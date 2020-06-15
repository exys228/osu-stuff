using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace osu_patch
{
	public static class OsuPatchExtensions // bunch of helper methods and shortcuts
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
			return new MemberRefUser(module, methodName, methodSig, typeRef);
		}

		public static TypeDef GetTypeDef(this FieldDef field) => field.FieldType.ToTypeDefOrRef().ResolveTypeDef();

		public static TypeSig GetTypeSig(this Type type, ModuleDef module) => type.GetTypeRef(module).ToTypeSig();

		public static TypeRefUser GetTypeRef(this Type type, ModuleDef module)
		{
			return new TypeRefUser(module, type.Namespace, type.Name, module.Import(type).DefinitionAssembly.ToAssemblyRef());
		}

		public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
		{
			if (val.CompareTo(min) < 0)
				return min;

			if (val.CompareTo(max) > 0)
				return max;

			return val;
		}

		private static readonly CodeDomProvider CodeDomProvider = CodeDomProvider.CreateProvider("C#");
		private static readonly Regex CompilerGeneratedRegex = new Regex("^<.*?>.*$", RegexOptions.Compiled);

		public static bool IsNameObfuscated(this IFullName name) => IsNameObfuscated(name.Name);

		public static bool IsNameObfuscated(this string name)
		{
			return !CodeDomProvider.IsValidIdentifier(name) && IsCompilerGenerated(name) || !CodeDomProvider.IsValidIdentifier(name) && name.StartsWith("#=");
		}
		public static void AddRange<T>(this ICollection<T> coll, IEnumerable<T> data)
		{
			foreach (var d in data)
				coll.Add(d);
		}

		public static bool IsCompilerGenerated(this IFullName name) => IsCompilerGenerated(name.Name);

		public static bool IsCompilerGenerated(this string name) => CompilerGeneratedRegex.IsMatch(name);

		public static bool IsEazInternalName(this IFullName name) => IsEazInternalName(name.Name);

		public static bool IsEazInternalName(this string name) => name.StartsWith("#=q");

		public static bool IsEazInternalNameRecursive(this TypeDef type)
		{
			return type.IsEazInternalName() || type.DeclaringType != null && type.DeclaringType.IsEazInternalNameRecursive();
		}
		public static bool IsSystemType(this IMethodDefOrRef method) => method.DeclaringType?.Namespace != null && (method.DeclaringType?.Namespace == "System" || method.DeclaringType.Namespace.StartsWith("System."));
		public static bool IsSystemType(this Type type) => type?.Namespace != null && (type.Namespace == "System" || type.Namespace.StartsWith("System."));
		public static bool IsSystemType(this ITypeDefOrRef type) => type?.Namespace != null && (type.Namespace == "System" || type.Namespace.StartsWith("System."));

		public static string GetAssemblyGuid(this Assembly ass) => ass.GetCustomAttributes().OfType<GuidAttribute>().FirstOrDefault()?.Value;

		public static string GetMethodName(this MethodDef meth) => $"{meth.DeclaringType.Name}::{meth.Name}({String.Join(", ", meth.MethodSig.Params.Select(x => x.TypeName))})";

		public static Dictionary<TValue, TKey> Swap<TKey, TValue>(this IDictionary<TKey, TValue> dict)
		{
			var newDict = new Dictionary<TValue, TKey>(dict.Count);

			foreach (var kvp in dict)
				newDict[kvp.Value] = kvp.Key;

			return newDict;
		}
	}

	public static class Misc
	{
		public static Instruction CreateLdarg(ushort value)
		{
			switch (value)
			{
				case 0:
					return OpCodes.Ldarg_0.ToInstruction();

				case 1:
					return OpCodes.Ldarg_1.ToInstruction();

				case 2:
					return OpCodes.Ldarg_2.ToInstruction();

				case 3:
					return OpCodes.Ldarg_3.ToInstruction();

				default:
					if (value <= byte.MaxValue)
						return new Instruction(OpCodes.Ldarg_S, (byte)value);

					return new Instruction(OpCodes.Ldarg, value);
			}
		}

		public static Instruction CreateLdarga(ushort value)
		{
			if (value <= byte.MaxValue)
				return new Instruction(OpCodes.Ldarga_S, (byte)value);

			return new Instruction(OpCodes.Ldarga, value);
		}
	}

	public static class MD5Helper
	{
		public static string Compute(string filename)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(filename))
				{
					var hash = md5.ComputeHash(stream);
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}
	}

	public static class XConsole
	{
		public const string PAD = "    "; // four spaces

		public static int WriteLine()
		{
			Console.WriteLine();
			return 1;
		}

		public static int WriteLine(object value) =>
			WriteColored(value.ToString(), true);

		public static int WriteLine(string value) =>
			WriteColored(value, true);

		public static int WriteLine(string value, params object[] args) =>
			WriteColored(string.Format(value, args), true);

		public static int Write(string value) =>
			WriteColored(value);

		public static int Write(string value, params object[] args) =>
			WriteColored(string.Format(value, args));

		private static int WriteColored(string value, bool newLine = false)
		{
			var str = string.Empty;
			var prevForeColor = Console.ForegroundColor;
			var prevBackColor = Console.BackgroundColor;

			foreach (var @char in value)
			{
				if (@char >= 0x10 && @char <= 0x1F)
				{
					if (!string.IsNullOrEmpty(str))
					{
						Console.Write(str);
						str = string.Empty;
					}

					Console.ForegroundColor = (ConsoleColor)(@char & 0x0F);
				}
				else if (@char >= 0x80 && @char <= 0x8F)
				{
					if (!string.IsNullOrEmpty(str))
					{
						Console.Write(str);
						str = string.Empty;
					}

					Console.BackgroundColor = (ConsoleColor)(@char & 0x0F);
				}
				else if (@char == 0x01)
				{
					if (!string.IsNullOrEmpty(str))
					{
						Console.Write(str);
						str = string.Empty;
					}

					Console.ResetColor();
				}
				else str += @char;
			}

			if(newLine)
				Console.WriteLine(str);
			else
				Console.Write(str);

			Console.ForegroundColor = prevForeColor;
			Console.BackgroundColor = prevBackColor;

			return 1;
		}

		public static string Fatal(string value) =>
			"\u0084\u001F[F]\u0001\u0014 " + value + "\u0001";

		public static string Error(string value) =>
			"[\u0014E\u0001] " + value;

		public static string Info(string value) =>
			"\u0001[\u0019I\u0001] " + value;

		public static string Warn(string value) =>
			"[\u0016W\u0001] " + value;

		public static int PrintFatal(string value, bool newLine) =>
			WriteColored(Fatal(value), newLine);

		public static int PrintError(string value, bool newLine) =>
			WriteColored(Error(value), newLine);

		public static int PrintInfo(string value, bool newLine) =>
			WriteColored(Info(value), newLine);

		public static int PrintWarn(string value, bool newLine) =>
			WriteColored(Warn(value), newLine);

		public static int PrintFatal(string value, params object[] args) =>
			WriteLine(Fatal(value), args);

		public static int PrintError(string value, params object[] args) =>
			WriteLine(Error(value), args);

		public static int PrintInfo(string value, params object[] args) =>
			WriteLine(Info(value), args);

		public static int PrintWarn(string value, params object[] args) =>
			WriteLine(Warn(value), args);
	}
}