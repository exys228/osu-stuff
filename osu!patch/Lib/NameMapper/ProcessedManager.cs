using dnlib.DotNet;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace osu_patch.Lib.NameMapper
{
	class ProcessedManager
	{
		public ConcurrentBag<PairInfo<IType>> Types { get; } = new ConcurrentBag<PairInfo<IType>>();
		public ConcurrentBag<PairInfo<IMethod>> Methods { get; } = new ConcurrentBag<PairInfo<IMethod>>();
		public ConcurrentBag<PairInfo<FieldDef>> Fields { get; } = new ConcurrentBag<PairInfo<FieldDef>>();
		public ConcurrentHashSet<MDToken> MDTokens { get; } = new ConcurrentHashSet<MDToken>();

		public bool Contains(IMDTokenProvider obfMdTokenProvider) =>
			MDTokens.Contains(obfMdTokenProvider.MDToken);

		public void Add(IType cleanType, IType obfType, bool fullyProcessed = false)
		{
			if (!MDTokens.Contains(obfType.MDToken))
			{
				Types.Add(new PairInfo<IType>(cleanType, obfType, fullyProcessed));
				MDTokens.Add(obfType.MDToken);
			}
		}

		public void Add(IMethod cleanMethod, IMethod obfMethod)
		{
			if (!MDTokens.Contains(obfMethod.MDToken))
			{
				Methods.Add(new PairInfo<IMethod>(cleanMethod, obfMethod));
				MDTokens.Add(obfMethod.MDToken);
			}
		}

		public void Add(FieldDef cleanField, FieldDef obfField)
		{
			if (!MDTokens.Contains(obfField.MDToken))
			{
				Fields.Add(new PairInfo<FieldDef>(cleanField, obfField));
				MDTokens.Add(obfField.MDToken);
			}
		}
	}

	[DebuggerDisplay("{Clean.FullName}   ||   {Obfuscated.FullName}")]
	class PairInfo<T> where T : IFullName, IMDTokenProvider
	{
		public T Clean;
		public T Obfuscated;
		public bool FullyProcessed;

		public PairInfo(T clean, T obf, bool fullyProcessed = false)
		{
			Clean = clean;
			Obfuscated = obf;
			FullyProcessed = fullyProcessed;
		}
	}
}