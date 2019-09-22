using dnlib.DotNet.Emit;
using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using de4dot.code.deobfuscators;

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

		public void ClampPosition() =>
			_position = _position.Clamp(0, Body.Instructions.Count);

		public MethodEditor(MethodExplorer parent)
		{
			Parent = parent;
			Body = Parent.Method.Body;
			Position = 0;
		}

		public int Locate(int startIndex, IList<OpCode> signature, bool setPosition = true)
		{
			if (signature == null || signature.Count == 0)
				throw new ArgumentException("Signature is null or empty.");

			for (int i = startIndex; i < Body.Instructions.Count; i++)
			{
				var occurence = 0;

				while (i + occurence < Body.Instructions.Count)
				{
					if (Body.Instructions[i + occurence].OpCode != signature[occurence] && signature[occurence] != null)
						break;

					if (++occurence >= signature.Count)
						return setPosition ? Position = i : i;
				}
			}

			throw new Exception("Unable to locate given signature.");
		}

		public int Locate(IList<OpCode> signature, bool setPosition = true) =>
			Locate(0, signature, setPosition);

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

		public void Insert(IList<Instruction> insList, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					foreach (var ins in insList.Reverse())
						Body.Instructions.Insert(_position, ins);

					break;

				case InsertMode.Overwrite:
					int i = 0;

					while (i < insList.Count && _position + i < Body.Instructions.Count) // overwrite existing
						Body.Instructions[_position + i] = insList[i++];

					while(i < insList.Count) // add at end if something is left
						Body.Instructions.Add(insList[i++]);

					break;
			}
		}

		public int Next(OpCode opCode, bool setPosition = true)
		{
			for(int i = 0; i < Body.Instructions.Count; i++)
				if (Body.Instructions[i].OpCode == opCode)
					return setPosition ? Position = i : i;

			throw new Exception("Unable to locate opcode.");
		}
			

		public void Remove(int index, int count)
		{
			for (int i = 0; i < count && index < Body.Instructions.Count; i++)
				Body.Instructions.RemoveAt(index);

			ClampPosition();
		}

		public void Remove(int count = 1) =>
			Remove(_position, count);

		public void Nop(int index, int count)
		{
			for (int i = 0; i < count && index + i < Body.Instructions.Count; i++)
				Body.Instructions[index + i].OpCode = OpCodes.Nop;
		}

		public void Nop(int count = 1) =>
			Nop(_position, count);

		public void LocateAndRemove(IList<OpCode> signature, bool setPosition = false) =>
			Remove(Locate(signature, setPosition), signature.Count);

		public void LocateAndNop(IList<OpCode> signature, bool setPosition = false) =>
			Nop(Locate(signature, setPosition), signature.Count);

		public void Add(Instruction ins) => Body.Instructions.Add(ins);
	}

	public enum InsertMode
	{
		Add,
		Overwrite
	}
}
