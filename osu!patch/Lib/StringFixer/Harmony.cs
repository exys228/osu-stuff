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

using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace osu_patch.Lib.StringFixer
{
	static class Harmony
	{
		private const string HARMONY_ID = "exys.osudeobf";
		public readonly static HarmonyLib.Harmony HarmonyInstance = new HarmonyLib.Harmony(HARMONY_ID);

		public static void Patch() =>
			HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

		public static void Unpatch() =>
			HarmonyInstance.UnpatchAll(HARMONY_ID);

		public class PatchOsuAuthLoader
		{
			public static void Prefix()
			{
				return;
			}
		}

		[HarmonyPatch(typeof(StackFrame), "GetMethod")]
		public class PatchStackTraceGetMethod
		{
			public static MethodInfo MethodToReplace;

			public static void Postfix(ref MethodBase __result)
			{
				if (__result.DeclaringType == typeof(RuntimeMethodHandle))
					__result = MethodToReplace ?? MethodBase.GetCurrentMethod();
			}
		}
	}
}
