using dnlib.DotNet;
using osu_patch.Naming;
using System;
using osu_patch.Exceptions;

namespace osu_patch.Explorers
{
	public class TypeExplorer
	{
		public ModuleExplorer Parent { get; }

		public TypeDef Type { get; }

		private INameProvider NameProvider { get; }

		public MethodExplorer this[string name] => Find(name);

		public TypeExplorer(ModuleExplorer parent, TypeDef type, INameProvider nameProvider = null)
		{
			Parent = parent;
			Type = type;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public MethodExplorer Find(string name)
		{
			string result = NameProvider.GetName(name);
			return new MethodExplorer(this, Type.FindMethod(result));
		}

		public MethodExplorer FindRaw(string name) =>
			new MethodExplorer(this, Type.FindMethod(name));
	}
}
