using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace StringFixerMini.CLI
{
	class Program
	{
		private static ModuleDefMD _module;
		private static Assembly _assembly;

		private static string _modulePath;

		static int Main(string[] args)
		{
			if (args.Length < 1)
				return Message("StringFixerMini.CLI\n" +
							   "by exys, 2019\n" +
							   "\n" +
							   "Usage:\n" +
							   "StringFixerMini.CLI [module]");

			if (!File.Exists(_modulePath = Path.GetFullPath(args[0])))
				return Message("E | Specified module path does not exist");

			try
			{
				_module = ModuleDefMD.Load(_modulePath);
				_assembly = Assembly.LoadFile(_modulePath);
			}
			catch (Exception e) { return Message("E | An error occurred while trying to load and process modules! Details:\n" + e); }

			Message($"I | Loaded module: {_module.Assembly.FullName}.");

			StringFixer.Fix(_module, _assembly);

			string filename = Path.GetFileNameWithoutExtension(_modulePath) + "-string" + Path.GetExtension(_modulePath);

			Message("I | Finally writing module back (with \"-string\" tag)!");

			_module.Write(
			Path.Combine(
			Path.GetDirectoryName(_modulePath) ?? throw new Exception("Path to write module to is null unexpectedly"), filename),
			new ModuleWriterOptions(_module)
			{
				MetadataOptions = { Flags = MetadataFlags.PreserveAll }
			});

			Console.ReadKey(true);
			return 0;
		}

		private static object _msgLock = new object();

		private static int Message(string msg = "", bool newline = true)
		{
			lock (_msgLock)
				Console.Write(msg + (newline ? Environment.NewLine : string.Empty));

			return 1;
		}
	}
}
