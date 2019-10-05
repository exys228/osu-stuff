using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StringFixerMini
{
	public static class StringFixer
	{
		private static bool _patched;

		public static void Fix(ModuleDef module, Assembly assembly)
		{
			if (!_patched)
			{
				Harmony.Patch();
				_patched = true;
			}

			var decrypterMethod = GetMethodsRecursive(module).SingleOrDefault(CanBeStringMethod);

			if (decrypterMethod is null)
				throw new StringFixerException("Could not find decrypter method");

			//a dictionary to cache all strings
			var dictionary = new Dictionary<int, string>();

			//get the decrypter method in a way in which we can invoke it
			var decrypter = FindMethod(assembly, decrypterMethod, new[] { typeof(int) }) ?? throw new StringFixerException("Couldn't find decrypter method through reflection");

			//store it so we can use it in the stacktrace patch
			Harmony.PatchStackTraceGetMethod.MethodToReplace = decrypter; // not sure if it's needed?

			//for every method with a body...
			foreach (MethodDef meth in GetMethodsRecursive(module).Where(a => a.HasBody && a.Body.HasInstructions))
			{
				if (meth.IsEazInternalName() || meth.DeclaringType != null && meth.DeclaringType.IsEazInternal())
					continue;

				//.. and every instruction (starting at the second one) ...
				for (int i = 1; i < meth.Body.Instructions.Count; i++)
				{
					//get this instruction and the previous
					var prev = meth.Body.Instructions[i - 1];
					var curr = meth.Body.Instructions[i];

					//if they invoke the string decrypter method with an int parameter
					if (prev.IsLdcI4() && curr.Operand != null && curr.Operand is MethodDef md && md.MDToken == decrypterMethod.MDToken)
					{
						//get the int parameter, and get the resulting string from either cache or invoking the decrypter method
						int val = prev.GetLdcI4Value();
						if (!dictionary.ContainsKey(val))
							dictionary[val] = (string)decrypter.Invoke(null, new object[] { val });

						// check if str == .ctor due to eaz using string decrypter to call constructors
						// if (dictionary[val] == ".ctor"/* && Flags.VirtFix*/) continue;

						//replace the instructions with the string

						prev.OpCode = OpCodes.Nop;
						curr.OpCode = OpCodes.Ldstr;
						curr.Operand = dictionary[val];
					}
				}
			}
		}

		private static bool CanBeStringMethod(MethodDef method)
		{
			//internal and static
			if (!method.IsStatic || !method.IsAssembly)
				return false;

			//takes int, returns string
			if (method.MethodSig.ToString() != "System.String (System.Int32)")
				return false;

			//actually a proper method, not abstract or from an interface
			if (!method.HasBody || !method.Body.HasInstructions)
				return false;

			//calls the second resolve method (used if string isn't in cache)
			if (!method.Body.Instructions.Any(a => a.OpCode.Code == Code.Call && a.Operand is MethodDef m
												  && m.MethodSig.ToString() == "System.String (System.Int32,System.Boolean)"))
				return false;

			//is not private or public
			if (method.IsPrivate || method.IsPublic)
				return false;


			return true;
		}

		private static IEnumerable<MethodDef> GetMethodsRecursive(ModuleDef t) => t.Types.SelectMany(GetMethodsRecursive);

		private static IEnumerable<MethodDef> GetMethodsRecursive(TypeDef type)
		{
			//return all methods in this type
			foreach (MethodDef m in type.Methods)
				yield return m;

			//go through nested types
			foreach (TypeDef t in type.NestedTypes)
				foreach (MethodDef m in GetMethodsRecursive(t))
					yield return m;
		}

		private static MethodInfo FindMethod(System.Reflection.Assembly ass, MethodDef meth, Type[] args)
		{
			var flags = BindingFlags.Default;
			flags |= meth.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
			flags |= meth.IsStatic ? BindingFlags.Static : BindingFlags.Instance;

			//BUG: this can fail
			Type type = ass.GetType(meth.DeclaringType.ReflectionFullName);
			return type?.GetMethod(meth.Name, flags, null, args, null);
		}

		private static bool IsEazInternalName(this IFullName name) => name.Name.StartsWith("#=q");

		private static bool IsEazInternal(this TypeDef type)
		{
			return type.IsEazInternalName() || type.DeclaringType != null && type.DeclaringType.IsEazInternal();
		}
	}
}
