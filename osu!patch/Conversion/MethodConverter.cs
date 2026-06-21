using dnlib.DotNet;
using osu_patch.Explorers;
using System;
using System.Collections.Generic;
using System.Reflection;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;

namespace osu_patch.Conversion
{
	/// <summary>
	/// This class converts osupatch-plugin's (ex. <see cref="TypeExplorer.InsertMethod"/>) delegate to MethodDef
	/// </summary>
	public class MethodConverter
	{
		private string _name;
		private MethodSig _methodSig;
		private List<ParameterInfo> _parameters;
		private Type _returnType;

		private BodyConverter _bodyConverter;
		private MemberConverter _memberConverter;
		
		private bool _hasThis;

		public MethodConverter(Delegate del, TypeExplorer type, bool importing = false, bool hasThis = false, bool forceStatic = false) 
			: this(del.Method, new MemberConverter(type), importing, hasThis, forceStatic) { }
		public MethodConverter(MethodInfo meth, TypeExplorer type, bool importing = false, bool hasThis = false, bool forceStatic = false) 
			: this(meth, new MemberConverter(type), importing, hasThis, forceStatic) { }
		public MethodConverter(ConstructorInfo meth, TypeExplorer type, bool importing = false, bool hasThis = false) 
			: this(meth, new MemberConverter(type), importing, hasThis) { }

		public MethodConverter(MethodInfo method, MemberConverter memberConverter, bool importing = false, bool hasThis = false, bool forceStatic = false)
		{
			_name = method.Name;
			_memberConverter = memberConverter;
			_methodSig = memberConverter.MethodInfoToMethodSig(method, hasThis, forceStatic);
			_parameters = new List<ParameterInfo>(method.GetParameters());
			_returnType = method.ReturnType;
			_bodyConverter = new BodyConverter(method, memberConverter, importing);
			_hasThis = hasThis;
		}
		
		public MethodConverter(ConstructorInfo constructor, MemberConverter memberConverter, bool importing = false, bool hasThis = false)
		{
			_name = constructor.Name;
			_memberConverter = memberConverter;
			_methodSig = memberConverter.MethodInfoToMethodSig(typeof(void), constructor);
			_parameters = new List<ParameterInfo>(constructor.GetParameters());
			_returnType = typeof(void);
			_bodyConverter = new BodyConverter(constructor, memberConverter, importing);
			_hasThis = hasThis;
		}

		public MethodExplorer ToMethodExplorer(bool forceBodyRebuild = false) =>
			new MethodExplorer(null, ToMethodDef(forceBodyRebuild));

		public MethodDefUser ToMethodDef(bool forceBodyRebuild = false)
		{
			var newMethodDef = new MethodDefUser(_name, _methodSig, MethodImplAttributes.IL | MethodImplAttributes.Managed)
			{
				Body = _bodyConverter.ToCilBody(forceBodyRebuild)
			};

			var idx = _hasThis ? 1 : 0;

			for (int i = idx; i < _parameters.Count; i++)
				newMethodDef.ParamDefs.Add(new ParamDefUser(_parameters[i].Name, (ushort)(i + (idx ^ 1)), (ParamAttributes)_parameters[i].Attributes));

			newMethodDef.ReturnType = _memberConverter.ImportAsOsuModuleType(_returnType).ToTypeSig();
			return newMethodDef;
		}
	}
}