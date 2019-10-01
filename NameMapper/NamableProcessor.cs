using System;
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

		public ProcessResult ProcessTypeSig(TypeSig cleanSig, TypeSig obfSig)
		{
			if (cleanSig is null || obfSig is null)
				return ProcessResult.NullArguments;

			if (cleanSig is GenericInstSig cleanGenericSig && obfSig is GenericInstSig obfGenericSig)
			{
				if (cleanGenericSig.GenericArguments.Count == obfGenericSig.GenericArguments.Count)
					for (int i = 0; i < cleanGenericSig.GenericArguments.Count; i++)
						ProcessType(cleanGenericSig.GenericArguments[i].ScopeType, obfGenericSig.GenericArguments[i].ScopeType);
			}

			ProcessType(cleanSig.ScopeType, obfSig.ScopeType);

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessType(IType cleanType, IType obfType)
		{
			if (cleanType is null || obfType is null)
				return ProcessResult.NullArguments;

			if (cleanType is TypeSig cleanTypeSig && obfType is TypeSig obfTypeSig)
				return ProcessTypeSig(cleanTypeSig, obfTypeSig);

			if (Monitor.TryEnter(cleanType))
			{
				try
				{
					if (cleanType.IsFromModule(ParentInstance) && obfType.IsFromModule(ParentInstance))
					{
						if (AlreadyProcessedTypes.Any(x => x.Key.Item2.MDToken == obfType.MDToken))
							return ProcessResult.AlreadyProcessed;

						var cleanTypeDef = cleanType.ScopeType.ResolveTypeDef();
						var obfTypeDef = obfType.ScopeType.ResolveTypeDef();

						if (cleanTypeDef is null || obfTypeDef is null)
							return ProcessResult.FailedToResolve;

						if (obfTypeDef.NameIsObfuscated())
						{
							AddOrCheckNamePair($"{cleanTypeDef.Namespace}.{cleanTypeDef.Name}", obfTypeDef.Name);

							if (ParentInstance.DeobfuscateNames)
							{
								obfTypeDef.Namespace = cleanTypeDef.Namespace;
								obfTypeDef.Name = cleanTypeDef.Name;
							}
						}

						ProcessType(cleanTypeDef.BaseType, obfTypeDef.BaseType);

						AlreadyProcessedTypes.TryAdd(new TypePair(cleanTypeDef, obfTypeDef), false);
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

		public ProcessResult ProcessMethod(IMethod cleanMethod, IMethod obfMethod)
		{
			if (cleanMethod is null || obfMethod is null)
				return ProcessResult.NullArguments;

			if (Monitor.TryEnter(cleanMethod))
			{
				try
				{
					if (cleanMethod.IsFromModule(ParentInstance) && obfMethod.IsFromModule(ParentInstance))
					{
						if (AlreadyProcessedMethods.Any(x => x.Item2.MDToken == obfMethod.MDToken))
							return ProcessResult.AlreadyProcessed;

						var cleanMethodDef = cleanMethod.ResolveMethodDef();
						var obfMethodDef = obfMethod.ResolveMethodDef();

						if (cleanMethodDef is null || obfMethodDef is null)
							return ProcessResult.FailedToResolve;

						if (cleanMethodDef.IsStatic != obfMethodDef.IsStatic)
							return ProcessResult.DifferentMethods;

						if (obfMethodDef.NameIsObfuscated())
						{
							AddOrCheckNamePair(cleanMethodDef.Name, obfMethodDef.Name);

							if(ParentInstance.DeobfuscateNames)
								obfMethodDef.Name = cleanMethodDef.Name;
						}

                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            ProcessType(cleanMethodDef.ReturnType, obfMethodDef.ReturnType);
                            ProcessType(cleanMethodDef.DeclaringType, obfMethodDef.DeclaringType);
                            ProcessMethodParameters(cleanMethodDef.Parameters, obfMethodDef.Parameters);
                        });

                        AlreadyProcessedMethods.Add(new MethodPair(cleanMethodDef, obfMethodDef));
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

		public ProcessResult ProcessMethodParameters(ParameterList cleanParams, ParameterList obfParams)
		{
			if (cleanParams is null || obfParams is null)
				return ProcessResult.NullArguments;

			if (cleanParams.Count == obfParams.Count)
			{
				for (int i = cleanParams.Method.IsStatic ? 0 : 1; i < cleanParams.Count; i++) // if instance (non-static) then first param is always A_0 (this)
				{
					if (obfParams[i].ParamDef.NameIsObfuscated())
					{
						AddOrCheckNamePair(cleanParams[i].Name, obfParams[i].Name);

						if (ParentInstance.DeobfuscateNames)
							obfParams[i].Name = cleanParams[i].Name; // param name don't get confused
					}

					ProcessType(cleanParams[i].Type, obfParams[i].Type);
				}
			}

			return ProcessResult.Ok;
		}

		public ProcessResult ProcessField(FieldDef cleanFieldDef, FieldDef obfFieldDef)
		{
			if (cleanFieldDef is null || obfFieldDef is null)
				return ProcessResult.NullArguments;

			if (Monitor.TryEnter(cleanFieldDef))
			{
				try
				{
					if (cleanFieldDef.IsFromModule(ParentInstance) && obfFieldDef.IsFromModule(ParentInstance))
					{
						if (AlreadyProcessedFields.Any(x => x.Item2.MDToken == obfFieldDef.MDToken))
							return ProcessResult.AlreadyProcessed;

						if (obfFieldDef.NameIsObfuscated())
						{
							AddOrCheckNamePair(cleanFieldDef.Name, obfFieldDef.Name);

							if(ParentInstance.DeobfuscateNames)
								obfFieldDef.Name = cleanFieldDef.Name;
						}

						ProcessType(cleanFieldDef.DeclaringType, obfFieldDef.DeclaringType);
						ProcessType(cleanFieldDef.FieldType.ToTypeDefOrRef(), obfFieldDef.FieldType.ToTypeDefOrRef());

						AlreadyProcessedFields.Add(new FieldPair(cleanFieldDef, obfFieldDef));
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

		private void AddOrCheckNamePair(string cleanName, string obfName)
		{
			string ret = ParentInstance.NamePairs.GetOrAdd(cleanName, obfName);

			if (ret != obfName)
				ParentInstance.Message($"W | Found duplicate (errored) name pair: \"{cleanName}\" => given \"{obfName}\"  but got  \"{ret}\".");
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
