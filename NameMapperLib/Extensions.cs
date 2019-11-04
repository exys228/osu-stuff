using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace NameMapperLib
{
	static class Extensions
	{
		/// <summary>
		/// Group by opcodes and exclude groups that have more than one method
		/// </summary>
		/// <param name="methods"></param>
		/// <returns>List of unique MethodDefs</returns>
		public static List<MethodDef> ExcludeMethodsDuplicatesByOpcodes(this IList<MethodDef> methods) => methods.GroupBy(m => m.Body?.Instructions.Select(i => i.OpCode.Code)).Where(g => g.Count() == 1).Select(g => g.FirstOrDefault()).ToList();

		public static bool IsFromModule(this IType type, NameMapper mapperInstance) => type.DefinitionAssembly == mapperInstance.CleanModule.Assembly || type.DefinitionAssembly == mapperInstance.ObfModule.Assembly;

		public static bool IsFromModule(this IMethod method, NameMapper mapperInstance) => method.DeclaringType.IsFromModule(mapperInstance);

		public static bool IsFromModule(this FieldDef fieldDef, NameMapper mapperInstance) => fieldDef.Module.Assembly == mapperInstance.CleanModule.Assembly || fieldDef.Module.Assembly == mapperInstance.ObfModule.Assembly;

		public static int CountTypes(this ModuleDef module) => module.CountTypes(x => true);

		public static int CountTypes(this ModuleDef module, Predicate<IFullName> predicate)
		{
			var count = 0;

			void Count(TypeDef type)
			{
				foreach (var nestedType in type.NestedTypes)
					if (predicate(type))
						Count(nestedType);

				count++;
			}

			foreach (var type in module.Types)
				if (predicate(type))
					Count(type);

			return count;
		}
	}
}
