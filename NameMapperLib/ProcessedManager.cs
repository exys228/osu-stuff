using System.Collections.Concurrent;
using dnlib.DotNet;

using TypePair = System.Tuple<dnlib.DotNet.IType, dnlib.DotNet.IType>;
using MethodPair = System.Tuple<dnlib.DotNet.IMethod, dnlib.DotNet.IMethod>;
using FieldPair = System.Tuple<dnlib.DotNet.FieldDef, dnlib.DotNet.FieldDef>;

namespace NameMapperLib
{
	class ProcessedManager
	{
		public ConcurrentBag<TypePairInfo> AlreadyProcessedTypes { get; } = new ConcurrentBag<TypePairInfo>();
		public ConcurrentBag<MethodPair> AlreadyProcessedMethods { get; } = new ConcurrentBag<MethodPair>();
		public ConcurrentBag<FieldPair> AlreadyProcessedFields { get; } = new ConcurrentBag<FieldPair>();
		public ConcurrentHashSet<MDToken> AlreadyProcessedMDTokens { get; } = new ConcurrentHashSet<MDToken>();

		public bool Contains(IMDTokenProvider obfMdTokenProvider) =>
			AlreadyProcessedMDTokens.Contains(obfMdTokenProvider.MDToken);

		public void Add(IType cleanType, IType obfType, bool fullyProcessed = false)
		{
			if (!AlreadyProcessedMDTokens.Contains(obfType.MDToken))
			{
				AlreadyProcessedTypes.Add(new TypePairInfo(new TypePair(cleanType, obfType), fullyProcessed));
				AlreadyProcessedMDTokens.Add(obfType.MDToken);
			}
		}

		public void Add(IMethod cleanMethod, IMethod obfMethod)
		{
			if (!AlreadyProcessedMDTokens.Contains(obfMethod.MDToken))
			{
				AlreadyProcessedMethods.Add(new MethodPair(cleanMethod, obfMethod));
				AlreadyProcessedMDTokens.Add(obfMethod.MDToken);
			}
		}

		public void Add(FieldDef cleanField, FieldDef obfField)
		{
			if (!AlreadyProcessedMDTokens.Contains(obfField.MDToken))
			{
				AlreadyProcessedFields.Add(new FieldPair(cleanField, obfField));
				AlreadyProcessedMDTokens.Add(obfField.MDToken);
			}
		}
	}

	class TypePairInfo
	{
		public TypePair Types;
		public bool FullyProcessed;

		public TypePairInfo(TypePair types, bool fullyProcessed)
		{
			Types = types;
			FullyProcessed = fullyProcessed;
		}
	}
}