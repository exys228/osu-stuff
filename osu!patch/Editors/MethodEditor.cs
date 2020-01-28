using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch.Conversion;
using osu_patch.Exceptions;
using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpCode = dnlib.DotNet.Emit.OpCode;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace osu_patch.Editors
{
	public class MethodEditor : IExplorerParent
	{
		public MethodExplorer Parent { get; }

		public IExplorerParent GetParent() => Parent;

		public CilBody Body { get; }

		public IList<Instruction> Instrs => Body.Instructions;

		public int Count => Instrs.Count;

		private int _position;

		/// <summary>
		/// Counts from 0, you may set index that is equals to current Count and that will just add instruction to desired position instead of inserting it, pretty cool too.
		/// </summary>
		public int Position
		{
			get => _position;
			private set
			{
				if (value < 0)
					throw new IndexOutOfRangeException("New position is lower than zero!");

				if (value > Count)
					throw new IndexOutOfRangeException("New position is outside the bounds of array.");

				_position = value;
			}
		}

		public MethodEditor(MethodExplorer parent)
		{
			Parent = parent;
			Body = parent.Method.Body;
			Position = 0;
		}

		public Instruction this[int index]
		{
			get => Body.Instructions[index];
			set
			{
				if (index < 0)
					throw new IndexOutOfRangeException("Index is lower than zero!");

				if (index >= Body.Instructions.Count)
					throw new IndexOutOfRangeException("Index is outside the bounds of array.");

				ReplaceAt(index, value);
			}
		}

		/// <summary>
		/// Clamps <see cref="Position"/> between 0 and <see cref="Body"/>.Instructions.Count - 1 (last <see cref="Instruction"/>).
		/// </summary>
		public void ClampPosition() =>
			_position = _position.Clamp(0, Math.Max(0, Count - 1));

		#region Locate
		/// <summary>
		/// Locate given <see cref="OpCode"/> <paramref name="signature"/> in <see cref="Body"/>.
		/// </summary>
		/// <param name="signature">The <paramref name="signature"/></param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		/// <returns>Position of found <paramref name="signature"/></returns>
		public int Locate(IList<OpCode> signature, bool setPosition = true) =>
			LocateAt(0, signature, setPosition);

		/// <summary>
		/// Locate given <see cref="OpCode"/> <paramref name="signature"/> in <see cref="Body"/> starting from given <paramref name="index"/>.
		/// </summary>
		/// <param name="index">Index to start from</param>
		/// <param name="signature">The <paramref name="signature"/></param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		/// <returns>Position of found signature</returns>
		public int LocateAt(int index, IList<OpCode> signature, bool setPosition = true)
		{
			if (signature == null || signature.Count == 0)
				throw new ArgumentException("Signature is null or empty.");

			for (int i = index; i < Count; i++)
			{
				var occurence = 0;

				while (i + occurence < Count)
				{
					if (Instrs[i + occurence].OpCode != signature[occurence] && signature[occurence] != null) // null is literally any opcode
						break;

					if (++occurence >= signature.Count)
						return setPosition ? Position = i : i;
				}
			}

			throw new MethodEditorLocateException("Unable to locate given signature.");
		}
		#endregion
		#region Insert
		// -- Insert an instruction

		/// <summary>
		/// Insert given <paramref name="instruction"/> in <see cref="Body"/> at <see cref="Position"/>.
		/// </summary>
		public void Insert(Instruction instruction, InsertMode mode = InsertMode.Add) =>
			InsertAt(_position, instruction, mode);

		/// <summary>
		/// Insert given <paramref name="instruction"/> in <see cref="Body"/> at given <paramref name="index"/>.
		/// </summary>
		public void InsertAt(int index, Instruction instruction, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					if (index == Count)
						Add(instruction);
					else
						Instrs.Insert(index, instruction);

					break;

				case InsertMode.Overwrite:
					ReplaceAt(index, instruction);
					break;

				default:
					return;
			}
		}

		// -- Insert IList<Instruction>

		/// <summary>
		/// Insert given instruction <paramref name="list"/> at <see cref="Position"/>.
		/// </summary>
		public void Insert(IList<Instruction> list, InsertMode mode = InsertMode.Add) =>
			InsertAt(_position, list, mode);

		/// <summary>
		/// Insert given instruction <paramref name="list"/> at given <paramref name="index"/>.
		/// </summary>
		public void InsertAt(int index, IList<Instruction> list, InsertMode mode = InsertMode.Add)
		{
			switch (mode)
			{
				case InsertMode.Add:
					foreach (var ins in list.Reverse())
					{
						if (index == Count)
							Add(ins);
						else
							Instrs.Insert(index, ins);
					}

					break;

				case InsertMode.Overwrite:
					int i = 0;

					while (i < list.Count && index + i < Count) // overwrite existing
						ReplaceAt(index, list[i++]);

					while (i < list.Count) // add at end if something is left
						Add(list[i++]);

					break;

				default:
					return;
			}
		}

		// -- Take Action's body instructions and then Insert them at specified index

		public void Insert(Action<object> action) =>
			InsertAt(_position, action);

		/// <summary>
		/// Read <paramref name="action"/>'s body as IL and insert it at specified index
		/// </summary>
		public void InsertAt(int index, Action<object> action)
		{
			throw new NotImplementedException();

			/*var info = action.GetMethodInfo();
			var body = info.GetMethodBody();
			var bodyArr = body?.GetILAsByteArray();

			var reader = new BodyConverter(action, this.GetRoot());
			var asd = reader.ToCilBody();*/
		}

		// -- Insert call to MethodDef

		public void InsertCall(MethodDef methodDef) =>
			InsertCallAt(_position, methodDef);

		public void InsertCallAt(int index, MethodDef methodDef)
		{
			var newInstrs = new List<Instruction>();

			if (!methodDef.IsStatic)
				newInstrs.Add(Instruction.Create(OpCodes.Ldarg_0));

			newInstrs.Add(Instruction.Create(methodDef.IsStatic ? OpCodes.Call : OpCodes.Callvirt, methodDef));

			if(methodDef.ReturnType != methodDef.Module.CorLibTypes.Void)
				newInstrs.Add(Instruction.Create(OpCodes.Pop));

			InsertAt(index, newInstrs);
		}
		// --
		#endregion
		#region Replace
		public void Replace(Instruction ins) =>
			ReplaceAt(_position, ins);

		public void ReplaceAt(int index, Instruction ins)
		{
			if (ins != null)
			{
				Instrs[index].OpCode = ins.OpCode;
				Instrs[index].Operand = ins.Operand;
			}
			else Instrs[index].OpCode = OpCodes.Nop;
		}

		public void Replace(int index, IList<Instruction> list) =>
			ReplaceAt(index, 1, list);

		public void ReplaceAt(int index, int count, IList<Instruction> list)
		{
			if (index + count > Count)
				throw new IndexOutOfRangeException($"Count of instructions is outside Instrs list! ({index + count} > {Count} (max))");

			var newList = new List<Instruction>(list); // because we're removing some instructions in process and list is an atomic type

			for (int i = 0; i < count; i++)
			{
				var item = newList.FirstOrDefault();

				if (item != null)
				{
					Instrs[index + i].OpCode = item.OpCode;
					Instrs[index + i].Operand = item.Operand;

					newList.RemoveAt(0);
				}
				else Instrs[index + i].OpCode = OpCodes.Nop;
			}

			if (newList.Count > 0) // insert what is left
				InsertAt(index + count, newList);
		}
		#endregion
		#region Remove
		/// <summary>
		/// Remove instruction(s) at <see cref="Position"/>.
		/// Use <see cref="Nop"/> to preserve destination param of branch-manipulating <see cref="OpCodes"/>.
		/// </summary>
		public void Remove(int count = 1) =>
			RemoveAt(_position, count);

		/// <summary>
		/// Remove instruction(s) at given <see cref="index"/>.
		/// Use <see cref="NopAt"/> to preserve destination param of branch-manipulating <see cref="OpCodes"/>.
		/// </summary>
		public void RemoveAt(int index, int count = 1)
		{
			for (int i = 0; i < count && index < Count; i++)
				Instrs.RemoveAt(index);

			ClampPosition();
		}
		#endregion
		#region Nop
		/// <summary>
		/// Nop instruction(s) at <see cref="Position"/>.
		/// </summary>
		public void Nop(int count = 1) =>
			NopAt(_position, count);

		/// <summary>
		/// Nop instruction(s) at given <paramref name="index"/>.
		/// </summary>
		public void NopAt(int index, int count = 1)
		{
			for (int i = 0; i < count && index + i < Count; i++)
				Instrs[index + i].OpCode = OpCodes.Nop;
		}
		#endregion
		#region Next
		/// <summary>
		/// Find next occurence of given <see cref="OpCode"/> starting from <see cref="Position"/>.
		/// </summary>
		/// <param name="opCode"><see cref="OpCode"/> to search for</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public int Next(OpCode opCode, bool setPosition = true) =>
			NextFrom(_position, opCode, setPosition);

		/// <summary>
		/// Find next occurence of given <see cref="OpCode"/> starting from <see cref="Position"/>.
		/// </summary>
		/// <param name="index">Index to search from</param>
		/// <param name="opCode"><see cref="OpCode"/> to search for</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public int NextFrom(int index, OpCode opCode, bool setPosition = true)
		{
			for (int i = index; i < Count; i++)
				if (Instrs[i].OpCode == opCode)
					return setPosition ? Position = i : i;

			throw new MethodEditorLocateException("Unable to locate given OpCode.");
		}
		#endregion
		#region Shortcuts
		/// <summary>
		/// Locate and remove given <see cref="OpCode"/> <paramref name="signature"/>.
		/// Use <see cref="LocateAndNop"/> to preserve destination param of branch-aimed <see cref="OpCodes"/>.
		/// </summary>
		/// <param name="signature">Signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public void LocateAndRemove(IList<OpCode> signature, bool setPosition = false) =>
			RemoveAt(Locate(signature, setPosition), signature.Count);

		/// <summary>
		/// Locate and nop given <see cref="OpCode"/> <paramref name="signature"/>.
		/// </summary>
		/// <param name="signature">Signature</param>
		/// <param name="setPosition">Update <see cref="Position"/> property or not</param>
		public void LocateAndNop(IList<OpCode> signature, bool setPosition = false) =>
			NopAt(Locate(signature, setPosition), signature.Count);
		#endregion

		/// <summary>
		/// Add given <paramref name="instruction"/> at the end of method body.
		/// </summary>
		/// <param name="instruction"></param>
		public void Add(Instruction instruction) =>
			Instrs.Add(instruction);

		/// <summary>
		/// Optimizes <see cref="Instrs"/> branches.
		/// </summary>
		public void OptimizeBranches() =>
			Body.OptimizeBranches();

		public void SimplifyBranches() =>
			Body.SimplifyBranches();
	}

	public enum InsertMode
	{
		Add,
		Overwrite
	}
}
