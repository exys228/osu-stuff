using dnlib.DotNet;
using osu_patch.Naming;
using System;

namespace osu_patch.Explorers
{
	class ModuleExplorer
	{
		public ModuleDefMD Module { get; }

		public INameProvider NameProvider { get; }

		public TypeExplorer this[string name] => Find(name);

		public ModuleExplorer(ModuleDefMD module, INameProvider nameProvider = null)
		{
			Module = module;
			NameProvider = nameProvider ?? DefaultNameProvider.Instance;
		}

		public TypeExplorer Find(string name)
		{
			string result = NameProvider.GetName(name) ?? throw new Exception($"ModuleExplorer: Unable to find name: \"{name}\".");
			return new TypeExplorer(this, Module.Find(result, false), NameProvider);
		}

		public TypeExplorer FindRaw(string name) =>
			new TypeExplorer(this, Module.Find(name, false), NameProvider);
	}
}
