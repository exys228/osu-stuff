using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch.Lib.NameMapper.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace osu_patch.Lib.NameMapper
{
	/// <summary>
	/// This program is used to identify obfuscated method/field/class names considering that you have similar binary (may be updated/changed, comparisons are made based on similarity).
	/// </summary>
	public class NameMapper
	{
		private const float MINIMAL_SUCCESS_PERCENTAGE = .1f;

		internal ModuleDefMD CleanModule { get; } // Module to inherit names from

		internal ModuleDefMD ObfModule { get; }
		
		private ConcurrentQueue<string> _debugOutput;

		private NamableProcessor _namableProcessor;

		internal ConcurrentDictionary<string, string> NamePairs = new ConcurrentDictionary<string, string>(); // clean, obf

		public bool DeobfuscateNames { get; }

		private int _overallErroredMethods;

		private int _inWork;

		public bool Processed { get; private set; }

		public bool ShowErroredMethods { get; set; } = true;

		public NameMapper(ModuleDefMD cleanModule, ModuleDefMD obfModule, ConcurrentQueue<string> debugOutput = null, bool deobfuscateNames = true)
		{
			CleanModule = cleanModule;
			ObfModule = obfModule;
			DeobfuscateNames = deobfuscateNames;
			_debugOutput = debugOutput;
			_namableProcessor = new NamableProcessor(this);
		}

		public Dictionary<string, string> GetNamePairs() => new Dictionary<string, string>(NamePairs);

		public void BeginProcessing()
		{
#if DEBUG
			Stopwatch debugStopwatch = new Stopwatch();
			debugStopwatch.Start();

			try
			{
#endif
				if (Processed)
					throw new NameMapperProcessingException("Already processed! This class is a one-time use.");

				// -- BEGIN PROCESSING

				//	 -- Trying to find exactly same methods using entry as a start point.

				var cleanEntry = FindEntryPoint(CleanModule);
				var obfEntry = FindEntryPoint(ObfModule);

				Message(XConsole.Info("Recursing using entry as a start point."));
				StartRecurseThread(cleanEntry, obfEntry);
				WaitMakeSure();

				//	 -- 

				//	 -- 1. Getting unique methods from both obf and clean types as two separate lists (NameProcessed, but not FullyProcessed)
				//	 -- 2. Comparing unique obf methods to unique clean methods, recursing same methods
				//	 -- 3. FullyProcessed = true

				Message(XConsole.Info("Recursing unique methods."));

				int prevCount = -1;

				var procMan = _namableProcessor.Processed;

				while (true)
				{
					foreach (var item in procMan.Types)
					{
						if (item.FullyProcessed)
							continue;

						var cleanMethods = item.Clean.ScopeType.ResolveTypeDef()?.Methods.Where(x => !x.IsNameObfuscated()).ToList();
						var obfMethods = item.Obfuscated.ScopeType.ResolveTypeDef()?.Methods.Where(x => !x.IsEazInternalName()).ToList();

						if (cleanMethods is null || obfMethods is null)
							continue;

						List<MethodDef> cleanUniqueMethods = cleanMethods.GetUniqueMethods();
						List<MethodDef> obfUniqueMethods = obfMethods.GetUniqueMethods();

						foreach (var cleanMethod in cleanUniqueMethods)
						{
							var obfMethod = obfUniqueMethods.FirstOrDefault(x => AreOpcodesEqual(cleanMethod?.Body?.Instructions, x.Body?.Instructions) == -1);

							if (obfMethod != null)
								StartRecurseThread(cleanMethod, obfMethod);
						}

						item.FullyProcessed = true;
					}

					WaitMakeSure();

					int count = procMan.Types.Count;

					if (count == prevCount)
						break;

					Message(XConsole.Info($"{count - prevCount} new types! Processing..."));

					prevCount = count;
				}

				//   --

				if (DeobfuscateNames)
				{
					//   -- Filling up interfaces

					foreach (var type in procMan.Types)
					{
						var obfTypeDef = type.Obfuscated.ScopeType.ResolveTypeDef();

						if (obfTypeDef.HasInterfaces)
						{
							var interfaceMethods = obfTypeDef.Interfaces.Where(x => x.Interface.IsFromModule(this)).SelectMany(x => x.Interface.ResolveTypeDef().Methods).Where(x => x.IsNameObfuscated()).ToList();

							foreach (var method in obfTypeDef.Methods)
							{
								if (NamePairs.TryGetValue(method.Name, out string obfName))
								{
									var interfaceMethod = interfaceMethods.FirstOrDefault(x => x.Name == obfName);

									if (interfaceMethod != null)
										interfaceMethod.Name = method.Name;
								}
							}
						}
					}

					//   --

					//   -- Renaming all names left

					var obfCleanDict = NamePairs.Swap();

					var res = new MemberFinder().FindAll(ObfModule);

					foreach (var meth in res.MethodDefs.Select(x => x.Key).Where(x => x.IsNameObfuscated()))
					{
						if (obfCleanDict.TryGetValue(meth.Name, out string value))
							meth.Name = value;
					}

					foreach (var type in res.TypeDefs.Select(x => x.Key).Where(x => x.IsNameObfuscated()))
					{
						if (obfCleanDict.TryGetValue(type.Name, out string value))
							type.Name = value;
					}

					foreach (var field in res.FieldDefs.Select(x => x.Key).Where(x => x.IsNameObfuscated()))
					{
						if (obfCleanDict.TryGetValue(field.Name, out string value))
							field.Name = value;
					}

					foreach (var genParam in res.GenericParams.Select(x => x.Key).Where(x => x.IsNameObfuscated()))
					{
						if (obfCleanDict.TryGetValue(genParam.Name, out string value))
							genParam.Name = value;
					}

					//   --
				}

				WaitMakeSure();

				if (_overallErroredMethods > 0)
					Message(XConsole.Warn($"Not all methods are processed! {_overallErroredMethods} left behind."));

				Message(XConsole.Info($"Overall known classes: {procMan.Types.Count}; Fully processed classes: {procMan.Types.Count(x => x.FullyProcessed)}"));

				var processedTypesCount = procMan.Types.Count;
				var allTypesCount = ObfModule.CountTypes(x => !x.IsNameObfuscated());
				var processedPercentage = (float)processedTypesCount / allTypesCount;

				if (processedPercentage < MINIMAL_SUCCESS_PERCENTAGE)
					throw new NameMapperProcessingException($"Processed types percentage: {processedTypesCount}/{allTypesCount} => {processedPercentage * 100}% < {MINIMAL_SUCCESS_PERCENTAGE * 100}% (min), counting as unsuccessful.");

				Processed = true;
#if DEBUG
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				Console.ReadKey();
			}
			finally
			{
				debugStopwatch.Stop();
				Message(XConsole.Info($"Mapped everything in: {debugStopwatch.ElapsedMilliseconds}ms"));
			}
#endif

			// -- END
		}

		public string FindName(string cleanName)
		{
			if (!Processed)
				return null;

			if (!NamePairs.TryGetValue(cleanName, out var obfName))
				throw new NameMapperException("Unable to find specified name: " + cleanName);

			return obfName;
		}

		/// <summary>
		/// Wait until all threads are done. Pretty much hardcoded.
		/// </summary>
		private void WaitMakeSure()
		{
			int occ = 0;

			long prevState = 0;

			while (true)
			{
				if (occ < 3 && _inWork == 0)
					occ++;
				else if (occ >= 3)
					break;
				else
					occ = 0;

				Thread.Sleep(100);

				if (Math.Abs(prevState - _inWork) > 25)
					Message(XConsole.Info("Waiting far all threads to finish! In work: " + _inWork));

				prevState = _inWork;
			}
		}

		/// <summary>
		/// Try to find a valid entry point for assembly, returns null if not found.
		/// </summary>
		/// <param name="module">Module to find entry point in.</param>
		/// <returns>True entry point, null if not found.</returns>
		private MethodDef FindEntryPoint(ModuleDef module)
		{
			if (module?.EntryPoint?.Body?.Instructions?.Count == 2 && module.EntryPoint.Body.Instructions[0]?.OpCode == OpCodes.Call)
				return ((IMethodDefOrRef)module.EntryPoint.Body.Instructions[0]?.Operand).ResolveMethodDef();

			throw new NameMapperException("Unable to find entry point of given module");
		}

		private void StartRecurseThread(IMethodDefOrRef cleanMethod, IMethodDefOrRef obfMethod) => StartRecurseThread(cleanMethod?.ResolveMethodDef(), obfMethod?.ResolveMethodDef());

		private void StartRecurseThread(MethodDef cleanMethod, MethodDef obfMethod)
		{
			Interlocked.Increment(ref _inWork);

			ThreadPool.QueueUserWorkItem(state =>
			{
				var lockTaken = false;

				try
				{
					if (cleanMethod is null || obfMethod is null)
						return;

					Monitor.Enter(obfMethod, ref lockTaken); // clean is used in OperandProcessors.ProcessMethod, hardcoded but that's important

					if (lockTaken)
					{
						var result = _namableProcessor.ProcessMethod(cleanMethod, obfMethod);

						if (cleanMethod.DeclaringType.IsEazInternalNameRecursive())
							return;

						if(result == ProcessResult.Ok)
						{
							var cleanInstrs = cleanMethod.Body?.Instructions;
							var obfInstrs = obfMethod.Body?.Instructions;

							if (!cleanMethod.HasBody || !obfMethod.HasBody)
								return;

							// ReSharper disable PossibleNullReferenceException
							if (cleanInstrs.Count != obfInstrs.Count)
							{
								Message(XConsole.Error($"Instruction count differs ({cleanMethod.GetMethodName()}). {cleanInstrs.Count} (clean) {obfInstrs.Count} (obf)."));
								return;
							}

							var idx = AreOpcodesEqual(cleanInstrs, obfInstrs);

							if (idx > -1)
							{
								Message(XConsole.Error($"Instructions differ ({cleanMethod.GetMethodName()}).") + Environment.NewLine +
										 "-*- Clean instructions:" + Environment.NewLine +
										 PrintInstructions(cleanInstrs, idx) + Environment.NewLine +
										 "-*- Obfuscated instructions:" + Environment.NewLine +
										 PrintInstructions(obfInstrs, idx));
								return;
							}

							for (int i = 0; i < cleanInstrs.Count; i++)
							{
								object cleanOperand = cleanInstrs[i].Operand;
								object obfOperand = obfInstrs[i].Operand;

								if (cleanOperand is null || obfOperand is null)
									continue;

								if (cleanOperand.GetType() != obfOperand.GetType())
									continue;

								if (cleanOperand is IMethodDefOrRef)
									StartRecurseThread(cleanOperand as IMethodDefOrRef, obfOperand as IMethodDefOrRef);
								else if (cleanOperand is IType)
									_namableProcessor.ProcessType(cleanOperand as IType, obfOperand as IType);
								else if (cleanOperand is FieldDef)
									_namableProcessor.ProcessField(cleanOperand as FieldDef, obfOperand as FieldDef);
							}
						}
						else if(result.IsError())
						{
							Message(XConsole.Error($"An error occurred while trying to process method ({cleanMethod.GetMethodName()}). Result code: {result}."));
							_overallErroredMethods++;
						}
					}
				}
				catch (Exception e)
				{
					Message(XConsole.Error($"An exception occurred while trying to process method ({cleanMethod.GetMethodName()}). Details:\n{e}"));
				}
				finally
				{
					Interlocked.Decrement(ref _inWork);

					if(lockTaken)
						Monitor.Exit(obfMethod);
				}
			});
		}

		/// <summary>
		/// Check instruction equality using opcodes only, no operands used.
		/// </summary>
		/// <returns>-1 if equal, errored index otherwise</returns>
		private int AreOpcodesEqual(IList<Instruction> cleanInstructions, IList<Instruction> obfInstructions)
		{
			if (cleanInstructions is null || obfInstructions is null)
				return -2;

			if (cleanInstructions.Count != obfInstructions.Count)
				return -2;

			for (int i = 0; i < cleanInstructions.Count; i++)
			{
				var cleanOpcode = cleanInstructions[i].OpCode;
				var obfOpcode = obfInstructions[i].OpCode;

				if (cleanOpcode != obfOpcode)
					return i;

				/*var cleanOperand = cleanInstructions[i].Operand;
				var obfOperand = obfInstructions[i].Operand;

				 // this doesn't really add any improvement in GetUniqueMethods as i expected, only REALLY slows down mapping speed
				if (cleanOperand is null || obfOperand is null)
					continue;

				if (cleanOperand.GetType() != obfOperand.GetType())
					return false;

				if (cleanOperand is IMethod)
				{
					var cleanMethod = cleanOperand as IMethod;
					var obfMethod = obfOperand as IMethod;

					var expectedObfMethod = _namableProcessor.AlreadyProcessedMethods.FirstOrDefault(x => x.Item1.MDToken == cleanMethod.MDToken)?.Item2;

					if (expectedObfMethod != null && obfMethod.MDToken != expectedObfMethod.MDToken)
						return false;
				}
				else if (cleanOperand is IType)
				{
					var cleanType = cleanOperand as IType;
					var obfType = obfOperand as IType;

					var expectedObfType = _namableProcessor.Types.FirstOrDefault(x => x.Types.Item1.MDToken == cleanType.MDToken)?.Types.Item2;

					if (expectedObfType != null && obfType.MDToken != expectedObfType.MDToken)
						return false;
				}
				else if (cleanOperand is FieldDef)
				{
					var cleanField = cleanOperand as FieldDef;
					var obfField = obfOperand as FieldDef;

					var expectedObfField = _namableProcessor.AlreadyProcessedFields.FirstOrDefault(x => x.Item1.MDToken == cleanField.MDToken)?.Item2;

					if (expectedObfField != null && obfField.MDToken != expectedObfField.MDToken)
						return false;
				}
				*/

				/*if (cleanOperand is null || obfOperand is null || cleanOperand.GetType() != obfOperand.GetType())
					continue; // ???????

				if(cleanOperand is sbyte || cleanOperand is int || cleanOperand is float || cleanOperand is double || cleanOperand is long)
					if (!cleanOperand.Equals(obfOperand))
						return false;*/ // useless anyways (?)
			}

			return -1;
		}

		private static readonly object _msgLock = new object();

		internal bool Message(string msg = "", bool newline = true)
		{
			lock (_msgLock)
				_debugOutput?.Enqueue(msg + (newline ? Environment.NewLine : string.Empty));

			return false;
		}

		internal string PrintInstructions(IList<Instruction> instrs, int errorIndex, int range = 2)
		{
			// ... -5 instrs
			// errorIndex
			// ... +5 instrs

			var start = (errorIndex - range).Clamp(0, instrs.Count);
			var end = (errorIndex + range + 1).Clamp(0, instrs.Count);
			var str = string.Empty;

			for (int i = start; i < end; i++)
				str += (i == errorIndex ? "\u001C>>> " : XConsole.PAD) + instrs[i] + "\u0001" + Environment.NewLine;

			return str.TrimEnd();
		}
	}
}
