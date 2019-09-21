using dnlib.DotNet;

namespace osu_patch
{
	public class OsuFindCollection
	{
		public TypeDef Type { get; private set; }
		public MethodDef Method { get; private set; }
		public bool Success { get; private set; }

		public OsuFindCollection(TypeDef type, MethodDef method, bool success)
		{
			Type = type;
			Method = method;
			Success = success;
		}
	}
}
