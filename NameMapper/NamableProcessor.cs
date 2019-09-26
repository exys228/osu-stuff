using dnlib.DotNet;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using FieldPair = System.Tuple<dnlib.DotNet.FieldDef, dnlib.DotNet.FieldDef>;
using MethodPair = System.Tuple<dnlib.DotNet.IMethod, dnlib.DotNet.IMethod>;
using TypePair = System.Tuple<dnlib.DotNet.IType, dnlib.DotNet.IType>;

namespace NameMapper
{
	/// <summary>
	/// The deal of this class is that it only processes NAMES of things, not bodies like in NameMapper.RecurseFromMethod etc.
	/// </summary>
	public class NamableProcessor
	{
		/// <summary>
		/// Bool - is fully processed or not. Used after recurse from Main.
		/// </summary>
		public ConcurrentDictionary<TypePair, bool> AlreadyProcessedTypes = new ConcurrentDictionary<TypePair, bool>();

		public ConcurrentBag<MethodPair> AlreadyProcessedMethods = new ConcurrentBag<MethodPair>();
		public ConcurrentBag<FieldPair> AlreadyProcessedFields = new ConcurrentBag<FieldPair>();

		public NameMapper ParentInstance { get; }

		public NamableProcessor(NameMapper parentInstance)
		{
			ParentInstance = parentInstance;
		}

		public ProcessResult ProcessTypeSig(TypeSig cleanSig, TypeSig obfuscatedSig)
		{
			if (cleanSig is null || obfuscatedSig is null)
				return ProcessResult.NullArguments;

			if (cleanSig is GenericInstSig cleanGenericSig && obfuscatedSig is GenericInstSig obfuscatedGenericSig)
			{
				if (cleanGenericSig.GenericArguments.Count == obfuscatedGenericSig.GenericArguments.Count)
					for (int i = 0; i < cleanGenericSig.GenericArguments.Count; i++)
						ProcessType(cleanGenericSig.GenericArguments[i].ScopeType, obfuscatedGenericSig.GenericArguments[i].ScopeType);
			}

			ProcessType(cleanSig.ScopeType, obfuscatedSig.ScopeType);

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessType(IType cleanType, IType obfuscatedType)
		{
			if (cleanType is null || obfuscatedType is null)
				return ProcessResult.NullArguments;

			if (cleanType is TypeSig cleanTypeSig && obfuscatedType is TypeSig obfuscatedTypeSig)
				return ProcessTypeSig(cleanTypeSig, obfuscatedTypeSig);

			if (Monitor.TryEnter(cleanType))
			{
				try
				{
					if (cleanType.IsFromModule(ParentInstance) && obfuscatedType.IsFromModule(ParentInstance))
					{
						if (!obfuscatedType.NameIsObfuscated())
							return ProcessResult.NameNotObfuscated;

						if (AlreadyProcessedTypes.Any(x => x.Key.Item2.MDToken == obfuscatedType.MDToken))
							return ProcessResult.AlreadyProcessed;

						var cleanTypeDef = cleanType.ScopeType.ResolveTypeDef();
						var obfuscatedTypeDef = obfuscatedType.ScopeType.ResolveTypeDef();

						if (cleanTypeDef is null || obfuscatedTypeDef is null)
							return ProcessResult.FailedToResolve;

						if (obfuscatedTypeDef.NameIsObfuscated())
						{
							AddOrCheckNamePair($"{cleanTypeDef.Namespace}.{cleanTypeDef.Name}", obfuscatedTypeDef.Name);

							if (ParentInstance.DeobfuscateNames)
							{
								obfuscatedTypeDef.Namespace = cleanTypeDef.Namespace;
								obfuscatedTypeDef.Name = cleanTypeDef.Name;
							}
						}

						ProcessType(cleanTypeDef.BaseType, obfuscatedTypeDef.BaseType);

						AlreadyProcessedTypes.TryAdd(new TypePair(cleanTypeDef, obfuscatedTypeDef), false);
					}
					else return ProcessResult.FrameworkType;
				}
				finally
				{
					Monitor.Exit(cleanType);
				}
			}
			else return ProcessResult.AlreadyInProcess;

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessMethod(IMethod cleanMethod, IMethod obfuscatedMethod)
		{
			if (cleanMethod is null || obfuscatedMethod is null)
				return ProcessResult.NullArguments;

			if (Monitor.TryEnter(cleanMethod))
			{
				try
				{
					if (cleanMethod.IsFromModule(ParentInstance) && obfuscatedMethod.IsFromModule(ParentInstance))
					{
						if (AlreadyProcessedMethods.Any(x => x.Item2.MDToken == obfuscatedMethod.MDToken))
							return ProcessResult.AlreadyProcessed;

						var cleanMethodDef = cleanMethod.ResolveMethodDef();
						var obfuscatedMethodDef = obfuscatedMethod.ResolveMethodDef();

						if (cleanMethodDef is null || obfuscatedMethodDef is null)
							return ProcessResult.FailedToResolve;

						if (cleanMethodDef.IsStatic != obfuscatedMethodDef.IsStatic)
							return ProcessResult.DifferentMethods;

						if (obfuscatedMethodDef.NameIsObfuscated())
						{
							AddOrCheckNamePair(cleanMethodDef.Name, obfuscatedMethodDef.Name);

							if(ParentInstance.DeobfuscateNames)
								obfuscatedMethodDef.Name = cleanMethodDef.Name;
						}

						ProcessType(cleanMethodDef.ReturnType, obfuscatedMethodDef.ReturnType);
						ProcessType(cleanMethodDef.DeclaringType, obfuscatedMethodDef.DeclaringType);
						ProcessMethodParameters(cleanMethodDef.Parameters, obfuscatedMethodDef.Parameters);

						AlreadyProcessedMethods.Add(new MethodPair(cleanMethodDef, obfuscatedMethodDef));
					}
					else return ProcessResult.FrameworkType;
				}
				finally
				{
					Monitor.Exit(cleanMethod);
				}
			}
			else return ProcessResult.AlreadyInProcess;

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessMethodParameters(ParameterList cleanParams, ParameterList obfuscatedParams)
		{
			if (cleanParams is null || obfuscatedParams is null)
				return ProcessResult.NullArguments;

			if (cleanParams.Count == obfuscatedParams.Count)
			{
				for (int i = cleanParams.Method.IsStatic ? 0 : 1; i < cleanParams.Count; i++) // if instance (non-static) then first param is always A_0 (this)
				{
					if (obfuscatedParams[i].ParamDef.NameIsObfuscated())
					{
						AddOrCheckNamePair(cleanParams[i].Name, obfuscatedParams[i].Name);

						if (ParentInstance.DeobfuscateNames)
							obfuscatedParams[i].Name = cleanParams[i].Name; // param name don't get confused
					}

					ProcessType(cleanParams[i].Type, obfuscatedParams[i].Type);
				}
			}

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessField(FieldDef cleanFieldDef, FieldDef obfuscatedFieldDef)
		{
			if (cleanFieldDef is null || obfuscatedFieldDef is null)
				return ProcessResult.NullArguments;

			if (Monitor.TryEnter(cleanFieldDef))
			{
				try
				{
					if (cleanFieldDef.IsFromModule(ParentInstance) && obfuscatedFieldDef.IsFromModule(ParentInstance))
					{
						if (AlreadyProcessedFields.Any(x => x.Item2.MDToken == obfuscatedFieldDef.MDToken))
							return ProcessResult.AlreadyProcessed;

						if (obfuscatedFieldDef.NameIsObfuscated())
						{
							AddOrCheckNamePair(cleanFieldDef.Name, obfuscatedFieldDef.Name);

							if(ParentInstance.DeobfuscateNames)
								obfuscatedFieldDef.Name = cleanFieldDef.Name;
						}

						ProcessType(cleanFieldDef.DeclaringType, obfuscatedFieldDef.DeclaringType);
						ProcessType(cleanFieldDef.FieldType.ToTypeDefOrRef(), obfuscatedFieldDef.FieldType.ToTypeDefOrRef());

						AlreadyProcessedFields.Add(new FieldPair(cleanFieldDef, obfuscatedFieldDef));
					}
					else return ProcessResult.FrameworkType;
				}
				finally
				{
					Monitor.Exit(cleanFieldDef);
				}
			}
			else return ProcessResult.AlreadyInProcess;

			return ProcessResult.Ok;
		}

		private void AddOrCheckNamePair(string cleanName, string obfuscatedName)
		{
			string ret = ParentInstance.NamePairs.GetOrAdd(cleanName, obfuscatedName);

			if (ret != obfuscatedName)
				ParentInstance.Message($"W | Found duplicate (errored) name pair: \"{cleanName}\" => given \"{obfuscatedName}\"  but got  \"{ret}\".");
		}
	}

	public enum ProcessResult
	{
		None,
		Ok,
		NullArguments,
		FailedToResolve,
		AlreadyProcessed,
		AlreadyInProcess,
		DifferentMethods,
		NameNotObfuscated,
		FrameworkType
	}
}
