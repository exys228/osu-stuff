using dnlib.DotNet;
using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu_patch.Lib.HookGenerator;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;
using osu_patch.Extensions;
using HarmonyLib;

namespace osu_patch.Conversion
{
	public class MemberConverter
	{
		private ModuleExplorer _moduleExplorer;
		private TypeExplorer _typeExplorer;
		private Type _sourceDeclaringType;
		private const MethodAttributes MethodAccessMask =
			MethodAttributes.Private |
			MethodAttributes.FamANDAssem |
			MethodAttributes.Assembly |
			MethodAttributes.Family |
			MethodAttributes.FamORAssem |
			MethodAttributes.Public;

		private static List<string> _methodBlackList = new List<string>()
		{
			"GetEnumerator"
		};

		public MemberConverter(TypeExplorer typeExplorer, Type sourceDeclaringType = null)
		{
			_typeExplorer = typeExplorer;
			_moduleExplorer = typeExplorer.GetRoot();
			_sourceDeclaringType = sourceDeclaringType;
		}
		
		public MethodSig MethodInfoToMethodSig(MethodInfo methInfo, bool hasThis = false, bool forceStatic = false) =>
			MethodInfoToMethodSig(methInfo.ReturnType, methInfo, hasThis, forceStatic);

		public MethodSig MethodInfoToMethodSig(Type retType, MethodBase methBase, bool hasThis = false, bool forceStatic = false)
		{
			var isStatic = (forceStatic || methBase.IsStatic) && !hasThis;

			var genParamCount = methBase.ContainsGenericParameters ? methBase.GetGenericArguments().Length : 0;
			
			if (methBase is MethodInfo meth && meth.DeclaringType.IsGenericType)
			{
				var declaringType = meth.DeclaringType.GetGenericTypeDefinition();
				methBase = declaringType
					.GetMethods((BindingFlags)int.MaxValue)
					.FirstOrDefault(x => x.Name == methBase.Name && x.GetParameters().Length == methBase.GetParameters().Length);
				retType = (methBase as MethodInfo).ReturnType;
			}

			var oldParams = methBase.GetParameters();
			var hasExplicitThis = (!methBase.IsStatic && oldParams.Length > 0 && oldParams[0].ParameterType == methBase.DeclaringType) || hasThis;
			var newParams = oldParams.Skip(hasExplicitThis ? 1 : 0)
				.Select(x => ImportAsOsuModuleType(x.ParameterType).ToTypeSig())
				.ToList();

			if (isStatic)
			{
				if (genParamCount == 0)
					return MethodSig.CreateStatic(ImportAsOsuModuleType(retType).ToTypeSig(), newParams.ToArray());
				else if (genParamCount > 0)
					return MethodSig.CreateStaticGeneric((uint)genParamCount, ImportAsOsuModuleType(retType).ToTypeSig());
			}

			return new MethodSig(ReflectionToDnLibConvention(methBase.CallingConvention), (uint)genParamCount, ImportAsOsuModuleType(retType).ToTypeSig(), newParams);
		}

		public FieldSig FieldInfoToFieldSig(FieldInfo field)
		{
			ITypeDefOrRef fieldType;

			if (PatcherCache.HasPatcherType(field.FieldType.FullName, _moduleExplorer.Module))
				fieldType = PatcherCache.GetPatcherType(field.FieldType.FullName, _moduleExplorer.Module);
			else
				fieldType = ImportAsOsuModuleType(field.FieldType);

			return new FieldSig(fieldType.ToTypeSig());
		}

		public IMemberRef ResolveMemberInfo(MemberInfo memberInfo)
		{
			if (memberInfo is MethodInfo m && IsEntryAssemblyMember(memberInfo) && !IsCompilerGenerated(m.DeclaringType) && m.DeclaringType == _sourceDeclaringType)
			{
				var methodSig = MethodInfoToMethodSig(m);
				var methodExplorer = _typeExplorer.FindMethodRaw(memberInfo.Name, methodSig) ?? _typeExplorer.FindMethodRaw(memberInfo.Name);
				if (methodExplorer is null)
					return _typeExplorer.InsertMethod((MethodAttributes)(int)m.Attributes, m, !m.IsStatic).Method;

				return methodExplorer.Method;
			}

			if (memberInfo.DeclaringType != null && IsCopyablePatcherType(memberInfo.DeclaringType))
				return ResolveCopiedPatcherMember(memberInfo);

			if (memberInfo is Type type)
			{
				return ImportAsOsuModuleType(type);
			}

			var importedOsuType = ImportAsOsuModuleType(memberInfo.DeclaringType);
			var importedType = importedOsuType.ResolveTypeDef();

			if (PatcherCache.HasPatcherType(memberInfo.DeclaringType.FullName, _moduleExplorer.Module))
			{
				var patcherType = PatcherCache.GetPatcherType(memberInfo.DeclaringType.FullName, _moduleExplorer.Module);

				var member = memberInfo is MethodBase ? 
					(IMemberRef)patcherType.FindMethod(memberInfo.Name) : 
					patcherType.FindField(memberInfo.Name);

				if (member != null)
				{
					return member;
				}
			}

			// TODO: remove duplicate code
			if (importedType is null)
			{
				var explorer = _typeExplorer;
				if (memberInfo is MethodBase methodBase)
				{
					var importedMethod = _moduleExplorer.Module.Import(methodBase);
					var methodType = importedMethod.DeclaringType;

					var typeName = $"{_typeExplorer.Type.FullName}_{methodType.Name}";

					if (methodType.IsTypeRef && (methodType as TypeRef).IsNested)
					{
						explorer = _moduleExplorer.FindRaw(typeName);
						if (explorer is null)
						{
							var typeDef = new TypeDefUser(typeName, _moduleExplorer.CorLibTypes.Object.TypeDefOrRef)
							{
								Attributes = ((TypeAttributes)(uint)methodBase.DeclaringType.Attributes) & ~TypeAttributes.NestedPrivate | TypeAttributes.Public,
							};

							_moduleExplorer.Module.Types.Add(typeDef);

							PatcherCache.AddType(methodBase.DeclaringType.FullName, typeDef);
							PatcherCache.AddType(methodType.ReflectionFullName, typeDef);

							explorer = new TypeExplorer(_moduleExplorer, typeDef);

							if (!importedMethod.Name.StartsWith(".c"))
							{
								foreach (var constructor in memberInfo.DeclaringType.GetConstructors((BindingFlags)int.MaxValue))
								{
									ResolveMemberInfo(constructor);
								}
							}
						}
					}
					
					if (importedMethod.IsCompilerGenerated())
					{
						var methodInfo = methodBase as MethodInfo;
						var existingMethod = explorer.FindMethodRaw(importedMethod.Name);
						if (existingMethod != null)
							return existingMethod.Method;

						var methodExplorer = explorer.InsertMethod((MethodAttributes)(int)methodInfo.Attributes, methodInfo, true);
						return methodExplorer.Method;
					}
					else if (importedMethod.Name.StartsWith(".c") && methodBase.DeclaringType.IsDefined(typeof(CompilerGeneratedAttribute), false) && explorer.FindMethodRaw(importedMethod.Name) == null)
					{
						var constructorInfo = methodBase as ConstructorInfo;
						if (constructorInfo.GetMethodBody() == null)
							return importedMethod;

						var methodExplorer = explorer.InsertMethod((MethodAttributes)(int)constructorInfo.Attributes, methodBase, true);
						return methodExplorer.Method;
					}

					return importedMethod;
				}
				else if (memberInfo is FieldInfo field)
				{
					var importedField = _moduleExplorer.Module.Import(field) as IField;

					var fieldType = importedField.DeclaringType;
					var typeName = $"{_typeExplorer.Type.FullName}_{fieldType.Name}";

					if (fieldType.IsTypeRef && (fieldType as TypeRef).IsNested)
					{
						explorer = _moduleExplorer.FindRaw(typeName);
						if (explorer is null)
						{
							var typeDef = new TypeDefUser(typeName, _moduleExplorer.CorLibTypes.Object.TypeDefOrRef)
							{
								Attributes = ((TypeAttributes)(uint)field.DeclaringType.Attributes) & ~TypeAttributes.NestedPrivate | TypeAttributes.Public,
							};

							_moduleExplorer.Module.Types.Add(typeDef);

							PatcherCache.AddType(field.DeclaringType.FullName, typeDef);
							PatcherCache.AddType(importedField.DeclaringType.ReflectionFullName, typeDef);

							explorer = new TypeExplorer(_moduleExplorer, typeDef);

							foreach (var constructor in memberInfo.DeclaringType.GetConstructors((BindingFlags)int.MaxValue))
								ResolveMemberInfo(constructor);
						}
					}

					var fieldDef = explorer.FindFieldRawNoThrow(importedField.Name);

					if (fieldDef != null)
					{
						return fieldDef;
					}

					var fieldSig = FieldInfoToFieldSig(field);
					fieldDef = new FieldDefUser(importedField.Name, fieldSig, (FieldAttributes)(ushort)field.Attributes);

					explorer.Type.Fields.Add(fieldDef);
					return fieldDef;
				}
			}

			switch (memberInfo.MemberType)
			{
				case MemberTypes.Field:
					if (importedType.IsSystemType())
						return _moduleExplorer.Module.Import((FieldInfo)memberInfo);

					var fieldInfo = (FieldInfo)memberInfo;
					var fieldName = _moduleExplorer.NameProvider.GetName(memberInfo.Name, true);
					var resolvedField = importedType.FindField(fieldName) ?? importedType.FindField(memberInfo.Name);
					if (resolvedField != null)
						return PublicizeResolvedHookMember(memberInfo, resolvedField);

					return new MemberRefUser(_moduleExplorer.Module, fieldName, FieldInfoToFieldSig(fieldInfo), importedOsuType);

				case MemberTypes.Constructor:
					if (importedType.IsSystemType())
						return _moduleExplorer.Module.Import((MethodBase)memberInfo);

					var ctorInfo = (ConstructorInfo)memberInfo;
					var resolvedConstructor = importedType.FindMethod(ctorInfo.Name, MethodInfoToMethodSig(typeof(void), ctorInfo));
					if (resolvedConstructor != null)
						return PublicizeResolvedHookMember(memberInfo, resolvedConstructor);

					return _moduleExplorer.Module.Import((MethodBase)memberInfo);

				case MemberTypes.Method:
					// TODO: rework dependencies and remove this dirty fix
					if (importedType.IsSystemType() && !importedType.HasGenericParameters || (importedType.Namespace ?? "").StartsWith("OpenTK"))
						return _moduleExplorer.Module.Import((MethodBase)memberInfo);

					var methodInfo = (MethodInfo)memberInfo;

					var hasObfuscatedMethods = importedType.Methods.Any(x => x.IsNameObfuscated());

					// TODO: messy :(
					var name = methodInfo.IsSpecialName || !hasObfuscatedMethods || importedType.Name == "List`1" || _methodBlackList.Contains(methodInfo.Name) ?
						methodInfo.Name :
						_moduleExplorer.NameProvider.GetName(methodInfo.Name, true);
					
					var methodSig = MethodInfoToMethodSig(methodInfo);

					if (importedType.HasGenericParameters)
					{
						if (importedType.IsSystemType())
						{
							var genericInstSig = importedOsuType.ToTypeSig().ToGenericInstSig();

							for (var i = 0; i < memberInfo.DeclaringType.GenericTypeArguments.Length; i++)
							{
								genericInstSig.GenericArguments[i] = ImportAsOsuModuleType(memberInfo.DeclaringType.GenericTypeArguments[i]).ToTypeSig();
							}
						}
						PublicizeResolvedHookMember(memberInfo, importedType.FindMethod(name, methodSig) ?? importedType.FindMethod(methodInfo.Name, methodSig));
						return new MemberRefUser(_moduleExplorer.Module, name, methodSig, importedOsuType as TypeSpecUser);
					}
					else
					{
						var resolvedMethod = importedType.FindMethod(name, methodSig);
						if (resolvedMethod != null)
							return PublicizeResolvedHookMember(memberInfo, resolvedMethod);

						return new MemberRefUser(_moduleExplorer.Module, name, methodSig, importedOsuType);
					}

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		private static IMemberRef PublicizeResolvedHookMember(MemberInfo sourceMember, IMemberRef targetMember)
		{
			if (targetMember == null || sourceMember?.DeclaringType == null || !PatcherCache.IsHookAssembly(sourceMember.DeclaringType.Assembly))
				return targetMember;

			if (targetMember is MethodDef method && NeedsPublicAccess(method.Attributes))
				method.Attributes = (method.Attributes & ~MethodAccessMask) | MethodAttributes.Public;
			else if (targetMember is FieldDef field && NeedsPublicAccess(field.Attributes))
				field.Attributes = (field.Attributes & ~FieldAttributes.FieldAccessMask) | FieldAttributes.Public;

			return targetMember;
		}

		private static TypeDef PublicizeResolvedHookType(Type sourceType, TypeDef targetType)
		{
			if (targetType == null || sourceType == null || !PatcherCache.IsHookAssembly(sourceType.Assembly))
				return targetType;

			if (targetType.IsNested && NeedsPublicAccess(targetType.Attributes))
				targetType.Attributes = (targetType.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;

			return targetType;
		}

		private static bool NeedsPublicAccess(MethodAttributes attributes)
		{
			var access = attributes & MethodAccessMask;
			return access == MethodAttributes.Private ||
				   access == MethodAttributes.FamANDAssem ||
				   access == MethodAttributes.Family;
		}

		private static bool NeedsPublicAccess(FieldAttributes attributes)
		{
			var access = attributes & FieldAttributes.FieldAccessMask;
			return access == FieldAttributes.Private ||
				   access == FieldAttributes.FamANDAssem ||
				   access == FieldAttributes.Family;
		}

		private static bool NeedsPublicAccess(TypeAttributes attributes)
		{
			var visibility = attributes & TypeAttributes.VisibilityMask;
			return visibility == TypeAttributes.NestedPrivate ||
				   visibility == TypeAttributes.NestedFamANDAssem ||
				   visibility == TypeAttributes.NestedFamily;
		}

		private IMemberRef ResolveCopiedPatcherMember(MemberInfo memberInfo)
		{
			var explorer = EnsurePatcherType(memberInfo.DeclaringType);

			switch (memberInfo.MemberType)
			{
				case MemberTypes.Field:
				{
					var fieldInfo = (FieldInfo)memberInfo;
					var field = explorer.FindFieldRawNoThrow(fieldInfo.Name);
					if (field != null)
						return field;

					field = new FieldDefUser(fieldInfo.Name, FieldInfoToFieldSig(fieldInfo), (FieldAttributes)(ushort)fieldInfo.Attributes);
					explorer.Type.Fields.Add(field);
					return field;
				}
				case MemberTypes.Constructor:
				{
					var constructorInfo = (ConstructorInfo)memberInfo;
					var methodSig = MethodInfoToMethodSig(typeof(void), constructorInfo);
					var method = explorer.FindMethodRaw(constructorInfo.Name, methodSig) ?? explorer.FindMethodRaw(constructorInfo.Name);
					if (method != null)
						return method.Method;

					if (constructorInfo.GetMethodBody() == null)
						return _moduleExplorer.Module.Import((MethodBase)memberInfo);

					return explorer.InsertMethod((MethodAttributes)(int)constructorInfo.Attributes, constructorInfo, true).Method;
				}
				case MemberTypes.Method:
				{
					var methodInfo = (MethodInfo)memberInfo;
					var methodSig = MethodInfoToMethodSig(methodInfo);
					var method = explorer.FindMethodRaw(methodInfo.Name, methodSig) ?? explorer.FindMethodRaw(methodInfo.Name);
					if (method != null)
						return method.Method;

					return explorer.InsertMethod((MethodAttributes)(int)methodInfo.Attributes, methodInfo, true).Method;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private TypeExplorer EnsurePatcherType(Type type, bool copyTypeInitializer = true)
		{
			if (PatcherCache.HasPatcherType(type.FullName, _moduleExplorer.Module))
				return new TypeExplorer(_moduleExplorer, PatcherCache.GetPatcherType(type.FullName, _moduleExplorer.Module), _moduleExplorer.NameProvider);

			var sourceAttributes = (TypeAttributes)(uint)type.Attributes;
			TypeDefUser typeDef;

			if (type.IsNested)
			{
				var declaringType = EnsurePatcherType(type.DeclaringType, false);
				typeDef = new TypeDefUser(string.Empty, type.Name, _moduleExplorer.CorLibTypes.Object.TypeDefOrRef)
				{
					Attributes = (sourceAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic
				};
				declaringType.Type.NestedTypes.Add(typeDef);
			}
			else
			{
				typeDef = new TypeDefUser(type.Namespace, type.Name, _moduleExplorer.CorLibTypes.Object.TypeDefOrRef)
				{
					Attributes = (sourceAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.Public
				};
				_moduleExplorer.Module.Types.Add(typeDef);
			}

			PatcherCache.AddType(type.FullName, typeDef);

			var explorer = new TypeExplorer(_moduleExplorer, typeDef, _moduleExplorer.NameProvider);
			foreach (var fieldInfo in type.GetFields((BindingFlags)int.MaxValue).Where(x => x.DeclaringType == type))
			{
				var fieldAttributes = (FieldAttributes)(ushort)fieldInfo.Attributes;
				fieldAttributes = (fieldAttributes & ~FieldAttributes.FieldAccessMask) | FieldAttributes.Public;
				var field = new FieldDefUser(fieldInfo.Name, FieldInfoToFieldSig(fieldInfo), fieldAttributes);
				explorer.Type.Fields.Add(field);
			}

			var typeInitializer = type.TypeInitializer;
			if (copyTypeInitializer && IsCompilerGenerated(type) && typeInitializer != null && typeInitializer.GetMethodBody() != null)
			{
				var methodSig = MethodInfoToMethodSig(typeof(void), typeInitializer);
				if (explorer.FindMethodRaw(typeInitializer.Name, methodSig) == null && explorer.FindMethodRaw(typeInitializer.Name) == null)
					explorer.InsertMethod((MethodAttributes)(int)typeInitializer.Attributes, typeInitializer, true);
			}


			return explorer;
		}

		private static bool IsEntryAssemblyMember(MemberInfo memberInfo) =>
			memberInfo.Module.Assembly == Assembly.GetEntryAssembly();

		private static bool IsCompilerGenerated(MemberInfo memberInfo) =>
			memberInfo != null && memberInfo.IsDefined(typeof(CompilerGeneratedAttribute), false);

		private static bool IsCopyablePatcherType(Type type) =>
			type.Assembly == Assembly.GetEntryAssembly() && !type.IsGenericParameter && !type.IsSystemType();


		public CallingConvention ReflectionToDnLibConvention(CallingConventions refConv)
		{
			CallingConvention newConv = CallingConvention.Default;

			if ((refConv & CallingConventions.VarArgs) != 0)
				newConv |= CallingConvention.VarArg;

			if ((refConv & CallingConventions.Any) == CallingConventions.Any)
				newConv |= CallingConvention.VarArg | CallingConvention.Default; // BUG ??????

			if ((refConv & CallingConventions.HasThis) != 0)
				newConv |= CallingConvention.HasThis;

			if ((refConv & CallingConventions.ExplicitThis) != 0)
				newConv |= CallingConvention.ExplicitThis;

			return newConv;
		}

		/// <summary>
		/// Convert hook types (generated by HookGenerator) to osu!.exe TypeSig
		/// </summary>
		public ITypeDefOrRef ImportAsOsuModuleType(Type type)
		{
			if (type.IsArray)
			{
				var elementType = ImportAsOsuModuleType(type.GetElementType()).ToTypeSig();
				return new TypeSpecUser(type.GetArrayRank() == 1
					? (TypeSig)new SZArraySig(elementType)
					: new ArraySig(elementType, (uint)type.GetArrayRank()));
			}

			if (PatcherCache.HasPatcherType(type.FullName, _moduleExplorer.Module))
				return PatcherCache.GetPatcherType(type.FullName, _moduleExplorer.Module);

			if (IsCopyablePatcherType(type))
				return EnsurePatcherType(type).Type;

			if (type.IsSystemType() || !PatcherCache.IsHookAssembly(type.Assembly) || type.IsGenericParameter) // System types and external dependencies (OpenTK, etc)
				return _moduleExplorer.Import(type);

			// from this point we know that type argument is definitely a Type from OsuHooks assembly

			if (type.IsNested)
				return PublicizeResolvedHookType(type, UnnestType(type));

			if (type.IsGenericType)
			{
				var osuType = PublicizeResolvedHookType(type, _moduleExplorer[type.FullName.Split('[')[0]].Type);
				var typeGenericArguments = type.GetGenericArguments();
				var genArgs = new List<TypeSig>();
				for (var i = 0; i < typeGenericArguments.Length; i++)
				{
					var genericType = ImportAsOsuModuleType(typeGenericArguments[i]);
					genArgs.Add(genericType.ToTypeSig());
				}

				var converted = new TypeSpecUser(new GenericInstSig(new ClassSig(osuType), genArgs.ToArray()));

				return converted;
			}

			return PublicizeResolvedHookType(type, _moduleExplorer[type.FullName].Type);
		}

		private TypeDef UnnestType(Type type) =>
			UnnestType(type, new List<Type>());

		private TypeDef UnnestType(Type type, List<Type> unnestOrder)
		{
			if (!type.IsNested) // finally! now unnesting
			{
				var currentTypeDef = _moduleExplorer[type.FullName];

				foreach (var nextTypeDef in unnestOrder)
					currentTypeDef = currentTypeDef.FindNestedType(nextTypeDef.Name);

				return currentTypeDef.Type;
			}

			unnestOrder.Add(type);
			return UnnestType(type.DeclaringType, unnestOrder);
		}

		static class PatcherCache // this is needed
		{
			private static HashSet<string> _guidCache = new HashSet<string>();
			private static Dictionary<string, TypeDef> _typeCache = new Dictionary<string, TypeDef>();

			public static void AddType(string name, TypeDef newType) 
			{
				if (name != null)
					_typeCache[name] = newType;

				_typeCache[newType.FullName] = newType;
				_typeCache[newType.ReflectionFullName] = newType;
			}
			public static bool HasPatcherType(string name, ModuleDef module) =>
				name != null && _typeCache.TryGetValue(name, out var type) && type.Module == module;
			public static TypeDef GetPatcherType(string name, ModuleDef module) => _typeCache[name];

			public static bool IsHookAssembly(Assembly ass)
			{
				var guid = ass.GetAssemblyGuid();

				if (guid != null && _guidCache.Contains(guid))
					return true;

				if (ass.CustomAttributes.Any(x => x.AttributeType.Name == HookGenerator.IDENTIFICATION_ATTRIBUTE_NAME))
				{
					if (guid != null)
						_guidCache.Add(guid);

					return true;
				}

				return false;
			}
		}
	}
}