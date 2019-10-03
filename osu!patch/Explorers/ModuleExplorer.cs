using dnlib.DotNet;
using osu_patch.Naming;
using System;
using osu_patch.Exceptions;

namespace osu_patch.Explorers
{
	public class ModuleExplorer
	{
		public ModuleDefMD Module { get; }

		public INameProvider NameProvider { get; }

		public TypeExplorer this[string name] => Find(name);

		public ModuleExplorer(ModuleDefMD module, INameProvider nameProvider = null)
		{
			Module = module;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public TypeExplorer Find(string name)
		{
			string result = NameProvider.GetName(name) ?? throw new ExplorerFindException($"Unable to find name: \"{name}\".");
			return new TypeExplorer(this, Module.Find(result, false), NameProvider);
		}

		public TypeExplorer FindRaw(string name) =>
			new TypeExplorer(this, Module.Find(name, false), NameProvider);
	}
}
