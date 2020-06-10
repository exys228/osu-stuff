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
		private MethodAttributes _flags;

		public MethodConverter(Delegate del, ModuleExplorer osuModule, bool hasThis = false) : this(del, new MemberConverter(osuModule), hasThis) { }

		public MethodConverter(Delegate del, MemberConverter memberConverter, bool hasThis = false)
		{
			var meth = del.Method;

			_name = meth.Name;
			_memberConverter = memberConverter;
			_methodSig = memberConverter.MethodInfoToMethodSig(meth, hasThis);
			_parameters = new List<ParameterInfo>(meth.GetParameters());
			_returnType = meth.ReturnType;
			_bodyConverter = new BodyConverter(del, memberConverter);
			_hasThis = hasThis;
		}

		public MethodExplorer ToMethodExplorer(bool forceBodyRebuild = false) =>
			new MethodExplorer(null, ToMethodDef(forceBodyRebuild));

		public MethodDefUser ToMethodDef(bool forceBodyRebuild = false)
		{
			var newMethodDef = new MethodDefUser(_name, _methodSig, MethodImplAttributes.IL | MethodImplAttributes.Managed);
			newMethodDef.Body = _bodyConverter.ToCilBody(forceBodyRebuild);

			var idx = _hasThis ? 1 : 0;

			for (int i = idx; i < _parameters.Count; i++)
				newMethodDef.ParamDefs.Add(new ParamDefUser(_parameters[i].Name, (ushort)(i + (idx ^ 1)), (ParamAttributes)_parameters[i].Attributes));

			newMethodDef.ReturnType = _memberConverter.ImportAsOsuModuleType(_returnType).ToTypeSig();
			return newMethodDef;
		}
	}
}