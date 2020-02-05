using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using osu_patch.Explorers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace osu_patch.Conversion
{
	public class BodyConverter
	{
		private IList<LocalVariableInfo> _locals;
		private IList<ExceptionHandlingClause> _handlers;
		private byte[] _body;
		private bool _initLocals;

		private int _position;
		private Module _patchModule;
		private MemberConverter _memberConverter;

		private bool _decreaseLdargRank;

		public CilBody Result { get; set; }

		#region Read methods
		private short ReadInt16()
		{
			var val = BitConverter.ToInt16(_body, _position);
			_position += 2;
			return val;
		}

		private ushort ReadUInt16()
		{
			var val = BitConverter.ToUInt16(_body, _position);
			_position += 2;
			return val;
		}

		private int ReadInt32()
		{
			var val = BitConverter.ToInt32(_body, _position);
			_position += 4;
			return val;
		}

		private uint ReadUInt32()
		{
			var val = BitConverter.ToUInt32(_body, _position);
			_position += 4;
			return val;
		}

		private long ReadInt64()
		{
			var val = BitConverter.ToInt64(_body, _position);
			_position += 8;
			return val;
		}

		private ulong ReadUInt64()
		{
			var val = BitConverter.ToUInt64(_body, _position);
			_position += 8;
			return val;
		}

		private float ReadSingle() // float
		{
			var val = BitConverter.ToSingle(_body, _position);
			_position += 4;
			return val;
		}

		private double ReadDouble()
		{
			var val = BitConverter.ToDouble(_body, _position);
			_position += 8;
			return val;
		}

		private sbyte ReadSByte() =>
			(sbyte)_body[_position++];

		private byte ReadByte() =>
			_body[_position++];
		#endregion

		public BodyConverter(Delegate del, ModuleExplorer osuModule) : this(del, new MemberConverter(osuModule)) { }

		public BodyConverter(Delegate del, MemberConverter memberConverter)
		{
			var methBody = del.Method.GetMethodBody() ?? throw new Exception("Unable to get method body!");

			_locals = methBody.LocalVariables;
			_handlers = methBody.ExceptionHandlingClauses;
			_body = methBody.GetILAsByteArray();
			_initLocals = methBody.InitLocals;

			_patchModule = del.Method.Module;
			_memberConverter = memberConverter;

			_decreaseLdargRank = (del.Method.Attributes & System.Reflection.MethodAttributes.Static) == 0; // not static

			Result = null;
		}

		public CilBody ToCilBody(bool forceRebuild = false)
		{
			if (Result != null && !forceRebuild)
				return Result;

			_position = 0;

			// Locals
			var newLocals = new List<Local>();
			foreach (var local in _locals)
				newLocals.Add(new Local(_memberConverter.ImportAsOsuModuleType(local.LocalType).ToTypeSig(), "", local.LocalIndex));

			// Instrs
			var newInstrs = ConvertBytesToInstructions(newLocals);

			// Handlers
			var newHandlers = new List<ExceptionHandler>();
			foreach (var handler in _handlers)
			{
				var newHandler = new ExceptionHandler((ExceptionHandlerType)handler.Flags);
				
				if(handler.Flags == ExceptionHandlingClauseOptions.Clause)
					newHandler.CatchType = _memberConverter.ImportAsOsuModuleType(handler.CatchType);

				if ((handler.Flags & ExceptionHandlingClauseOptions.Filter) != 0)
					newHandler.FilterStart = newInstrs[(uint)handler.FilterOffset];

				newHandler.HandlerStart = newInstrs[(uint)handler.HandlerOffset];
				newHandler.HandlerEnd = newInstrs[(uint)(handler.HandlerOffset + handler.HandlerLength)];
				newHandler.TryStart = newInstrs[(uint)handler.TryOffset];
				newHandler.TryEnd = newInstrs[(uint)(handler.TryOffset + handler.TryLength)];
				newHandler.HandlerType = (ExceptionHandlerType)handler.Flags;

				newHandlers.Add(newHandler);
			}

			return Result = new CilBody(_initLocals, newInstrs.Values.ToList(), newHandlers, newLocals);
		}

		private Dictionary<uint, Instruction> ConvertBytesToInstructions(IList<Local> locals)
		{
			var newInstrs = new Dictionary<uint, Instruction>(); // IL_IDX, nop
			var branchesToFill = new Dictionary<Instruction, uint>(); // branch_instr, dest
			var casesToFill = new Dictionary<Instruction, uint[]>(); // switch_instr, cases

			while (_position < _body.Length)
			{
				var newInstr = new Instruction();

				ushort opCodeByte = ReadByte();

				if (opCodeByte == 0xfe)
					opCodeByte = (ushort)(0xfe00 | ReadByte());

				var opCode = RuntimeOpCodeList.Get(opCodeByte);

				newInstr.OpCode = opCode;
				newInstr.Offset = (uint)(_position - 1);

				MDToken mdToken;

				switch (opCode.OperandType)
				{
					case OperandType.InlineBrTarget:
						newInstr.Operand = null;
						var dest = (uint)(ReadInt32() + _position);

						if (dest > newInstr.Offset)
							branchesToFill.Add(newInstr, dest);
						else
							newInstr.Operand = newInstrs[dest];

						break;

					case OperandType.InlineField:
						newInstr.Operand = _memberConverter.ResolveMemberInfo(_patchModule.ResolveField(ReadInt32()));
						break;

					case OperandType.InlineMethod:
						mdToken = new MDToken(ReadInt32());

						MemberInfo mb;

						if (mdToken.Table == Table.MemberRef)
							mb = _patchModule.ResolveMember((int)mdToken.Raw);
						else
							mb = _patchModule.ResolveMethod((int)mdToken.Raw);

						newInstr.Operand = _memberConverter.ResolveMemberInfo(mb);
						break;

					case OperandType.InlineSig:
						newInstr.Operand = null;
						_patchModule.ResolveSignature(ReadInt32());

						// BUG: Calli is not supported
						throw new Exception("Calli is not supported!");

					case OperandType.InlineTok:
						mdToken = new MDToken(ReadInt32());

						// BUG: Ldtoken is not supported
						throw new Exception("Ldtoken is not supported!");

					case OperandType.InlineType:
						newInstr.Operand = _memberConverter.ResolveMemberInfo(_patchModule.ResolveType(ReadInt32()));
						break;

					case OperandType.InlineI:
						newInstr.Operand = ReadInt32();
						break;

					case OperandType.InlineI8:
						newInstr.Operand = ReadInt64();
						break;

					case OperandType.InlineNone:
						newInstr.Operand = null;
						break;

					case OperandType.InlineR:
						newInstr.Operand = ReadDouble();
						break;

					case OperandType.InlineString:
						newInstr.Operand = _patchModule.ResolveString(ReadInt32());
						break;

					case OperandType.InlineSwitch:
						int count = ReadInt32();
						int finalPos = _position + count * sizeof(int);
						uint[] cases = new uint[count];

						for (uint i = 0; i < count; i++)
							cases[i] = (uint)(finalPos + ReadInt32());

						casesToFill.Add(newInstr, cases);
						break;

					case OperandType.InlineVar:
						newInstr.Operand = locals[ReadUInt16()];
						break;

					case OperandType.ShortInlineBrTarget:
						newInstr.Operand = null;
						var sDest = (uint)(ReadSByte() + _position);

						if (sDest > newInstr.Offset)
							branchesToFill.Add(newInstr, sDest);
						else
							newInstr.Operand = newInstrs[sDest];

						break;

					case OperandType.ShortInlineI:
						newInstr.Operand = ReadSByte();
						break;

					case OperandType.ShortInlineR:
						newInstr.Operand = ReadSingle();
						break;

					case OperandType.ShortInlineVar:
						newInstr.Operand = locals[ReadByte()];
						break;
				}

				if (_decreaseLdargRank)
					RankDownIfLdarg(newInstr);

				newInstrs.Add(newInstr.Offset, newInstr);
			}

			foreach (var branch in branchesToFill)
				branch.Key.Operand = newInstrs[branch.Value];

			foreach (var cases in casesToFill)
			{
				var newCases = new Instruction[cases.Value.Length];

				for (int i = 0; i < newCases.Length; i++)
					newCases[i] = newInstrs[cases.Value[i]];

				cases.Key.Operand = newCases;
			}

			return newInstrs;
		}

		public void RankDownIfLdarg(Instruction ins)
		{
			switch (ins.OpCode.Code)
			{
				case Code.Ldarg_0:
					throw new Exception("Can't rank down below zero!");

				case Code.Ldarg_1:
					ins.OpCode = OpCodes.Ldarg_0;
					break;

				case Code.Ldarg_2:
					ins.OpCode = OpCodes.Ldarg_1;
					break;

				case Code.Ldarg_3:
					ins.OpCode = OpCodes.Ldarg_2;
					break;

				case Code.Ldarg:
				case Code.Ldarg_S:
				case Code.Ldarga:
				case Code.Ldarga_S:
				{
					var isLdarga = ins.OpCode.Code == Code.Ldarga || ins.OpCode.Code == Code.Ldarga_S;
					var newValue = (ushort)ins.Operand - 1;

					if (newValue < 0)
						throw new Exception("Can't rank down below zero!");

					Instruction newIns;

					if(isLdarga)
						newIns = Misc.CreateLdarga((ushort)newValue);
					else
						newIns = Misc.CreateLdarg((ushort)newValue);
					
					ins.OpCode = newIns.OpCode;
					ins.Operand = newIns.Operand;
					break;
				}
			}
		}
	}

	public static class RuntimeOpCodeList
	{
		private static Dictionary<ushort, OpCode> _opCodes = new Dictionary<ushort, OpCode>();

		static RuntimeOpCodeList()
		{
			var fields = typeof(OpCodes).GetFields();

			for (int i = 0; i < fields.Length; i++)
			{
				var info = fields[i];

				if (info.FieldType == typeof(OpCode))
				{
					var opCode = (OpCode)info.GetValue(null);
					var opCodeValue = opCode.Value;

					_opCodes.Add((ushort)opCodeValue, opCode);
				}
			}
		}

		public static OpCode Get(ushort id) =>
			_opCodes[id];
	}
}