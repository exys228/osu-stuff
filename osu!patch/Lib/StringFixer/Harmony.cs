using Harmony;
using System;
using System.Diagnostics;
using System.Reflection;

namespace osu_patch.Lib.StringFixer
{
	static class Harmony
	{
		private const string HARMONY_ID = "exys.osudeobf";

		private static readonly HarmonyInstance _harmony = HarmonyInstance.Create(HARMONY_ID);

		public static void Patch() =>
			_harmony.PatchAll(Assembly.GetExecutingAssembly());

		public static void Unpatch() =>
			_harmony.UnpatchAll(HARMONY_ID);

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
