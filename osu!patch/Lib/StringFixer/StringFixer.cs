/* MIT License
 *
 * Copyright (c) 2017 HoLLy
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace osu_patch.Lib.StringFixer
{
	public static class StringFixer
	{
		public static void Fix(ModuleDef module, Assembly assembly)
		{
			Harmony.Patch();

			var decrypterMethod = GetMethodsRecursive(module).SingleOrDefault(CanBeStringMethod);

			if (decrypterMethod is null)
				throw new StringFixerException("Could not find decrypter method");

			//a dictionary to cache all strings
			var dictionary = new Dictionary<int, string>();

			// random method to get GameBase type
			var targetFramerateSig = new List<OpCode>
			{
				OpCodes.Ldsfld,
				OpCodes.Callvirt,
				OpCodes.Ldc_I4_1,
				OpCodes.Bne_Un_S,
				OpCodes.Ldc_R8,
				OpCodes.Ret,
				OpCodes.Call,
				OpCodes.Stloc_0,
				OpCodes.Ldsfld
			};

			TypeDef gameBase = null;

			foreach (var type in module.GetTypes())
			{
				foreach (var meth in type.Methods)
				{
					if (meth.HasBody && !meth.Name.StartsWith("#=q") && meth.ReturnType == module.CorLibTypes.Double)
					{
						if (meth.Body.Instructions.Count == 48 && (double)meth.Body.Instructions[13].Operand == 960)
						{
							gameBase = type;
							break;
						}
					}
				}
			}

			var osuAuthLoaderSig = new List<OpCode>
			{
				OpCodes.Ldc_I4_1,
				OpCodes.Newarr,
				OpCodes.Stloc_0,
				OpCodes.Ldloc_0,
				OpCodes.Ldc_I4_0,
				OpCodes.Ldarg_0,
				OpCodes.Stelem_Ref,
				OpCodes.Call,
				OpCodes.Call,
				OpCodes.Ldstr,
				OpCodes.Ldloc_0,
				OpCodes.Call,
				OpCodes.Ret
			};


			var count = 0;

			MethodDef osuAuthLoaderMethod = null;

			// find OsuAuthLoader method
			foreach (var meth in gameBase.Methods)
			{
				if (meth.HasBody)
				{
					foreach (var instr in meth.Body.Instructions)
					{
						foreach (var authOpCode in osuAuthLoaderSig)
						{
							if (instr.OpCode == authOpCode)
								count++;
							if (count == 13)
							{
								osuAuthLoaderMethod = meth;
								break;
							}
						}
					}
				}
			}

			// avoid OsuAuth call
			Harmony.HarmonyInstance.Patch(assembly.Modules.ToArray()[0].ResolveMethod((int)osuAuthLoaderMethod.MDToken.Raw), new HarmonyLib.HarmonyMethod(typeof(Harmony.PatchOsuAuthLoader), "Prefix"));

			//get the decrypter method in a way in which we can invoke it
			var decrypter = FindMethod(assembly, decrypterMethod, new[] { typeof(int) }) ?? throw new StringFixerException("Couldn't find decrypter method through reflection");

			//store it so we can use it in the stacktrace patch
			Harmony.PatchStackTraceGetMethod.MethodToReplace = decrypter; // not sure if it's needed?

			//for every method with a body...
			foreach (MethodDef meth in GetMethodsRecursive(module).Where(a => a.HasBody && a.Body.HasInstructions))
			{
				if (meth.IsEazInternalName() || meth.DeclaringType != null && meth.DeclaringType.IsEazInternalNameRecursive())
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

			Harmony.Unpatch();
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

		private static MethodInfo FindMethod(Assembly ass, MethodDef meth, Type[] args)
		{
			var flags = BindingFlags.Default;
			flags |= meth.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic;
			flags |= meth.IsStatic ? BindingFlags.Static : BindingFlags.Instance;

			Type type = ass.GetType(meth.DeclaringType.ReflectionFullName);
			return type?.GetMethod(meth.Name, flags, null, args, null);
		}
	}
}
