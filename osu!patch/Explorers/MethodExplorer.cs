using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch.Editors;
using System;

namespace osu_patch.Explorers
{
	public class MethodExplorer : IExplorerParent
	{
		public TypeExplorer Parent { get; }

		public IExplorerParent GetParent() => Parent;

		public MethodDef Method { get; }

		public MethodEditor Editor { get; }

		public CilBody Body { get; }

		public Instruction this[int index]
		{
			get => Editor[index];
			set => Editor[index] = value;
		}

		public MethodExplorer(TypeExplorer parent, MethodDef method)
		{
			Parent = parent;
			Method = method;
			Body = Method.Body;

			if (method.HasBody && method.Body != null)
				Editor = new MethodEditor(this);
		}
	}
}
