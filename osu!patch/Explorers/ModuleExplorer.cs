using dnlib.DotNet;
using osu_patch.Exceptions;
using osu_patch.Naming;
using System;

namespace osu_patch.Explorers
{
	public class ModuleExplorer : IExplorerParent
	{
		public ModuleDefMD Module { get; }
		public ModuleDefMD SelfModule { get; }

		public INameProvider NameProvider { get; }

		public ICorLibTypes CorLibTypes => Module.CorLibTypes;

		public TypeExplorer this[string name] => Find(name);

		public TypeSig ImportAsTypeSig(Type type) => Module.ImportAsTypeSig(type);

		public ITypeDefOrRef Import(Type type) => Module.Import(type);

		public ModuleExplorer(ModuleDefMD module, ModuleDefMD selfModule, INameProvider nameProvider = null)
		{
			Module = module;
			SelfModule = selfModule;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public TypeExplorer Find(string name)
		{
			string result = NameProvider.GetName(name);

			if (result == null)
				return new TypeExplorer(this, Module.Find(name, false), NameProvider) ?? throw new ExplorerFindException($"Unable to find name: \"{name}\".");

			return new TypeExplorer(this, Module.Find(result, false), NameProvider);
		}

		public TypeExplorer FindRaw(string name)
		{
			var type = Module.Find(name, false);

			if (type == null) 
				return null;

			return new TypeExplorer(this, type, NameProvider);
		}

		public IExplorerParent GetParent() => null;
	}
}
