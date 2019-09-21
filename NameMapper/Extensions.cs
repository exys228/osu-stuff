using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace NameMapper
{
    internal static class Extensions
    {
	    /// <summary>
	    /// Group by opcodes and exclude groups that have more than one method
	    /// </summary>
	    /// <param name="methods"></param>
	    /// <returns>List of unique MethodDefs</returns>
	    public static List<MethodDef> ExcludeMethodsDuplicatesByOpcodes(this IList<MethodDef> methods) => methods.GroupBy(m => m.Body?.Instructions.Select(i => i.OpCode)).Where(g => g.Count() == 1).Select(g => g.FirstOrDefault()).ToList();

	    public static bool IsFromModule(this IType type, NameMapper mapperInstance) => type.DefinitionAssembly == mapperInstance.CleanModule.Assembly || type.DefinitionAssembly == mapperInstance.ObfuscatedModule.Assembly;

	    public static bool IsFromModule(this IMethod method, NameMapper mapperInstance) => method.DeclaringType.IsFromModule(mapperInstance);

	    public static bool IsFromModule(this FieldDef fieldDef, NameMapper mapperInstance) => fieldDef.Module.Assembly == mapperInstance.CleanModule.Assembly || fieldDef.Module.Assembly == mapperInstance.ObfuscatedModule.Assembly;

	    public static bool NameIsObfuscated(this IFullName fullName) => fullName.Name.StartsWith("#=z");
	}
}
