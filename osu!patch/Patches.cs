using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using osu_patch.Explorers;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace osu_patch
{
	public static class Patches
	{
		private const string OsuBaseUrl = "osu.ppy.sh";

		public static readonly Patch[] PatchList =
		{
			new Patch("\"Unsigned executable\" fix", true, () =>
			{
				try
				{
					/*	--   REMOVE THIS   \/ \/ \/ \/ \/ \/ \/
						if (!AuthenticodeTools.IsTrusted(OsuMain.get_Filename()))
						{
							new ErrorDialog(new Exception("Unsigned executable!"), false).ShowDialog();
							Environment.Exit(0);
						}
					*/

					CMain.ObfOsuExplorer["osu.OsuMain"]["Main"].Editor.LocateAndNop(new[]
					{
						OpCodes.Call,
						OpCodes.Call,
						OpCodes.Brtrue_S,
						null, // obfuscator's string stuff, may be either in decrypted or encrypted state.
						null, // --
						OpCodes.Newobj,
						OpCodes.Ldc_I4_0,
						OpCodes.Newobj,
						OpCodes.Call,
						OpCodes.Pop,
						OpCodes.Ldc_I4_0,
						OpCodes.Call
					});

					return true;
				}
				catch { return false; }
			}),
			new Patch("Bancho MD5 hash of osu!.exe fix", true, () =>
			{
				try
				{
					var method = CMain.ObfOsuExplorer["osu.Online.BanchoClient"]["initializePrivate"];

					// array5[0] = CryptoHelper.GetMd5(OsuMain.get_FullPath());
					method.Editor.Locate(new[]
					{
						OpCodes.Call,
						OpCodes.Call,
						OpCodes.Stelem_Ref,
						OpCodes.Dup,
						OpCodes.Ldc_I4_1,
						null,
						null,
						OpCodes.Stelem_Ref,
						OpCodes.Dup
					});

					method.Editor.Nop(2);

					// replace CryptoHelper.GetMd5(OsuMain.get_FullPath()) with "ORIGINAL_MD5_HASH"
					method.Editor.Insert(Instruction.Create(OpCodes.Ldstr, CMain.ObfOsuHash));

					return true;
				}
				catch { return false; }
			}),
			new Patch("Persistent supporter rank", true, () =>
			{
				try
				{
					var receive = CMain.ObfOsuExplorer["osu.Online.BanchoClient"]["receive"];

					receive.Editor.Locate(new[]
					{
						OpCodes.Call,
						OpCodes.Stsfld,
						OpCodes.Ldsfld,
						OpCodes.Ldsfld,
						OpCodes.Dup,
						OpCodes.Brtrue_S
					});

					// | 4 (supp rank)

					receive.Editor.Insert(new[]
					{
						/*Instruction.Create(OpCodes.Ldc_I4_4),
						Instruction.Create(OpCodes.Not),
						Instruction.Create(OpCodes.And)*/
						
						Instruction.Create(OpCodes.Ldc_I4_4),
						Instruction.Create(OpCodes.Or),
					});

					// --- TEMP REMOVE PERMISSION FOR TESTING ^^^

					/*var method = CMain.ObfOsuExplorer["osu.GameModes.Menus.Menu"]["checkPermissions"];

					var opc = new[]
					{
						OpCodes.Ldsfld,
						OpCodes.Call,
						OpCodes.Ldc_I4_4,
						OpCodes.And,
						OpCodes.Ldc_I4_0,
						OpCodes.Ble
					};

					method.Editor.Locate(opc);
					method.Editor.Nop(opc.Length);*/

					return true;
				}
				catch { return false; }
			}),
			new Patch("Local offset change while paused", true, () =>
			{
				try
				{
					// literally first 10 instructions

					CMain.ObfOsuExplorer["osu.GameModes.Play.Player"]["ChangeCustomOffset"].Editor.LocateAndNop(new[]
					{
						OpCodes.Ldsfld,   // Player::Paused
						OpCodes.Brtrue,   // ret
						OpCodes.Ldsfld,   // Player::Unpausing
						OpCodes.Brtrue,   // ret
						OpCodes.Ldsfld,   // --
						OpCodes.Ldarg_0,  // --
						OpCodes.Ldfld,	  // -- 
						OpCodes.Ldc_I4,	  // --
						OpCodes.Add,	  // --
						OpCodes.Ble_S,	  // ^^ AudioEngine.Time > this.firstHitTime + 10000 && ...
						OpCodes.Ldsfld,	  // --
						OpCodes.Brtrue_S, // --
						OpCodes.Ldsfld,	  // --
						OpCodes.Brtrue_S, // ^^ ... !GameBase.TestMode && !EventManager.BreakMode
						OpCodes.Ret
					});

					return true;
				}
				catch { return false; }
			}),
			new Patch("Patcher addon", true, () =>
			{
				try
				{
					#region Patch() function

					var type = CMain.ObfOsuExplorer["osu_common.Updater.CommonUpdater"];
					var MoveInPlace = type["MoveInPlace"];

					/*
						if (CommonUpdater.SafelyMove(text, fileName, 200, 5, allowDefiniteMove))
						{
							if(fileName == "osu!.exe") // <======== INSERTS THIS
								Patch();			   // <========
							
							CommonUpdater.Log("{0} => {1}: OK", new object[]
							{
								text,
								fileName
							});
							ConfigManagerCompact.Configuration["h_" + fileName] = CommonUpdater.getMd5(fileName, false);
						}
					*/

					MoveInPlace.Editor.Locate(new[]
					{
						null,
						null,
						OpCodes.Ldc_I4_2,
						OpCodes.Newarr,
						OpCodes.Dup,
						OpCodes.Ldc_I4_0,
						OpCodes.Ldloc_0,
						OpCodes.Stelem_Ref,
						OpCodes.Dup,
						OpCodes.Ldc_I4_1
					});

					MethodDefUser patchMethod = PatchAddon_CreatePatchMethod();
					type.Type.Methods.Add(patchMethod);

					var op_Equality = CMain.ObfOsuModule.CreateMethodRef(true, typeof(String), "op_Equality", typeof(bool), typeof(string), typeof(string));

					MoveInPlace.Editor.Insert(new[] // if (fileName == "osu!.exe") Patch();
					{
						Instruction.Create(OpCodes.Ldloc_1),
						Instruction.Create(OpCodes.Ldstr, "osu!.exe"),
						Instruction.Create(OpCodes.Call, op_Equality),
						Instruction.Create(OpCodes.Brfalse, MoveInPlace[MoveInPlace.Editor.Position]), // at this moment Position is the LAST instruction so that means brtrue to pos AFTER inserted instructions
						Instruction.Create(OpCodes.Call, patchMethod)
					});

					MoveInPlace.Body.SimplifyBranches();
					MoveInPlace.Body.OptimizeBranches();

					#endregion
					#region Update only if there's an actual update, not just hash is invalid

					var doUpdate = type["doUpdate"];

					/* 
						  After:
						  DownloadableFileInfo downloadableFileInfo = enumerator.Current;
						  if(flag)
						  {
						  ...
					*/

					doUpdate.Editor.Locate(new[]
					{
						OpCodes.Ldloc_S,
						OpCodes.Brfalse,
						null,
						null,
						OpCodes.Ldc_I4_0,
						OpCodes.Newarr,
						OpCodes.Call
					});

					int brfalseOcc = doUpdate.Editor.Locate(new[]
					{
						OpCodes.Ldloca_S,
						OpCodes.Call,
						OpCodes.Brtrue,
						OpCodes.Leave,
						OpCodes.Ldloc_0,
						OpCodes.Ldfld,
						null,
						null,
						OpCodes.Call
					}, false);

					FieldDef fldFilename = CMain.ObfOsuModule.Find("osu_common.Updater.UpdaterFileInfo", false)?.FindField("filename");
					FieldDef fldHash = CMain.ObfOsuModule.Find("osu_common.Updater.UpdaterFileInfo", false)?.FindField("file_hash");

					if(fldFilename == null || fldHash == null)
						return false;

					var op_Inequality = CMain.ObfOsuModule.CreateMethodRef(true, typeof(String), "op_Inequality", typeof(bool), typeof(string), typeof(string));

					doUpdate.Editor.Insert(new[] // if (downloadableFile.filename != "osu!.exe" || downloadableFile.file_hash != ORIGINAL_OSU_HASH)
					{
						Instruction.Create(OpCodes.Ldloc_0),
						Instruction.Create(OpCodes.Ldfld, fldFilename),
						Instruction.Create(OpCodes.Ldstr, "osu!.exe"),
						Instruction.Create(OpCodes.Call, op_Inequality),
						Instruction.Create(OpCodes.Brtrue, doUpdate[doUpdate.Editor.Position]),
						Instruction.Create(OpCodes.Ldloc_0),
						Instruction.Create(OpCodes.Ldfld, fldHash),
						Instruction.Create(OpCodes.Ldstr, CMain.ObfOsuHash),
						Instruction.Create(OpCodes.Call, op_Inequality),
						Instruction.Create(OpCodes.Brfalse, doUpdate[brfalseOcc])
					});

					doUpdate.Body.SimplifyBranches();
					doUpdate.Body.OptimizeBranches();

					#endregion

					return true;
				}
				catch { return false; }
			}),
			new Patch("Switch servers to asuki.me", false, () =>
			{
				try
				{
					var method = CMain.ObfOsuExplorer["osu.Online.BanchoClient"].FindRaw(".cctor");

					var declStart = method.Editor.Locate(new[]
					{
						null,
						OpCodes.Newarr,
						OpCodes.Dup,
						OpCodes.Ldc_I4_0,
						null, // ezstr
						null, // --
						OpCodes.Stelem_Ref,
					});

					var toRemove = method.Editor.LocateAt(declStart, new[]
					{
						OpCodes.Stelem_Ref,
						OpCodes.Stsfld
					}, false) - declStart + 1;

					method.Editor.Remove(toRemove);
					method.Editor.Insert(AsukiAddon_CreateServersArrayInitializer(new[]
					{
						"ripple.moe",
						"51.15.223.146"
					}));

					// TODO replace all ldstrs in all method bodies

					var baseUrlField = new FieldDefUser("OsuBaseUrl", new FieldSig(CMain.ObfOsuModule.CorLibTypes.String), FieldAttributes.Public | FieldAttributes.Static);

					var urlsType = CMain.ObfOsuExplorer["osu.Urls"].Type;

					urlsType.Fields.Add(baseUrlField);

					var methods = new MemberFinder().FindAll(CMain.ObfOsuModule).MethodDefs.Select(x => x.Key);

					foreach (var meth in methods)
					{
						var editor = new MethodExplorer(null, meth).Editor;

						if(editor is null) // body is null
							continue;

						for (int i = 0; i < editor.Count; i++)
						{
							var instr = editor[i];

							if (instr.OpCode == OpCodes.Ldstr)
							{
								string str;

								if (instr.Operand is UTF8String)
									str = instr.Operand as UTF8String;
								else if (instr.Operand is string)
									str = instr.Operand as string;
								else continue;

								if (str.Contains(OsuBaseUrl))
								{
									editor.Replace(i, AsukiAddon_UniversalizeOsuURL(str, baseUrlField));
									Console.WriteLine("deb | " + str);
									// Console.WriteLine($"{meth.DeclaringType.Name}::{meth.Name}");
								}
								else if (str.Contains("ppy.sh"))
									Console.WriteLine("deb | " + str);
							}
						}
					}

					var cctorEditor = new MethodExplorer(null, urlsType.FindMethod(".cctor")).Editor;

					cctorEditor.InsertAt(cctorEditor.Count - 1, new[]
					{
						Instruction.Create(OpCodes.Ldstr, "ripple.moe"),
						Instruction.Create(OpCodes.Stsfld, baseUrlField),
					});

					return true;
				}
				catch { return false; }
			}),

			#region Disabled
			/*
			enum SubmitMode
			{
				None,
				WithoutNoFail,
				DontSubmit,
				WithNoFail
			}

			new Patch("NoFail submit options", false, () =>
			{
				
				OsuFindCollection res = CMain.FindTypeMethod("osu.GameModes.Play.Player", "Dispose");

				if(!res.Success)
					return false;

				TypeDef Player = res.Type;
				MethodDef Dispose = res.Method;

				#region Add TheoreticallyFailed and SubmitMode
				MethodDef cctor = Player.FindMethod(".cctor");

				if(cctor == null)
					return false;

				//Theoretically failed
				FieldDefUser TheoreticallyFailed = new FieldDefUser("TheoreticallyFailed", new FieldSig(CMain.OsuModule.CorLibTypes.Boolean), FieldAttributes.Public | FieldAttributes.Static);
				Player.Fields.Add(TheoreticallyFailed);


				cctor.Editor.Insert(0, new[]
				{
					Instruction.Create(OpCodes.Ldc_I4_0),
					Instruction.Create(OpCodes.Stsfld, TheoreticallyFailed)
				});
				// --

				// Submit mode // 0 = did not yet choose; 1 - withOUT nofail; 2 - don't submit; 3 - WITH nofail
				FieldDefUser SubmitModeDef = new FieldDefUser("SubmitMode", new FieldSig(CMain.OsuModule.CorLibTypes.SByte), FieldAttributes.Public | FieldAttributes.Static);
				Player.Fields.Add(SubmitModeDef);

				cctor.Body.Instructions.Insert(0, new Instruction[]
				{
					Instruction.Create(OpCodes.Ldc_I4_M1),
					Instruction.Create(OpCodes.Stsfld, SubmitModeDef)
				});
				// --
				#endregion
				#region Default TheoreticallyFailed and SubmitMode at Dispose
				int occurence = Dispose.Body.Instructions.FindOccurence(new OpCode[]
				{
					OpCodes.Ldsfld,
					OpCodes.Ldc_I4_1,
					OpCodes.Add,
					OpCodes.Stsfld,
					OpCodes.Ldsfld,
					OpCodes.Brfalse_S,
					OpCodes.Ldsfld,
					OpCodes.Ldc_I4_1
				});

				if(occurence == -1)
					return false;

				Dispose.Body.Instructions.Insert(occurence, new Instruction[]
				{
					Instruction.Create(OpCodes.Ldc_I4_0),
					Instruction.Create(OpCodes.Stsfld, TheoreticallyFailed),
					Instruction.Create(OpCodes.Ldc_I4_M1),
					Instruction.Create(OpCodes.Stsfld, SubmitModeDef)
				});
				#endregion
				#region TheoreticallyFailed at Update
				MethodDef Update = Player.FindMethodObf("Update");

				if(Update == null)
					return false;

				int occurence2 = Update.Body.Instructions.FindOccurence(new OpCode[]
				{
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Brfalse,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Callvirt
				}) + 1; // !

				FieldDef Ruleset = Player.FindFieldObf("Ruleset");
				FieldDef HpBar = CMain.OsuModule.FindObf("osu.GameModes.Play.Rulesets.Ruleset")?.FindFieldObf("HpBar");
				MethodDef GetCurrentHp = HpBar.GetTypeDef()?.FindMethodObf("get_CurrentHp");

				if(Ruleset == null || HpBar == null || GetCurrentHp == null)
					return false;

				Instruction Ldarg = Instruction.Create(OpCodes.Ldarg_0);
				Update.Body.Instructions.Insert(occurence2, Ldarg);

				Update.Body.Instructions.Insert(occurence2, new Instruction[] // if (this.RuleSet.HpBar != null && this.RuleSet.HpBar.get_CurrentHp() <= 0.0) TF = true;
				{
					Instruction.Create(OpCodes.Ldfld, Ruleset),
					Instruction.Create(OpCodes.Ldfld, HpBar),
					Instruction.Create(OpCodes.Brfalse, Ldarg),
					Instruction.Create(OpCodes.Ldarg_0),
					Instruction.Create(OpCodes.Ldfld, Ruleset),
					Instruction.Create(OpCodes.Ldfld, HpBar),
					Instruction.Create(OpCodes.Callvirt, GetCurrentHp),
					Instruction.Create(OpCodes.Ldc_R8, 0.0),
					Instruction.Create(OpCodes.Bgt_Un, Ldarg),
					Instruction.Create(OpCodes.Ldc_I4_1),
					Instruction.Create(OpCodes.Stsfld, TheoreticallyFailed),
				});
				#endregion
				#region HandleScoreSubmission dialog
				var currentScore = Player?.FindFieldObf("currentScore"); // Player.currentScore
				var EnabledMods = currentScore?.GetTypeDef()?.FindFieldObf("EnabledMods"); // Score.EnabledMods
				var ModStatus = CMain.OsuModule.FindObf("osu.GameplayElements.Scoring.ModManager")?.FindFieldObf("ModStatus");

				var SubmitWithoutNoFail = NoFailAddon_CreateSubmitOption(SubmitModeDef, SubmitMode.WithoutNoFail);
				var SubmitWithNoFail = NoFailAddon_CreateSubmitOption(SubmitModeDef, SubmitMode.WithNoFail);
				var ShowSubmitModeDialog = NoFailAddon_CreateShowSubmitModeDialog(SubmitWithoutNoFail, SubmitWithNoFail);

				Player.Methods.Add(SubmitWithoutNoFail);
				Player.Methods.Add(SubmitWithNoFail);
				Player.Methods.Add(ShowSubmitModeDialog);

				var instructions = Update.Body.Instructions;

				int occurence3 = instructions.FindOccurence(new OpCode[] 
				{
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Ldc_I4_1,
					OpCodes.Sub,
					OpCodes.Callvirt,
					OpCodes.Ldfld,
					OpCodes.Ldc_I4
				}); // before DoPass checks

				if(occurence3 == -1)
					return false;

				int occurence4 = instructions.FindOccurence(new OpCode[]
				{
					OpCodes.Ldsfld,
					OpCodes.Brfalse,
					OpCodes.Ldsfld,
					OpCodes.Brfalse,
					OpCodes.Ldsfld,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Ldc_I4_0,
					OpCodes.Callvirt,
					OpCodes.Ldfld,
					OpCodes.Ble,
				}); // After DoPassChecks (after if's body)

				if(occurence4 == -1)
					return false;

				instructions.Insert(occurence3, new Instruction[]
				{
					// if sm == -1
					Instruction.Create(OpCodes.Ldsfld, SubmitModeDef),
					Instruction.Create(OpCodes.Ldc_I4_M1),
					Instruction.Create(OpCodes.Nop), // bne.un.s to next statement
					Instruction.Create(OpCodes.Ldsfld, currentScore),
					Instruction.Create(OpCodes.Ldfld, EnabledMods),
					Instruction.Create(OpCodes.Ldc_I4_1),
					Instruction.Create(OpCodes.And),
					Instruction.Create(OpCodes.Nop), // brfalse.s to next statement
					Instruction.Create(OpCodes.Ldsfld, TheoreticallyFailed),
					Instruction.Create(OpCodes.Nop), // brfalse.s to next statement
					Instruction.Create(OpCodes.Ldc_I4_0),
					Instruction.Create(OpCodes.Stsfld, SubmitModeDef), // sm = 0 (choosing)
					Instruction.Create(OpCodes.Call, ShowSubmitModeDialog),

					// if sm == 1 (withOUT nofail)
					Instruction.Create(OpCodes.Ldsfld, SubmitModeDef),
					Instruction.Create(OpCodes.Ldc_I4_1),
					Instruction.Create(OpCodes.Nop), // bne.un.s  to next statement
					Instruction.Create(OpCodes.Ldsfld, currentScore),
					Instruction.Create(OpCodes.Ldfld, EnabledMods),
					Instruction.Create(OpCodes.Ldc_I4_1),
					Instruction.Create(OpCodes.Not),
					Instruction.Create(OpCodes.And),
					Instruction.Create(OpCodes.Dup),
					Instruction.Create(OpCodes.Dup),
					Instruction.Create(OpCodes.Stfld, EnabledMods),
					Instruction.Create(OpCodes.Stsfld, ModStatus),
					// --

					// if sm == 2 (don't submit)
					Instruction.Create(OpCodes.Ldsfld, SubmitModeDef),
					Instruction.Create(OpCodes.Ldc_I4_2), // TODO ME PLZZ!1!!!1
					Instruction.Create(OpCodes.Nop), // bne.un.s  to next statement
					// --

					// Extension to next if statement
					Instruction.Create(OpCodes.Ldsfld, SubmitModeDef),
					Instruction.Create(OpCodes.Ldc_I4_0),
					Instruction.Create(OpCodes.Beq_S, instructions[occurence4]) // if(sm == 0) then wait for user to choose (don't exec DoPass)
				});


				instructions[occurence3 +  2].OpCode =
				instructions[occurence3 +  15].OpCode =
				instructions[occurence3 + 27].OpCode = OpCodes.Bne_Un_S;

				instructions[occurence3 + 7].OpCode = OpCodes.Brfalse_S;
				instructions[occurence3 + 9].OpCode = OpCodes.Brtrue_S;

				instructions[occurence3 +  2].Operand =
				instructions[occurence3 +  7].Operand =
				instructions[occurence3 +  9].Operand = instructions[occurence3 + 13]; // TODO any explanatory comments are appreciated
				instructions[occurence3 +  15].Operand = instructions[occurence3 + 25];
				instructions[occurence3 + 27].Operand = instructions[occurence3 + 28];

				#endregion
				

				return true;
			}), // Doesn't work needs a redo but NOT NOW
			*/
			
			/*
			new Patch("Turn updates off", () =>
			{
				OpCode[] opc =
				{
					OpCodes.Ldloc_S,
					OpCodes.Brfalse,
					OpCodes.Ldc_I4,
					OpCodes.Call,
					OpCodes.Ldc_I4_0,
					OpCodes.Newarr,
					OpCodes.Call
				};

				OpCode[] opcBrtrue =
				{
					OpCodes.Ldloca_S,
					OpCodes.Call,
					OpCodes.Brtrue,
					OpCodes.Leave_S,
					OpCodes.Ldloca_S,
					OpCodes.Constrained
				};

				MethodDef method = Program.OsuModule.FindObf("osu_common.Updater.CommonUpdater").FindMethodObf("doUpdate");

				if(method == null)
					return false;

				int occurence = method.Body.Instructions.FindOccurence(opc);

				if(occurence == -1)
					return false;

				int occurenceBrtrue = method.Body.Instructions.FindOccurence(opcBrtrue);

				if(occurenceBrtrue == -1)
					return false;

				FieldDef ldfldField = Program.OsuModule.Find("osu_common.Updater.UpdaterFileInfo", false).FindField("filename");

				if(ldfldField == null)
					return false;

				TypeRefUser stringRef = new TypeRefUser(Program.OsuModule, "System", "String", Program.OsuModule.CorLibTypes.AssemblyRef);

				MemberRefUser op_Inequality = new MemberRefUser(Program.OsuModule, "op_Equality",
						MethodSig.CreateStatic(Program.OsuModule.CorLibTypes.Boolean, Program.OsuModule.CorLibTypes.String, Program.OsuModule.CorLibTypes.String),
						stringRef);

				Instruction[] instructions =
				{
					Instruction.Create(OpCodes.Ldloc_0),
					Instruction.Create(OpCodes.Ldfld, ldfldField),
					Instruction.Create(OpCodes.Ldstr, "osu!.exe"),
					Instruction.Create(OpCodes.Call, op_Inequality),
					Instruction.Create(OpCodes.Brtrue, method.Body.Instructions[occurenceBrtrue]) // to insert
				};

				method.Body.Instructions.Insert(occurence, instructions);

				return true;
			}),
			*/
			#endregion
		};

		private static MethodDefUser PatchAddon_CreatePatchMethod()
		{
			MethodImplAttributes implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;

			MethodDefUser method = new MethodDefUser("Patch", MethodSig.CreateStatic(CMain.ObfOsuModule.CorLibTypes.Void), implFlags, flags);

			CilBody body = new CilBody();
			method.Body = body;

			var FileExists = CMain.ObfOsuModule.CreateMethodRef(true, typeof(File), "Exists", typeof(bool), typeof(string));
			var Process = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Process), ".ctor", typeof(void));
			var ProcessGetStartInfo = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Process), "get_StartInfo", typeof(ProcessStartInfo));
			var ProcessStartInfoSetFileName = CMain.ObfOsuModule.CreateMethodRef(false, typeof(ProcessStartInfo), "set_FileName", typeof(void), typeof(string));
			var ProcessStartInfoSetArguments = CMain.ObfOsuModule.CreateMethodRef(false, typeof(ProcessStartInfo), "set_Arguments", typeof(void), typeof(string));
			var AssemblyGetExecutingAssembly = CMain.ObfOsuModule.CreateMethodRef(true, typeof(Assembly), "GetExecutingAssembly", typeof(Assembly));
			var AssemblyGetLocation = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Assembly), "get_Location", typeof(string));
			var PathGetDirectoryName = CMain.ObfOsuModule.CreateMethodRef(true, typeof(Path), "GetDirectoryName", typeof(string), typeof(string));
			var ProcessStartInfoSetWorkingDirectory = CMain.ObfOsuModule.CreateMethodRef(false, typeof(ProcessStartInfo), "set_WorkingDirectory", typeof(void), typeof(string));
			var ProcessStart = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Process), "Start", typeof(bool));
			var ProcessWaitForExit = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Process), "WaitForExit", typeof(void));
			var ProcessGetExitCode = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Process), "get_ExitCode", typeof(int));
			var FileCreate = CMain.ObfOsuModule.CreateMethodRef(true, typeof(File), "Create", typeof(FileStream), typeof(string));
			var StreamClose = CMain.ObfOsuModule.CreateMethodRef(false, typeof(Stream), "Close", typeof(void));
			// var FileDelete = CMain.ObfOsuModule.CreateMethodRef(true, typeof(File), "Delete", typeof(void), typeof(string));
			// var FileMove = CMain.ObfOsuModule.CreateMethodRef(true, typeof(File), "Move", typeof(void), typeof(string), typeof(string));

			var SafelyMove = CMain.FindTypeMethod("osu_common.Updater.CommonUpdater", "SafelyMove").Method;

			if (SafelyMove == null)
				return null;

			Instruction[] instructions = // i honestly don't know what the fuck is this because i didn't write original code down
			{
				/* 0 */ Instruction.Create(OpCodes.Ldstr, "osu!patch\\osu!patch.exe"),
				/* 1 */ Instruction.Create(OpCodes.Call, FileExists),
				/* 2 */ Instruction.Create(OpCodes.Nop),
				/* 3 */ Instruction.Create(OpCodes.Newobj, Process),
				/* 4 */ Instruction.Create(OpCodes.Dup),
				/* 5 */ Instruction.Create(OpCodes.Callvirt, ProcessGetStartInfo),
				/* 6 */ Instruction.Create(OpCodes.Ldstr, "osu!patch\\osu!patch.exe"),
				/* 7 */ Instruction.Create(OpCodes.Callvirt, ProcessStartInfoSetFileName),
				/* 8 */ Instruction.Create(OpCodes.Dup),
				/* 9 */ Instruction.Create(OpCodes.Callvirt, ProcessGetStartInfo),
				/* 10 */ Instruction.Create(OpCodes.Ldstr, "osu!patch\\clean.exe osu!.exe"),
				/* 11 */ Instruction.Create(OpCodes.Callvirt, ProcessStartInfoSetArguments),
				/* 12 */ Instruction.Create(OpCodes.Dup),
				/* 13 */ Instruction.Create(OpCodes.Callvirt, ProcessGetStartInfo),

				/* 14 */ Instruction.Create(OpCodes.Call, AssemblyGetExecutingAssembly),
				/* 15 */ Instruction.Create(OpCodes.Callvirt, AssemblyGetLocation),
				/* 16 */ Instruction.Create(OpCodes.Call, PathGetDirectoryName),
				/* 17 */ Instruction.Create(OpCodes.Callvirt, ProcessStartInfoSetWorkingDirectory),

				/* 18 */ Instruction.Create(OpCodes.Dup),
				/* 19 */ Instruction.Create(OpCodes.Callvirt, ProcessStart),
				/* 20 */ Instruction.Create(OpCodes.Pop),
				/* 21 */ Instruction.Create(OpCodes.Dup),
				/* 22 */ Instruction.Create(OpCodes.Callvirt, ProcessWaitForExit),
				/* 23 */ Instruction.Create(OpCodes.Callvirt, ProcessGetExitCode),
				/* 24 */ Instruction.Create(OpCodes.Nop),
				/* 25 */ Instruction.Create(OpCodes.Ldstr, "osu!-osupatch.exe"),
				/* 26 */ Instruction.Create(OpCodes.Ldstr, "osu!.exe"),
				/* 27 */ Instruction.Create(OpCodes.Ldc_I4, 200),
				/* 28 */ Instruction.Create(OpCodes.Ldc_I4_5),
				/* 29 */ Instruction.Create(OpCodes.Ldc_I4_1),
				/* 30 */ Instruction.Create(OpCodes.Call, SafelyMove),
				/* 31 */ Instruction.Create(OpCodes.Pop),
				/* 32 */ Instruction.Create(OpCodes.Ret),
				/* 33 */ Instruction.Create(OpCodes.Ldstr, ".osu!patch_failed"),
				/* 34 */ Instruction.Create(OpCodes.Call, FileCreate),
				/* 35 */ Instruction.Create(OpCodes.Callvirt, StreamClose),
				/* 36 */ Instruction.Create(OpCodes.Ret)
			};

			instructions[2] = Instruction.Create(OpCodes.Brfalse_S, instructions[36]);
			instructions[24] = Instruction.Create(OpCodes.Brtrue_S, instructions[33]);

			body.Instructions.Insert(0, instructions);

			body.MaxStack = 5;

			body.SimplifyBranches();
			body.OptimizeBranches();

			return method;
		}

		private static IList<Instruction> AsukiAddon_CreateServersArrayInitializer(IList<string> addrList)
		{
			var ret = new List<Instruction>();

			ret.AddRange(new[]
			{
				Instruction.CreateLdcI4(addrList.Count),
				Instruction.Create(OpCodes.Newarr, CMain.ObfOsuModule.CorLibTypes.String)
			});

			for(int i = 0; i < addrList.Count; i++)
			{
				ret.AddRange(new[]
				{
					Instruction.Create(OpCodes.Dup),
					Instruction.CreateLdcI4(i),
					Instruction.Create(OpCodes.Ldstr, addrList[i]),
					Instruction.Create(OpCodes.Stelem_Ref)
				});
			}

			return ret;
		}

		public static IList<Instruction> AsukiAddon_UniversalizeOsuURL(string str, FieldDef baseUrlField, string baseUrl = OsuBaseUrl)
		{
			var parts = str.Split(new[] { baseUrl }, StringSplitOptions.None);

			if (parts.Length == 2)
			{
				var sb = new ILStringBuilder();
				
				if(!String.IsNullOrEmpty(parts[0]))
					sb.Add(parts[0]);

				sb.Add(Instruction.Create(OpCodes.Ldsfld, baseUrlField));

				if (!String.IsNullOrEmpty(parts[1]))
					sb.Add(parts[1]);

				return sb.Instructions;
			}

			return new List<Instruction> { Instruction.Create(OpCodes.Ldstr, str) };
		}

		class ILStringBuilder
		{
			public List<Instruction> Instructions = new List<Instruction>();

			private byte _stackCounter;
			
			private static readonly MemberRef StringConcat = CMain.ObfOsuModule.CreateMethodRef(true, typeof(String), "Concat", typeof(string), typeof(string), typeof(string));

			public void Add(Instruction ins)
			{
				Instructions.Add(ins);
				_stackCounter++;

				if (_stackCounter >= 2)
				{
					Instructions.Add(Instruction.Create(OpCodes.Call, StringConcat));
					_stackCounter = 1;
				}
			}

			public void Add(string str) =>
				Add(Instruction.Create(OpCodes.Ldstr, str));
		}

		#region Mentally disabled
		/*
		private static MethodDefUser NoFailAddon_CreateShowSubmitModeDialog(MethodDef SubmitWithoutNoFail, MethodDef SubmitWithNoFail)
		{
			MethodImplAttributes implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;

			MethodDefUser method = new MethodDefUser("ShowNoFailDialog", MethodSig.CreateStatic(CMain.OsuModule.CorLibTypes.Void), implFlags, flags);

			CilBody body = new CilBody();
			method.Body = body;

			var GameBase_ShowDialog = CMain.OsuModule.FindObf("osu.GameBase")?.FindMethodObf("ShowDialog");

			var Player = CMain.OsuModule.FindObf("osu.GameModes.Play.Player");
			var Player_currentScore = Player?.FindFieldObf("currentScore");
			var Score = Player_currentScore?.GetTypeDef();
			var Score_EnabledMods = Score?.FindFieldObf("EnabledMods");
			var Score_submit = Score?.FindMethodObf("submit"); // Submit != submit BE CAREFUL

			var pDialog = CMain.OsuModule.FindObf("osu.Graphics.UserInterface.pDialog");
			var pDialog_ctor = pDialog?.FindMethod(".ctor");
			var pDialog_AddOption = pDialog?.FindMethodObf("AddOption");

			var XnaColor = CMain.OsuModule.FindReflection("Microsoft.Xna.Framework.Graphics.Color");
			var XnaColor_get_YellowGreen = XnaColor?.FindMethod("get_YellowGreen");

			var EventHandler_ctor = CMain.OsuModule.CreateMethodRef(false, typeof(EventHandler), ".ctor", typeof(void), typeof(object), typeof(IntPtr));

			if (GameBase_ShowDialog == null || Player == null || Player_currentScore == null || Score == null || Score_EnabledMods == null || Score_submit == null || XnaColor == null ||
				pDialog == null || pDialog_AddOption == null || XnaColor == null || XnaColor_get_YellowGreen == null || EventHandler_ctor == null)
				return null;

			Instruction Ret = Instruction.Create(OpCodes.Ret);

			method.Body.Instructions.Insert(0, Ret);

			/*
			pDialog pDialog = new pDialog("You played with NoFail yet didn't fail.", true);
			pDialog.AddOption("Submit without NoFail", Color.YellowGreen, new EventHandler(MyType.SubmitWithoutNoFail), true, false, true);
			pDialog.AddOption("Don't submit", Color.YellowGreen, null, true, false, true);
			pDialog.AddOption("Submit WITH NoFail", Color.YellowGreen, new EventHandler(MyType.SubmitWithNoFail), true, false, true);
			GameBase.ShowDialog(pDialog);
			*\/

			body.Instructions.Insert(0, new Instruction[]
			{
				Instruction.Create(OpCodes.Ldstr, "You played with NoFail yet didn't fail."),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Newobj, pDialog_ctor),
				Instruction.Create(OpCodes.Dup),
				Instruction.Create(OpCodes.Ldstr, "Submit without NoFail"),
				Instruction.Create(OpCodes.Call, XnaColor_get_YellowGreen),
				Instruction.Create(OpCodes.Ldnull),
				Instruction.Create(OpCodes.Ldftn, SubmitWithoutNoFail),
				Instruction.Create(OpCodes.Newobj, EventHandler_ctor),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Ldc_I4_0),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Callvirt, pDialog_AddOption),
				Instruction.Create(OpCodes.Dup),
				Instruction.Create(OpCodes.Ldstr, "Don't submit"),
				Instruction.Create(OpCodes.Call, XnaColor_get_YellowGreen),
				Instruction.Create(OpCodes.Ldnull),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Ldc_I4_0),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Callvirt, pDialog_AddOption),
				Instruction.Create(OpCodes.Dup),
				Instruction.Create(OpCodes.Ldstr, "Submit WITH NoFail"),
				Instruction.Create(OpCodes.Call, XnaColor_get_YellowGreen),
				Instruction.Create(OpCodes.Ldnull),
				Instruction.Create(OpCodes.Ldftn, SubmitWithNoFail),
				Instruction.Create(OpCodes.Newobj, EventHandler_ctor),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Ldc_I4_0),
				Instruction.Create(OpCodes.Ldc_I4_1),
				Instruction.Create(OpCodes.Callvirt, pDialog_AddOption),
				Instruction.Create(OpCodes.Call, GameBase_ShowDialog),
				Instruction.Create(OpCodes.Ret)
			});

			body.MaxStack = 8;

			body.SimplifyBranches();
			body.OptimizeBranches();

			return method;
		}

		private static MethodDefUser NoFailAddon_CreateSubmitOption(FieldDefUser submitModeDef, SubmitMode submitMode)
		{
			MethodImplAttributes implFlags = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;

			MethodDefUser method = new MethodDefUser("SubmitOption_" + submitMode.ToString(), MethodSig.CreateStatic(CMain.OsuModule.CorLibTypes.Void, CMain.OsuModule.CorLibTypes.Object, typeof(DoWorkEventArgs).GetTypeSig()), implFlags, flags);

			CilBody body = new CilBody();
			method.Body = body;

			body.Instructions.Insert(0, new Instruction[]
			{
				Instruction.CreateLdcI4((byte)submitMode),
				Instruction.Create(OpCodes.Stsfld, submitModeDef),
				Instruction.Create(OpCodes.Ret)
			});

			body.MaxStack = 8;

			body.SimplifyBranches();
			body.OptimizeBranches();

			return method;
		}
		*/
		#endregion
	}
}