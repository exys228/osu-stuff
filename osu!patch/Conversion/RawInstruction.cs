using dnlib.DotNet.Emit;

namespace osu_patch.Conversion
{
	public class RawInstruction
	{
		public OpCode OpCode { get; set; }

		public object Operand { get; set; }

		public RawInstruction(OpCode opCode, object operand)
		{
			OpCode = opCode;
			Operand = operand;
		}
	}
}