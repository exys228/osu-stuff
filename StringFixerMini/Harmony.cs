using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Harmony;

namespace StringFixerMini
{ 
	static class Harmony
	{
		public static void Patch()
		{
			HarmonyInstance h = HarmonyInstance.Create("exys.osudeobf");
			h.PatchAll(Assembly.GetExecutingAssembly());
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
