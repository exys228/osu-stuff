using dnlib.DotNet;
using System.Collections.Concurrent;
using System.Diagnostics;

using TypePair = osu_patch.Lib.NameMapper.PairInfo<dnlib.DotNet.IType>;
using MethodPair = osu_patch.Lib.NameMapper.PairInfo<dnlib.DotNet.IMethod>;
using FieldPair = osu_patch.Lib.NameMapper.PairInfo<dnlib.DotNet.FieldDef>;

namespace osu_patch.Lib.NameMapper
{
	class ProcessedManager
	{
		public ConcurrentBag<TypePair> Types { get; } = new ConcurrentBag<TypePair>();

		public ConcurrentBag<MethodPair> Methods { get; } = new ConcurrentBag<MethodPair>();

		public ConcurrentBag<FieldPair> Fields { get; } = new ConcurrentBag<FieldPair>();

		public ConcurrentHashSet<MDToken> MDTokens { get; } = new ConcurrentHashSet<MDToken>();

		public bool Contains(IMDTokenProvider obfMdTokenProvider) =>
			MDTokens.Contains(obfMdTokenProvider.MDToken);

		public void Add(IType cleanType, IType obfType, bool fullyProcessed = false)
		{
			if (!MDTokens.Contains(obfType.MDToken))
			{
				Types.Add(new TypePair(cleanType, obfType, fullyProcessed));
				MDTokens.Add(obfType.MDToken);
			}
		}

		public void Add(IMethod cleanMethod, IMethod obfMethod)
		{
			if (!MDTokens.Contains(obfMethod.MDToken))
			{
				Methods.Add(new MethodPair(cleanMethod, obfMethod));
				MDTokens.Add(obfMethod.MDToken);
			}
		}

		public void Add(FieldDef cleanField, FieldDef obfField)
		{
			if (!MDTokens.Contains(obfField.MDToken))
			{
				Fields.Add(new FieldPair(cleanField, obfField));
				MDTokens.Add(obfField.MDToken);
			}
		}
	}

	[DebuggerDisplay("{Clean.FullName}   ||   {Obfuscated.FullName}")] // BUG: will be fkd up for methods cuz return types and args are included in FullName
	class PairInfo<T> where T : IFullName, IMDTokenProvider
	{
		public readonly T Clean;
		public readonly T Obfuscated;
		public bool IsFullyProcessed;

		public PairInfo(T clean, T obf, bool isFullyProcessed = false)
		{
			Clean = clean;
			Obfuscated = obf;
			IsFullyProcessed = isFullyProcessed;
		}
	}
}