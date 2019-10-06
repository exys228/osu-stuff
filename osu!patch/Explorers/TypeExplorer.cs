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

		public INameProvider NameProvider { get; }

		public MethodExplorer this[string name] => FindMethod(name);

		public TypeExplorer(ModuleExplorer parent, TypeDef type, INameProvider nameProvider = null)
		{
			Parent = parent;
			Type = type;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public MethodExplorer FindMethod(string name, MethodSig sig = null)
		{
			var obfName = NameProvider.GetName(name);
			var result = (sig is null ? Type.FindMethod(obfName) : Type.FindMethod(obfName, sig))
						 ?? throw CreateUnableToFindException("method");

			return new MethodExplorer(this, result);
		}

		public MethodExplorer FindMethodRaw(string name, MethodSig sig = null)
		{
			var method = sig is null ? Type.FindMethod(name) : Type.FindMethod(name, sig);
			return new MethodExplorer(this, method ?? throw CreateUnableToFindException("method"));
		}

		public FieldDef FindField(string name) =>
			Type.FindField(NameProvider.GetName(name)) ?? throw CreateUnableToFindException("field");

		public FieldDef FindFieldRaw(string name) =>
			Type.FindField(name) ?? throw CreateUnableToFindException("field");

		private static NameProviderException CreateUnableToFindException(string whatExactly) => // field, method etc.
			new NameProviderException($"Unable to find {whatExactly} specified!");
	}
}
