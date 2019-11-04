using System;
using dnlib.DotNet;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using OsuPatchCommon;

using FieldPair = System.Tuple<dnlib.DotNet.FieldDef, dnlib.DotNet.FieldDef>;
using MethodPair = System.Tuple<dnlib.DotNet.IMethod, dnlib.DotNet.IMethod>;
using TypePair = System.Tuple<dnlib.DotNet.IType, dnlib.DotNet.IType>;

namespace NameMapperLib
{
	/// <summary>
	/// The deal of this class is that it only processes NAMES of things, not bodies like in NameMapper.RecurseFromMethod etc.
	/// </summary>
	class NamableProcessor
	{
		public ProcessedManager Processed { get; } = new ProcessedManager();

		public NameMapper ParentInstance { get; }

		public NamableProcessor(NameMapper parentInstance) =>
			ParentInstance = parentInstance;

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
						if (Processed.Contains(obfType))
							return ProcessResult.AlreadyProcessed;

						var cleanTypeDef = cleanType.ScopeType.ResolveTypeDef();
						var obfTypeDef = obfType.ScopeType.ResolveTypeDef();

						if (cleanTypeDef is null || obfTypeDef is null)
							return ProcessResult.FailedToResolve;

						if (obfTypeDef.IsNameObfuscated())
						{
							AddOrCheckNamePair($"{cleanTypeDef.Namespace}.{cleanTypeDef.Name}", obfTypeDef.Name);

							if (ParentInstance.DeobfuscateNames)
							{
								obfTypeDef.Namespace = cleanTypeDef.Namespace;
								obfTypeDef.Name = cleanTypeDef.Name;
							}
						}

						if (cleanTypeDef.HasGenericParameters && cleanTypeDef.GenericParameters.Count == obfTypeDef.GenericParameters.Count)
							for(int i = 0; i < cleanTypeDef.GenericParameters.Count; i++)
								obfTypeDef.GenericParameters[i].Name = cleanTypeDef.GenericParameters[i].Name;

						ProcessType(cleanTypeDef.BaseType, obfTypeDef.BaseType);

						Processed.Add(cleanTypeDef, obfTypeDef);
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

		public ProcessResult ProcessMethod(IMethod cleanMethod, IMethod obfMethod) =>
			ProcessMethod(cleanMethod?.ResolveMethodDef(), obfMethod?.ResolveMethodDef());

		public ProcessResult ProcessMethod(MethodDef cleanMethodDef, MethodDef obfMethodDef)
		{
			if (cleanMethodDef is null || obfMethodDef is null)
				return ProcessResult.NullArguments;

			if (Monitor.TryEnter(cleanMethodDef))
			{
				try
				{
					if (cleanMethodDef.IsFromModule(ParentInstance) && obfMethodDef.IsFromModule(ParentInstance))
					{
						if (Processed.Contains(obfMethodDef))
							return ProcessResult.AlreadyProcessed;

						if (cleanMethodDef.IsStatic != obfMethodDef.IsStatic)
							return ProcessResult.DifferentMethods;

						if (obfMethodDef.IsNameObfuscated())
						{
							AddOrCheckNamePair(cleanMethodDef.Name, obfMethodDef.Name);

							if (ParentInstance.DeobfuscateNames)
								obfMethodDef.Name = cleanMethodDef.Name;
						}

						ThreadPool.QueueUserWorkItem(state =>
						{
							ProcessType(cleanMethodDef.ReturnType, obfMethodDef.ReturnType);
							ProcessType(cleanMethodDef.DeclaringType, obfMethodDef.DeclaringType);
							ProcessMethodParameters(cleanMethodDef.Parameters, obfMethodDef.Parameters);
						});

						Processed.Add(cleanMethodDef, obfMethodDef);
					}
					else return ProcessResult.FrameworkType;
				}
				finally
				{
					Monitor.Exit(cleanMethodDef);
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
				for (int i = cleanParams.MethodSigIndexBase; i < cleanParams.Count; i++) // if instance (non-static) then first param is always A_0 (this)
				{
					if (obfParams[i].ParamDef.IsNameObfuscated())
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
						if (Processed.Contains(obfFieldDef))
							return ProcessResult.AlreadyProcessed;

						if (obfFieldDef.IsNameObfuscated())
						{
							AddOrCheckNamePair(cleanFieldDef.Name, obfFieldDef.Name);

							if (ParentInstance.DeobfuscateNames)
								obfFieldDef.Name = cleanFieldDef.Name;
						}

						ProcessType(cleanFieldDef.DeclaringType, obfFieldDef.DeclaringType);
						ProcessType(cleanFieldDef.FieldType.ToTypeDefOrRef(), obfFieldDef.FieldType.ToTypeDefOrRef());

						Processed.Add(cleanFieldDef, obfFieldDef);
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

	enum ProcessResult
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
