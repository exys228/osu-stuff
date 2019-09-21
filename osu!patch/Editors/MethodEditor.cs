using dnlib.DotNet.Emit;
using osu_patch.Explorers;
using System;

namespace osu_patch.Editors
{
	class MethodEditor
	{
		public MethodExplorer Parent { get; }

		public CilBody Body { get; }

		private int _position;

		public int Position
		{
			get => _position;
			private set
			{
				if(value < 0)
					throw new Exception("New position is lower than zero!");

				if (value >= Body.Instructions.Count)
					throw new Exception("New position is outside the bounds of array.");

				_position = value;
			}
		}

		public MethodEditor(MethodExplorer parent)
		{
			Parent = parent;
			Body = Parent.Method.Body;
			Position = 0;
		}

		public int Locate(OpCode[] signature, bool setPosition = true)
		{
			if (signature == null || signature.Length == 0)
				throw new ArgumentException("Signature is null or empty.");

			for (int i = 0; i < Body.Instructions.Count; i++)
			{
				var occurence = 0;

				while (i + occurence < Body.Instructions.Count)
				{
					if (Body.Instructions[i + occurence].OpCode != signature[occurence] && signature[occurence] != null)
						break;

					if (++occurence >= signature.Length)
						return setPosition ? Position = i : i;
				}
			}

			throw new Exception("Unable to locate given signature.");
		}

		public void Insert(Instruction ins, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					Body.Instructions.Insert(_position, ins);
					break;

				case InsertMode.Overwrite:
					Body.Instructions[_position] = ins;
					break;
			}
		}

		public void Insert(Instruction[] insArr, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					Array.Reverse(insArr);

					foreach (var ins in insArr)
						Body.Instructions.Insert(_position, ins);

					break;

				case InsertMode.Overwrite:
					int i = 0;

					while (i < insArr.Length && _position + i < Body.Instructions.Count) // overwrite existing
						Body.Instructions[_position + i] = insArr[i++];

					while(i < insArr.Length) // add at end if something is left
						Body.Instructions.Add(insArr[i++]);

					break;
			}
		}

		public void Remove(int count = 1)
		{
			for(int i = 0; i < count && _position < Body.Instructions.Count; i++)
				Body.Instructions.RemoveAt(_position);
		}

		public void Add(Instruction ins) => Body.Instructions.Add(ins);
	}

	public enum InsertMode
	{
		Add,
		Overwrite
	}
}
