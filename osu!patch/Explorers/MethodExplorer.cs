using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch.Naming;
using System;
using osu_patch.Editors;

namespace osu_patch.Explorers
{
	public class MethodExplorer
	{
		public TypeExplorer Parent { get; }

		public MethodDef Method { get; }

		public MethodEditor Editor { get; }

		public CilBody Body { get; }

		public Instruction this[int index]
		{
			get => Body.Instructions[index];
			set
			{
				if (index < 0)
					throw new IndexOutOfRangeException("Index is lower than zero!");

				if (index >= Body.Instructions.Count)
					throw new IndexOutOfRangeException("Index is outside the bounds of array.");

				Body.Instructions[index] = value;
			}
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
