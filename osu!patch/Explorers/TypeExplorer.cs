using dnlib.DotNet;
using osu_patch.Conversion;
using osu_patch.Exceptions;
using osu_patch.Naming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace osu_patch.Explorers
{
	public class TypeExplorer : IExplorerParent
	{
		public IExplorerParent Parent { get; }

		public IExplorerParent GetParent() => Parent;

		public TypeDef Type { get; }

		public INameProvider NameProvider { get; }

		public MethodExplorer this[string name] => FindMethod(name);

		public TypeExplorer(IExplorerParent parent, TypeDef type, INameProvider nameProvider = null)
		{
			Parent = parent;
			Type = type;
			NameProvider = nameProvider ?? MapperNameProvider.Instance;
		}

		public MethodExplorer FindMethod(string name, MethodSig sig = null)
		{
			var obfName = NameProvider.GetName(name);
			var result = (sig is null ? Type.FindMethod(obfName) : Type.FindMethod(obfName, sig)) ?? throw CreateException("method");

			return new MethodExplorer(this, result);
		}

		public MethodExplorer FindMethodRaw(string name, MethodSig sig = null)
		{
			var method = sig is null ? Type.FindMethod(name) : Type.FindMethod(name, sig);

			return new MethodExplorer(this, method ?? throw CreateException("method"));
		}

		public FieldDef FindField(string name) => Type.FindField(NameProvider.GetName(name)) ?? throw CreateException("field");

		public FieldDef FindFieldRaw(string name) => Type.FindField(name) ?? throw CreateException("field");

		public TypeExplorer FindNestedType(string name) => new TypeExplorer(this, Type.NestedTypes.FirstOrDefault(x => x.Name == NameProvider.GetName(name)) ?? throw CreateException("type"), NameProvider);

		public TypeExplorer FindNestedTypeRaw(string name) => new TypeExplorer(this, Type.NestedTypes.FirstOrDefault(x => x.Name == name) ?? throw CreateException("type"), NameProvider);

		#region InsertMethod conversion methods

		// -- Func

		public MethodExplorer InsertMethod<TResult>(MethodAttributes attributes, Func<TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T, TResult>(MethodAttributes attributes, Func<T, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, TResult>(MethodAttributes attributes, Func<T1, T2, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, TResult>(MethodAttributes attributes, Func<T1, T2, T3, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(MethodAttributes attributes, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> func) =>
			InsertMethod(attributes, (Delegate)func);

		// --

		// -- Action

		public MethodExplorer InsertMethod(MethodAttributes attributes, Action action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T>(MethodAttributes attributes, Action<T> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2>(MethodAttributes attributes, Action<T1, T2> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3>(MethodAttributes attributes, Action<T1, T2, T3> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4>(MethodAttributes attributes, Action<T1, T2, T3, T4> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> action) =>
			InsertMethod(attributes, (Delegate)action);

		public MethodExplorer InsertMethod<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(MethodAttributes attributes, Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> action) =>
			InsertMethod(attributes, (Delegate)action);

		// --
		#endregion

		public MethodExplorer InsertMethod(MethodAttributes attributes, Delegate del)
		{
			var hasThis = false;
			var paramList = new List<ParameterInfo>(del.Method.GetParameters());

			if ((attributes & MethodAttributes.Static) == 0)
			{
				if(paramList.Count == 0)
					throw new Exception("First argument is this-dummy and must be same type as instance-class in which this method is being injected in.");

				paramList.RemoveAt(0);
				hasThis = true;
			}

			var convertedMethodDef = new MethodConverter(del, this.GetRoot(), hasThis).ToMethodExplorer();
			convertedMethodDef.Method.Attributes = attributes;

			foreach (var param in paramList)
				convertedMethodDef.Method.ParamDefs.Add(new ParamDefUser(param.Name, (ushort)param.Position, (ParamAttributes)param.Attributes));

			Type.Methods.Add(convertedMethodDef.Method);
			return convertedMethodDef;
		}

		private static NameProviderException CreateException(string whatExactly) => // field, method etc.
			new NameProviderException($"Unable to find {whatExactly} specified!");
	}
}
