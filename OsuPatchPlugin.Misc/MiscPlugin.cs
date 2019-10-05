using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch;
using osu_patch.Custom;
using osu_patch.Misc;
using System;
using System.Collections.Generic;

namespace OsuPatchPlugin.Misc
{
	public class MiscPlugin : IOsuPatchPlugin
	{
		public IEnumerable<Patch> GetPatches() => new[]
		{
			new Patch("Local offset change while paused", true, (patch, exp) =>
			{
				// literally first 10 instructions
				exp["osu.GameModes.Play.Player"]["ChangeCustomOffset"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld, // Player::Paused
					OpCodes.Brtrue, // ret
					OpCodes.Ldsfld, // Player::Unpausing
					OpCodes.Brtrue, // ret
					OpCodes.Ldsfld, // --
					OpCodes.Ldarg_0, // --
					OpCodes.Ldfld, // -- 
					OpCodes.Ldc_I4, // --
					OpCodes.Add, // --
					OpCodes.Ble_S, // ^^ AudioEngine.Time > this.firstHitTime + 10000 && ...
					OpCodes.Ldsfld, // --
					OpCodes.Brtrue_S, // --
					OpCodes.Ldsfld, // --
					OpCodes.Brtrue_S, // ^^ ... !GameBase.TestMode && !EventManager.BreakMode
					OpCodes.Ret
				});

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Saving failed replays", true, (patch, exp) =>
			{
				var Player = exp["osu.GameModes.Play.Player"];
				var onKeyPressed = Player["onKeyPressed"];

				var loc = onKeyPressed.Editor.Locate(new[]
				{
					OpCodes.Ldsfld,    // 0 
					OpCodes.Brfalse,   // 1
					OpCodes.Call,	   // 2
					OpCodes.Brtrue,	   // 3
					OpCodes.Ldsfld,    // 4 
					OpCodes.Brfalse_S, // 5
					OpCodes.Ldarg_2,   // 6
					OpCodes.Ldc_I4_S,  // 7
					OpCodes.Bne_Un_S,  // 8
					OpCodes.Ldsfld,	   // 9
					OpCodes.Brfalse_S, // 10
					OpCodes.Ldarg_0,   // 11 // NOP
					OpCodes.Call,	   // 12 // NOP // this.HandleScoreSubmission();
					OpCodes.Ldsfld,	   // 13
					OpCodes.Stsfld,	   // 14
					OpCodes.Ldc_I4_1,  // 15
					OpCodes.Call,	   // 16
					OpCodes.Ldc_I4_1,  // 17
					OpCodes.Stsfld,	   // 18
					OpCodes.Ldc_I4_2,  // 19
					OpCodes.Ldc_I4_1,  // 20
					OpCodes.Ldc_I4_0,  // 21
					OpCodes.Call,	   // 22
					OpCodes.Ldc_I4_1,  // 23
					OpCodes.Ret		   // 24 
				}, false);

				var ExportReplay = exp["osu.GameplayElements.Scoring.ScoreManager"]["ExportReplay"].Method;
				var currentScore = Player.FindField("currentScore");

				onKeyPressed.Editor.NopAt(loc + 11, 2);

				onKeyPressed.Editor.InsertAt(loc + 15, new[]
				{
					Instruction.Create(OpCodes.Ldsfld, currentScore),
					Instruction.Create(OpCodes.Ldc_I4_1),
					Instruction.Create(OpCodes.Call, ExportReplay),
					Instruction.Create(OpCodes.Pop)
				});

				/*
				if (Player.Failed && k == Keys.F1 && Player.currentScore != null)
				{
					this.HandleScoreSubmission();
					InputManager.ReplayScore = Player.currentScore;
					ScoreManager.ExportReplay(Player.currentScore, true); <<< Inserts this
					InputManager.set_ReplayMode(true);
					InputManager.ReplayToEnd = true;
					GameBase.ChangeModeInstant(OsuModes.Play, true, 0);
					return true;
				} 
				*/
				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("No minimum delay before pausing again", true, (patch, exp) =>
			{
				// first 27 instructions
				exp["osu.GameModes.Play.Player"]["togglePause"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Call,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Brfalse_S,
					OpCodes.Ldsfld,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Sub,
					OpCodes.Ldc_I4,
					OpCodes.Bge_S,
					OpCodes.Ldc_I4,
					OpCodes.Call,
					OpCodes.Call,
					OpCodes.Ret
				});

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("No \"mouse buttons are disabled\" message", true, (patch, exp) =>
			{
				/*
				 *	if (!warningMouseButtonsDisabled && ConfigManager.sMouseDisableButtons)
				 *	{
				 *		warningMouseButtonsDisabled = true;
				 *		NotificationManager.ShowMessage(string.Format(LocalisationManager.GetString(OsuString.InputManager_MouseButtonsDisabledWarning), BindingManager.For(Bindings.DisableMouseButtons)), Color.Beige, 10000);
				 *	}
				 *
				 */

				exp["osu.GameModes.Play.Player"]["Initialize"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Brfalse_S,
					OpCodes.Ldc_I4_1,
					OpCodes.Stsfld,
					OpCodes.Ldc_I4,
					OpCodes.Call,
					OpCodes.Ldc_I4_S,
					OpCodes.Call,
					OpCodes.Box,
					OpCodes.Call,
					OpCodes.Call,
					OpCodes.Ldc_I4,
					OpCodes.Ldnull,
					OpCodes.Call
				});

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Don't send frames to spectators (NoSpec)", true, (patch, exp) =>
			{
				/*
				 *	if (!warningMouseButtonsDisabled && ConfigManager.sMouseDisableButtons)
				 *	{
				 *		warningMouseButtonsDisabled = true;
				 *		NotificationManager.ShowMessage(string.Format(LocalisationManager.GetString(OsuString.InputManager_MouseButtonsDisabledWarning), BindingManager.For(Bindings.DisableMouseButtons)), Color.Beige, 10000);
				 *	}
				 *
				 */

				exp["osu.Online.StreamingManager"]["PurgeFrames"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldc_I4_S,
					OpCodes.Ldsfld,
					OpCodes.Ldarg_0,
					OpCodes.Ldloc_3,
					OpCodes.Ldarg_1,
					OpCodes.Stloc_S,
					OpCodes.Ldloca_S,
					OpCodes.Call,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Br_S,
					OpCodes.Ldloca_S,
					OpCodes.Call,
					OpCodes.Ldsfld,
					OpCodes.Dup,
					OpCodes.Ldc_I4_1,
					OpCodes.Add,
					OpCodes.Stsfld,
					OpCodes.Newobj,
					OpCodes.Call
				});

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Don't send anti-cheat flags to Bancho", true, (patch, exp) =>
			{
				// Startup flags // Remove this part -> (OsuMain.startupValue > 0) ? ("a" + OsuMain.startupValue) // And leave this one -> Scrobbler.last.BeatmapId.ToString()
				exp["osu.Helpers.Scrobbler"]["sendCurrentTrack"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Ldc_I4_0,
					OpCodes.Bgt_S
				});

				/* Submit flags // Remove this \/ \/ \/
				 * for (int i = 0; i < (int)Player.flag; i++)
				 * {
				 *      text += " ";
				 * }
				 */
				exp["osu.GameplayElements.Scoring.Score"]["get_onlineFormatted"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldloc_0,
					OpCodes.Nop,
					OpCodes.Ldstr,
					OpCodes.Call,
					OpCodes.Stloc_0,
					OpCodes.Ldloc_1,
					OpCodes.Ldc_I4_1,
					OpCodes.Add,
					OpCodes.Stloc_1,
					OpCodes.Ldloc_1,
					OpCodes.Ldsfld,
					OpCodes.Blt_S
				});

				return new PatchResult(patch, PatchStatus.Success);;
			})
					
			#region Disabled
			/*
			private const string OSU_BASE_URL = "osu.ppy.sh";

			new Patch("Switch servers to asuki.me", false, (patch, exp) =>
			{
				var method = exp["osu.Online.BanchoClient"].FindMethodRaw(".cctor");

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
				method.Editor.Insert(AsukiPatch_CreateServersArrayInitializer(exp, new[]
				{
					"ripple.moe",
					"51.15.223.146"
				}));

				// TODO replace all ldstrs in all method bodies

				var baseUrlField = new FieldDefUser("OSU_BASE_URL", new FieldSig(exp.Module.CorLibTypes.String), FieldAttributes.Public | FieldAttributes.Static);

				var urlsType = exp["osu.Urls"].Type;

				urlsType.Fields.Add(baseUrlField);

				var methods = new MemberFinder().FindAll(exp.Module).MethodDefs.Select(x => x.Key);

				foreach (var meth in methods)
				{
					var editor = new MethodExplorer(null, meth).Editor;

					if (editor is null) // body is null
						continue;

					for (int i = 0; i < editor.Count; i++)
					{
						var instr = editor[i];

						if (instr.OpCode == OpCodes.Ldstr)
						{
							string str;

							if (instr.Operand is UTF8String operand)
								str = operand;
							else if (instr.Operand is string strOperand)
								str = strOperand;
							else continue;

							if (str is null)
								continue;

							if (str.Contains(OSU_BASE_URL))
							{
								editor.Replace(i, AsukiPatch_UniversalizeOsuURL(exp, str, baseUrlField));
								// Console.WriteLine($"{meth.DeclaringType.Name}::{meth.Name}");
							}
						}
					}
				}

				var cctorEditor = new MethodExplorer(null, urlsType.FindMethod(".cctor")).Editor;

				cctorEditor.InsertAt(cctorEditor.Count - 1, new[]
				{
					Instruction.Create(OpCodes.Ldstr, "ripple.moe"),
					Instruction.Create(OpCodes.Stsfld, baseUrlField),
				});

				return new PatchResult(patch, PatchStatus.Success);
			})
			*/
			#endregion
		};

		private class IlStringBuilder
		{
			public List<Instruction> Instructions = new List<Instruction>();

			private byte _stackCounter;

			private static MemberRef _stringConcat;

			public IlStringBuilder(ModuleDef module)
			{
				var mod = _stringConcat?.Module;

				if (mod != null && mod == module)
					return;

				_stringConcat = module.CreateMethodRef(true, typeof(String), "Concat", typeof(string), typeof(string), typeof(string));
			}

			public void Add(Instruction ins)
			{
				Instructions.Add(ins);
				_stackCounter++;

				if (_stackCounter >= 2)
				{
					Instructions.Add(Instruction.Create(OpCodes.Call, _stringConcat));
					_stackCounter = 1;
				}
			}

			public void Add(string str) =>
				Add(Instruction.Create(OpCodes.Ldstr, str));
		}

		public void Load(ModuleDef originalObfOsuModule) { }

		#region Disabled
		/*
		private static IList<Instruction> AsukiPatch_CreateServersArrayInitializer(ModuleExplorer exp, IList<string> addrList)
		{
			var ret = new List<Instruction>();

			ret.AddRange(new[]
			{
				Instruction.CreateLdcI4(addrList.Count),
				Instruction.Create(OpCodes.Newarr, exp.Module.CorLibTypes.String)
			});

			for (int i = 0; i < addrList.Count; i++)
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

		private static IList<Instruction> AsukiPatch_UniversalizeOsuURL(ModuleExplorer exp, string str, FieldDef baseUrlField, string baseUrl = OSU_BASE_URL)
		{
			var parts = str.Split(new[] { baseUrl }, StringSplitOptions.None);

			if (parts.Length == 2)
			{
				var sb = new IlStringBuilder(exp.Module);

				if (!String.IsNullOrEmpty(parts[0]))
					sb.Add(parts[0]);

				sb.Add(Instruction.Create(OpCodes.Ldsfld, baseUrlField));

				if (!String.IsNullOrEmpty(parts[1]))
					sb.Add(parts[1]);

				return sb.Instructions;
			}

			return new List<Instruction> { Instruction.Create(OpCodes.Ldstr, str) };
		}
		 */
		#endregion
	}
}