using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using System;
using System.Collections.Generic;
using System.Linq;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace osu_patch.Lib.HookGenerator
{
	public class HookGenerator
	{
		private string _assemblyName;

		private ModuleDef _originalModule;
		private ModuleDefUser _hookModule;

		private Dictionary<string, DefInfo<TypeDef>> _processedTypes = new Dictionary<string, DefInfo<TypeDef>>();
		private List<DefInfo<MethodDef>> _processedMethods = new List<DefInfo<MethodDef>>();
		private List<DefInfo<FieldDef>> _processedFields = new List<DefInfo<FieldDef>>();

		public HookGenerator(string assemblyName, ModuleDef originalModule)
		{
			_assemblyName = assemblyName;
			_originalModule = originalModule;
		}

		public ModuleDefUser CreateHookModule()
		{
			_processedTypes.Clear();
			_processedMethods.Clear();
			_processedFields.Clear();

			_hookModule = new ModuleDefUser($"{_assemblyName}.dll", Guid.NewGuid(), new AssemblyRefUser(new AssemblyNameInfo(typeof(void).Assembly.GetName().FullName)))
			{
				Kind = ModuleKind.Dll,
				RuntimeVersion = MDHeaderRuntimeVersion.MS_CLR_40
			};

			var ass = new AssemblyDefUser(_assemblyName, _originalModule.Assembly.Version);
			ass.Modules.Add(_hookModule); // ??????? wtf but this is needed lmao

			// -- Add custom attribute for identifying

			var attr = new TypeDefUser("OsuHookAssemblyAttribute", _hookModule.Import(typeof(Attribute)));
			var attrCtor = new MethodDefUser(".ctor", MethodSig.CreateInstance(_hookModule.CorLibTypes.Void), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			attrCtor.Body = new CilBody();
			attrCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
			attr.Methods.Add(attrCtor);

			_hookModule.Types.Add(attr);
			ass.CustomAttributes.Add(new CustomAttribute(attrCtor));

			// --

			foreach (var typeDef in _originalModule.Types)
				CreateRawTree(typeDef);

			PopulateEverythingWithData();
			return _hookModule;
		}

		private void CreateRawTree(TypeDef originalTypeDef, TypeDef parent = null)
		{
			if (originalTypeDef.IsNameObfuscated())
				return;

			var nameSpace = "";

			if (parent is null) // not nested
				nameSpace = originalTypeDef.Namespace;

			var newType = new TypeDefUser(nameSpace, originalTypeDef.Name, null)
			{
				Attributes = originalTypeDef.Attributes.ConvertToHookAttributes()
			};

			// Generate dummy methods

			foreach (var originalMethod in originalTypeDef.Methods.Where(t => !t.IsNameObfuscated()))
			{
				var newMethod = new MethodDefUser(originalMethod.Name, MethodSig.CreateInstance(_hookModule.CorLibTypes.Void), originalMethod.Attributes.ConvertToHookAttributes());

				if (originalMethod.HasBody)
				{
					newMethod.Body = new CilBody();
					newMethod.Body.Instructions.Insert(0, new[]
					{
						Instruction.Create(OpCodes.Ldstr, "This is a dummy method and should NOT be used like this!"),
						Instruction.Create(OpCodes.Newobj, _hookModule.CreateMethodRef(false, typeof(Exception), ".ctor", typeof(void), typeof(string))),
						Instruction.Create(OpCodes.Throw)
					});
				}

				newType.Methods.Add(newMethod);
				_processedMethods.Add(new DefInfo<MethodDef>(newMethod, originalMethod));
			}

			// Generate dummy fields

			foreach (var originalField in originalTypeDef.Fields.Where(t => !t.IsNameObfuscated()))
			{
				var newField = new FieldDefUser(originalField.Name, new FieldSig(_hookModule.CorLibTypes.Object), originalField.Attributes.ConvertToHookAttributes());
				newType.Fields.Add(newField);
				_processedFields.Add(new DefInfo<FieldDef>(newField, originalField));
			}

			if (parent is null)
				_hookModule.Types.Add(newType);
			else
				parent.NestedTypes.Add(newType);

			_processedTypes[originalTypeDef.FullName] = new DefInfo<TypeDef>(newType, originalTypeDef);

			// --

			foreach (var type in originalTypeDef.NestedTypes)
				CreateRawTree(type, newType);
		}

		private void PopulateEverythingWithData()
		{
			// Populate types
			foreach (var kvp in _processedTypes)
			{
				var originalTypeDef = kvp.Value.OriginalDef;
				var hookTypeDef = kvp.Value.HookDef;

				if (originalTypeDef.BaseType != null) // original has basetype && original's basetype is already processed
					hookTypeDef.BaseType = FindHookTypeDef(originalTypeDef.BaseType);

				if (originalTypeDef.HasInterfaces)
				{
					foreach (var originalInterface in originalTypeDef.Interfaces)
						hookTypeDef.Interfaces.Add(new InterfaceImplUser(FindHookTypeDef(originalInterface.Interface)));

					hookTypeDef.BaseType = FindHookTypeDef(originalTypeDef.BaseType);
				}

				if (originalTypeDef.HasGenericParameters)
					foreach (var originalParam in originalTypeDef.GenericParameters)
						hookTypeDef.GenericParameters.Add(new GenericParamUser(originalParam.Number, GenericParamAttributes.NoSpecialConstraint, originalParam.Name));
			}

			// Populate fields
			foreach (var kvp in _processedFields)
				if (kvp.OriginalDef.FieldType != null)
					kvp.HookDef.FieldType = FindHookTypeSig(kvp.OriginalDef.FieldType);

			// Populate methods
			foreach (var kvp in _processedMethods)
			{
				var originalMethodDef = kvp.OriginalDef;
				var hookMethodDef = kvp.HookDef;

				hookMethodDef.MethodSig = CreateHookMethodSig(originalMethodDef.MethodSig);

				if (originalMethodDef.HasParamDefs)
				{
					var originalParamDefs = originalMethodDef.ParamDefs;
					var hookParamDefs = hookMethodDef.ParamDefs;

					foreach (var originalParam in originalParamDefs)
						hookParamDefs.Add(new ParamDefUser(originalParam.Name, originalParam.Sequence, originalParam.Attributes));
				}

				if (originalMethodDef.HasGenericParameters)
					foreach (var originalParam in originalMethodDef.GenericParameters)
						hookMethodDef.GenericParameters.Add(new GenericParamUser(originalParam.Number, GenericParamAttributes.NoSpecialConstraint, originalParam.Name));
			}
		}

		private MethodSig CreateHookMethodSig(MethodSig originalSig)
		{
			return new MethodSig(originalSig.CallingConvention,
								 originalSig.GenParamCount,
								 FindHookTypeSig(originalSig.RetType),
								 originalSig.Params.Select(FindHookTypeSig).ToArray());
		}

		private ITypeDefOrRef FindHookTypeDef(ITypeDefOrRef originalDef) =>
			FindHookTypeSig(originalDef.ToTypeSig()).ToTypeDefOrRef();

		private TypeSig FindHookTypeSig(TypeSig originalSig)
		{
			if (originalSig is null)
				return null;

			if (originalSig is GenericVar originalGenVar) // generic var in type
				return new GenericVar(originalGenVar.Number);

			if (originalSig is GenericMVar originalGenMVar) // generic var in method
				return new GenericMVar(originalGenMVar.Number);

			if (originalSig is ByRefSig originalRefSig) // reference parameter (&)
				return new ByRefSig(FindHookTypeSig(originalRefSig.Next));

			if (originalSig is SZArraySig originalSzArray) // one-dimensional array
				return new SZArraySig(FindHookTypeSig(originalSzArray.Next));

			if (originalSig is ArraySig originalArray) // multi-dimensional array
				return new ArraySig(FindHookTypeSig(originalArray.ScopeType.ToTypeSig()), originalArray.Rank, originalArray.Sizes, originalArray.LowerBounds);

			if (originalSig is GenericInstSig originalGenSig) // [!] is generic
			{
				var hookType = FindHookTypeDefRaw(originalGenSig.ScopeType);
				var hookArgSigList = new List<TypeSig>();

				int idx = 0;

				foreach (var originalArg in originalGenSig.GenericArguments)
				{
					hookArgSigList.Add(originalArg.ElementType == ElementType.Var ? new GenericVar(idx) : FindHookTypeSig(originalArg));
					idx++;
				}

				return new GenericInstSig(new ClassSig(hookType), hookArgSigList);
			}

			return FindHookTypeSigRaw(originalSig);
		}

		private TypeSig FindHookTypeSigRaw(TypeSig originalSig) =>
			FindHookTypeDefRaw(originalSig.ToTypeDefOrRef()).ToTypeSig();

		private ITypeDefOrRef FindHookTypeDefRaw(ITypeDefOrRef originalDef)
		{
			if (originalDef.IsSystemType() || originalDef.DefinitionAssembly.FullName != _originalModule.Assembly.FullName) // System types and external dependencies (OpenTK, etc)
				return _hookModule.Import(originalDef).ScopeType;

			if (_processedTypes.TryGetValue(originalDef.ScopeType.FullName, out var defInfo)) // Internal hook type (already added)
				return defInfo.HookDef;

			// TODO: Some kind of message? This shouldn't happen until type is eaz type
			return _hookModule.CorLibTypes.Object.TypeDefOrRef;
		}

		private class DefInfo<T> where T : IMemberDef
		{
			public T HookDef { get; }

			public T OriginalDef { get; }

			public DefInfo(T hookDef, T originalDef)
			{
				HookDef = hookDef;
				OriginalDef = originalDef;
			}
		}
	}

	static class HookAssGenExtensions
	{
		public static TypeAttributes ConvertToHookAttributes(this TypeAttributes originalAttrs)
		{
			var newAttrs = originalAttrs;

			if (originalAttrs.HasFlag(TypeAttributes.NestedFamily) || originalAttrs.HasFlag(TypeAttributes.NestedPrivate))
			{
				newAttrs &= ~(TypeAttributes.NestedFamORAssem | TypeAttributes.NestedPrivate);
				newAttrs |= TypeAttributes.NestedPublic;
			}
			else if(!originalAttrs.HasFlag(TypeAttributes.NestedPublic))
				newAttrs |= TypeAttributes.Public;

			return newAttrs;
		}

		public static MethodAttributes ConvertToHookAttributes(this MethodAttributes originalAttrs)
		{
			var newAttrs = originalAttrs;
			newAttrs &= ~(MethodAttributes.Assembly | MethodAttributes.Private);
			newAttrs |= MethodAttributes.Public;
			return newAttrs;
		}

		public static FieldAttributes ConvertToHookAttributes(this FieldAttributes originalAttrs)
		{
			var newAttrs = originalAttrs;
			newAttrs &= ~(FieldAttributes.Assembly | FieldAttributes.Private);
			newAttrs |= FieldAttributes.Public;
			return newAttrs;
		}
	}
}