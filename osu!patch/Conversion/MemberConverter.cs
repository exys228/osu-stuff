using dnlib.DotNet;
using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

		private static List<string> _methodBlackList = new List<string>()
		{
			"GetEnumerator"
		};

		public MemberConverter(TypeExplorer typeExplorer)
		{
			_typeExplorer = typeExplorer;
			_moduleExplorer = typeExplorer.GetRoot();
		}
		
		public MethodSig MethodInfoToMethodSig(MethodInfo methInfo, bool hasThis = false) =>
			MethodInfoToMethodSig(methInfo.ReturnType, methInfo, hasThis);

		public MethodSig MethodInfoToMethodSig(Type retType, MethodBase methBase, bool hasThis = false)
		{
			var isStatic = methBase.IsStatic && !hasThis;

			var genParamCount = methBase.ContainsGenericParameters ? methBase.GetGenericArguments().Length : 0;
			
			if (methBase is MethodInfo meth && meth.DeclaringType.IsGenericType)
			{
				var declaringType = meth.DeclaringType.GetGenericTypeDefinition();
				methBase = declaringType
					.GetMethods((BindingFlags)int.MaxValue)
					.FirstOrDefault(x => x.Name == methBase.Name && x.GetParameters().Length == methBase.GetParameters().Length);
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

			if (PatcherCache.HasPatcherType(field.FieldType.FullName))
				fieldType = PatcherCache.GetPatcherType(field.FieldType.FullName);
			else
				fieldType = ImportAsOsuModuleType(field.FieldType);

			return new FieldSig(fieldType.ToTypeSig());
		}

		public IMemberRef ResolveMemberInfo(MemberInfo memberInfo)
		{
			if (memberInfo is MethodInfo m && memberInfo.Module.Assembly == Assembly.GetEntryAssembly())
			{
				var methodExplorer = _typeExplorer.FindMethodRaw(memberInfo.Name);
				if (methodExplorer is null)
				{
					return _typeExplorer.InsertMethod((MethodAttributes)(int)m.Attributes, m).Method;
				}
				else
				{
					return methodExplorer.Method;
				}
			}

			if (memberInfo is Type type)
				return ImportAsOsuModuleType(type).ResolveTypeDef();

			var importedOsuType = ImportAsOsuModuleType(memberInfo.DeclaringType);
			var importedType = importedOsuType.ResolveTypeDef();

			if (PatcherCache.HasPatcherType(memberInfo.DeclaringType.FullName))
			{
				var patcherType = PatcherCache.GetPatcherType(memberInfo.DeclaringType.FullName);

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
						var methodExplorer = explorer.InsertMethod((MethodAttributes)(int)methodInfo.Attributes, methodInfo, true);

						return methodExplorer.Method;
					}
					else if (importedMethod.Name.StartsWith(".c") && explorer.FindMethodRaw(importedMethod.Name) == null)
					{
						var constructorInfo = methodBase as ConstructorInfo;
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

					return importedType.FindField(_moduleExplorer.NameProvider.GetName(memberInfo.Name))
						?? importedType.FindField(memberInfo.Name);

				case MemberTypes.Constructor:
					if (importedType.IsSystemType())
						return _moduleExplorer.Module.Import((MethodBase)memberInfo);

					var ctorInfo = (ConstructorInfo)memberInfo;
					return importedType.FindMethod(ctorInfo.Name, MethodInfoToMethodSig(typeof(void), ctorInfo));

				case MemberTypes.Method:
					// TODO: rework dependencies and remove this dirty fix
					if (importedType.IsSystemType() && !importedType.HasGenericParameters || importedType.Namespace.StartsWith("OpenTK"))
						return _moduleExplorer.Module.Import((MethodBase)memberInfo);

					var methodInfo = (MethodInfo)memberInfo;

					var hasObfuscatedMethods = importedType.Methods.Any(x => x.IsNameObfuscated());

					// TODO: messy :(
					var name = methodInfo.IsSpecialName || !hasObfuscatedMethods || importedType.Name == "List`1" || _methodBlackList.Contains(methodInfo.Name) ?
						methodInfo.Name :
						_moduleExplorer.NameProvider.GetName(methodInfo.Name);
					
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
						return new MemberRefUser(_moduleExplorer.Module, name, methodSig, importedOsuType as TypeSpecUser);
					}
					else
						return importedType.FindMethod(name, methodSig);

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

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
			if (type.IsSystemType() || !PatcherCache.IsHookAssembly(type.Assembly) || type.IsGenericParameter) // System types and external dependencies (OpenTK, etc)
				return _moduleExplorer.Import(type);

			// from this point we know that type argument is definitely a Type from OsuHooks assembly

			if (type.IsNested)
				return UnnestType(type);

			if (type.IsGenericType)
			{
				var osuType = _moduleExplorer[type.FullName.Split('[')[0]].Type;
				var typeGenericArguments = type.GetGenericArguments();
				var genArgs = new List<TypeSig>();
				for (var i = 0; i < typeGenericArguments.Length; i++)
				{
					var genericType = ImportAsOsuModuleType(typeGenericArguments[i]).ResolveTypeDef();
					genArgs.Add(genericType.ToTypeSig());
				}

				var converted = new TypeSpecUser(new GenericInstSig(new ClassSig(osuType), genArgs.ToArray()));

				return converted;
			}

			return _moduleExplorer[type.FullName].Type;
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
				_typeCache.Add(name, newType);
				_typeCache.Add(newType.ReflectionFullName, newType);
			}
			public static bool HasPatcherType(string name) => _typeCache.ContainsKey(name);
			public static TypeDef GetPatcherType(string name) => _typeCache[name];

			public static bool IsHookAssembly(Assembly ass)
			{
				var guid = ass.GetAssemblyGuid();

				if (_guidCache.Contains(guid))
					return true;

				if (ass.CustomAttributes.Any(x => x.AttributeType.Name == HookGenerator.IDENTIFICATION_ATTRIBUTE_NAME))
				{
					_guidCache.Add(guid);
					return true;
				}

				return false;
			}
		}
	}
}