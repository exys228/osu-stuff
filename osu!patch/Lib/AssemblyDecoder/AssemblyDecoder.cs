using System.Collections.Generic;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using EazDecodeLib;

namespace osu_patch.Lib.AssemblyDecoder
{
	public class AssemblyDecoder
	{
		private static readonly Regex _regexObfuscated = new Regex("^#=[a-zA-Z0-9_$]+={0,2}$", RegexOptions.Compiled);
		private readonly Dictionary<string, string> _namePair = new Dictionary<string, string>();
		private readonly CryptoHelper _crypto;

		public AssemblyDecoder(CryptoHelper crypto)
		{
			_crypto = crypto;
		}

		public void Process(ModuleDef def)
		{
			DecodeRecursive(def.Types);
		}

		public Dictionary<string, string> GetNamePair() => _namePair;

		private void DecodeRecursive(IEnumerable<IFullName> members)
		{
			foreach (var fullName in members)
			{
				switch (fullName)
				{
					case TypeDef t:
						DecodeSingle(t);

						foreach (var generic in t.GenericParameters)
							DecodeSingle(generic);

						DecodeRecursive(t.Events);
						DecodeRecursive(t.Fields);
						DecodeRecursive(t.Methods);
						DecodeRecursive(t.NestedTypes);
						DecodeRecursive(t.Properties);

						foreach (var impl in t.Interfaces)
							DecodeSingle(impl.Interface);
						break;
					case MethodDef m:
						DecodeSingle(m);

						foreach (var generic in m.GenericParameters)
							DecodeSingle(generic);
						foreach (var param in m.Parameters)
							DecodeSingle(param);
						break;
					case FieldDef _:
					case PropertyDef _:
					case EventDef _:
						DecodeSingle(fullName);
						break;
					default:
						DecodeSingle(fullName);
						break;
				}
			}
		}

		private void DecodeSingle(IFullName param)
		{
			if (!_regexObfuscated.IsMatch(param.Name)) 
				return;

			var decrypted = _crypto.Decrypt(param.Name);

			if (!_namePair.TryGetValue(decrypted, out _))
				_namePair.Add(decrypted, param.Name);

			if (param is TypeSpec spec)
				DecodeSingle(spec.ScopeType);
		}

		private void DecodeSingle(IVariable param)
		{
			if (!_regexObfuscated.IsMatch(param.Name)) 
				return;

			var decrypted = _crypto.Decrypt(param.Name);

			if (!_namePair.TryGetValue(decrypted, out _))
				_namePair.Add(decrypted, param.Name);
		}
	}
}
