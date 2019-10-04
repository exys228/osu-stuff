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

		public MethodExplorer this[string name] => FindMethod(name);

		public TypeExplorer(ModuleExplorer parent, TypeDef type, INameProvider nameProvider = null)
		{
			Parent = parent;
			Type = type;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public MethodExplorer FindMethod(string name)
		{
			var result = Type.FindMethod(NameProvider.GetName(name));

			if (result is null)
				throw CreateUnableToFindException("method");

			return new MethodExplorer(this, result);
		}

		public MethodExplorer FindMethodRaw(string name) =>
			new MethodExplorer(this, Type.FindMethod(name) ?? throw CreateUnableToFindException("method"));

		public FieldDef FindField(string name)
		{
			var result = Type.FindField(NameProvider.GetName(name));

			if (result is null)
				throw CreateUnableToFindException("field");

			return result;
		}

		public FieldDef FindFieldRaw(string name) =>
			Type.FindField(name) ?? throw CreateUnableToFindException("field");

		private static NameProviderException CreateUnableToFindException(string whatExactly) => // field, method etc.
			new NameProviderException($"Unable to find {whatExactly} specified!");
	}
}
