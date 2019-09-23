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

		public MethodEditor(MethodExplorer parent)
		{
			Parent = parent;
			Body = Parent.Method.Body;
			Position = 0;
		}

		/// <summary>
		/// Clamps position between 0 and <see cref="Body"/>.Instructions.Count - 1 (last instruction).
		/// </summary>
		public void ClampPosition() =>
			_position = _position.Clamp(0, Body.Instructions.Count - 1); // TODO what if count is 0

		/// <summary>
		/// Locate given OpCode signature in <see cref="Body"/>.
		/// </summary>
		/// <param name="signature">The signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		/// <returns>Position of found signature</returns>
		public int Locate(IList<OpCode> signature, bool setPosition = true) =>
			LocateAt(0, signature, setPosition);

		/// <summary>
		/// Locate given OpCode signature in method body starting from given index.
		/// </summary>
		/// <param name="index">Index to start from</param>
		/// <param name="signature">The signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		/// <returns>Position of found signature</returns>
		public int LocateAt(int index, IList<OpCode> signature, bool setPosition = true)
		{
			if (signature == null || signature.Count == 0)
				throw new ArgumentException("Signature is null or empty.");

			for (int i = index; i < Body.Instructions.Count; i++)
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

		/// <summary>
		/// Insert given instruction in method body at <see cref="Position"/>.
		/// </summary>
		public void Insert(Instruction ins, InsertMode mode = InsertMode.Add) =>
			InsertAt(_position, ins, mode);

		/// <summary>
		/// Insert given instruction in method body at given index.
		/// </summary>
		public void InsertAt(int index, Instruction ins, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					Body.Instructions.Insert(index, ins);
					break;

				case InsertMode.Overwrite:
					Body.Instructions[index] = ins;
					break;
			}
		}

		/// <summary>
		/// Insert given instruction list at <see cref="Position"/>.
		/// </summary>
		public void Insert(IList<Instruction> insList, InsertMode mode = InsertMode.Add) =>
			InsertAt(_position, insList, mode);

		/// <summary>
		/// Insert given instruction list at given index.
		/// </summary>
		public void InsertAt(int index, IList<Instruction> insList, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					foreach (var ins in insList.Reverse())
						Body.Instructions.Insert(index, ins);

					break;

				case InsertMode.Overwrite:
					int i = 0;

					while (i < insList.Count && index + i < Body.Instructions.Count) // overwrite existing
						Body.Instructions[index + i] = insList[i++];

					while(i < insList.Count) // add at end if something is left
						Body.Instructions.Add(insList[i++]);

					break;
			}
		}

		public void Replace(int count, IList<Instruction> insList) =>
			ReplaceAt(_position, count, insList);

		public void ReplaceAt(int index, int count, IList<Instruction> insList)
		{
			throw new NotImplementedException(); // todo ok я хочу спать и завтра в военкомат поэтому благополучно посылаю нахуй, кхмкхмкхм на завтра. кстати это первый раз за долгое время когда я пишу комментарий на русском но всё равно никто не заметит так что похуй
		}

		/// <summary>
		/// Find next occurence of given OpCode starting from <see cref="Position"/>.
		/// </summary>
		/// <param name="opCode">OpCode</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public int Next(OpCode opCode, bool setPosition = true)
		{
			for(int i = _position; i < Body.Instructions.Count; i++)
				if (Body.Instructions[i].OpCode == opCode)
					return setPosition ? Position = i : i;

			throw new Exception("Unable to locate given OpCode.");
		}

		/// <summary>
		/// Remove instruction(s) at <see cref="Position"/>.
		/// Use <see cref="Nop"/> to preserve destination param of branch-aimed OpCodes.
		/// </summary>
		public void Remove(int count = 1) =>
			RemoveAt(_position, count);

		/// <summary>
		/// Remove instuction(s) at given <see cref="index"/>.
		/// Use <see cref="NopAt"/> to preserve destination param of branch-aimed OpCodes.
		/// </summary>
		public void RemoveAt(int index, int count = 1)
		{
			for (int i = 0; i < count && index < Body.Instructions.Count; i++)
				Body.Instructions.RemoveAt(index);

			ClampPosition();
		}

		/// <summary>
		/// Nop instruction(s) at <see cref="Position"/>.
		/// </summary>
		public void Nop(int count = 1) =>
			NopAt(_position, count);

		/// <summary>
		/// Nop instruction(s) at given index.
		/// </summary>
		public void NopAt(int index, int count = 1)
		{
			for (int i = 0; i < count && index + i < Body.Instructions.Count; i++)
				Body.Instructions[index + i].OpCode = OpCodes.Nop;
		}

		/// <summary>
		/// Locate and remove given OpCode signature.
		/// Use <see cref="LocateAndNop"/> to preserve destination param of branch-aimed OpCodes.
		/// </summary>
		/// <param name="signature">Signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public void LocateAndRemove(IList<OpCode> signature, bool setPosition = false) =>
			RemoveAt(Locate(signature, setPosition), signature.Count);

		/// <summary>
		/// Locate and nop given OpCode signature.
		/// </summary>
		/// <param name="signature">Signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public void LocateAndNop(IList<OpCode> signature, bool setPosition = false) =>
			NopAt(Locate(signature, setPosition), signature.Count);

		/// <summary>
		/// Add given instruction at the end of method body.
		/// </summary>
		/// <param name="ins"></param>
		public void Add(Instruction ins) => Body.Instructions.Add(ins);
	}

	public enum InsertMode
	{
		Add,
		Overwrite
	}
}
