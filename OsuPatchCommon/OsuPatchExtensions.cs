using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace OsuPatchCommon
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

		public static bool IsNameObfuscated(this IFullName name) => name.Name.StartsWith("#=z");

		public static bool IsEazInternalName(this IFullName name) => name.Name.StartsWith("#=q");

		public static bool IsEazInternalNameRecursive(this TypeDef type)
		{
			return type.IsEazInternalName() || type.DeclaringType != null && type.DeclaringType.IsEazInternalNameRecursive();
		}

		public static bool IsSystemType(this Type type) => type.Namespace != null && (type.Namespace == "System" || type.Namespace.StartsWith("System."));

		public static bool IsSystemType(this ITypeDefOrRef type) => type.Namespace == "System" || type.Namespace.StartsWith("System.");
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
}