using dnlib.DotNet;
using osu_patch.Exceptions;
using osu_patch.Naming;
using System;

namespace osu_patch.Explorers
{
	public class ModuleExplorer : IExplorerParent
	{
		public ModuleDefMD Module { get; }

		public INameProvider NameProvider { get; }

		public ICorLibTypes CorLibTypes => Module.CorLibTypes;

		public TypeExplorer this[string name] => Find(name);

		public TypeSig ImportAsTypeSig(Type type) => Module.ImportAsTypeSig(type);

		public ITypeDefOrRef Import(Type type) => Module.Import(type);

		public ModuleExplorer(ModuleDefMD module, INameProvider nameProvider = null)
		{
			Module = module;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public TypeExplorer Find(string name)
		{
			string result = NameProvider.GetName(name);

			if (result == null)
				return new TypeExplorer(this, Module.Find(name, false), NameProvider);

			return new TypeExplorer(this, Module.Find(result, false), NameProvider);
		}

		public IExplorerParent GetParent() => null;
	}
}
