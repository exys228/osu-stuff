using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch;
using osu_patch.Plugins;
using System;
using System.Collections.Generic;

namespace OsuPatchPlugin.SilentOsuDirect
{
	public class SilentOsuDirectPlugin : IOsuPatchPlugin
	{
		public IEnumerable<Patch> GetPatches() => new Patch[]
		{
			new Patch("UpdateStatus patch", (patch, exp) =>
			{
				// Remove "OsuDirect" status on Bancho
				var updateStatus = exp["osu.Online.BanchoClient"]["UpdateStatus"].Editor;
				var loc = updateStatus.Locate(new[]
				{
					OpCodes.Ldc_I4_5,
					OpCodes.Starg_S,
					OpCodes.Br_S,
					OpCodes.Ldsfld,
					OpCodes.Stloc_0,
					OpCodes.Ldsfld,
					OpCodes.Stloc_1,
					OpCodes.Br_S,
					OpCodes.Ldc_I4_S,
					OpCodes.Starg_S,
					OpCodes.Br_S,
				});
				updateStatus.ReplaceAt(loc + 8, Instruction.Create(OpCodes.Ldc_I4_0));

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Download server patch", (patch, exp) =>
			{
				// Patch download server to storage.ainu.pw (that's me)
				var pWebReq = exp["osu_common.Helpers.pWebRequest"].FindMethodRaw(".ctor").Editor;
				var pWebLoc = pWebReq.Locate(new[]
				{
					OpCodes.Ldarg_0,
					OpCodes.Call,
				});
				// The code below got help from xxCherry!
				// Thank you so much for helping me! :D
				var contains = exp.Module.CreateMethodRef(false, typeof(String), "Contains", typeof(bool), typeof(string));
				var replace = exp.Module.CreateMethodRef(false, typeof(String), "Replace", typeof(string),  typeof(string), typeof(string));
				var concat = exp.Module.CreateMethodRef(true, typeof(String), "Concat", typeof(string),  typeof(string), typeof(string));
				var parameter = pWebReq.Parent.Method.Parameters[1];
				var instructions = new[] {
					Instruction.Create(OpCodes.Ldarg_1),
					Instruction.Create(OpCodes.Ldstr, "ppy.sh/d/"),
					Instruction.Create(OpCodes.Callvirt, contains),
					Instruction.Create(OpCodes.Nop),
					Instruction.Create(OpCodes.Ldstr, ""),
					Instruction.Create(OpCodes.Ldarg_1),
					Instruction.Create(OpCodes.Ldstr, "osu.ppy.sh/d/"),
					Instruction.Create(OpCodes.Ldstr, "storage.ainu.pw/d/"),
					Instruction.Create(OpCodes.Callvirt, replace),
					Instruction.Create(OpCodes.Call, concat),
					Instruction.Create(OpCodes.Starg_S, parameter),
					Instruction.Create(OpCodes.Nop),
					Instruction.Create(OpCodes.Nop),
					Instruction.Create(OpCodes.Nop),
				};

				instructions[3] = Instruction.Create(OpCodes.Brfalse, instructions[11]);
				pWebReq.InsertAt(pWebLoc + 2, instructions);

				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Enable osu!direct", (patch, exp) =>
			{
				// Remove supporter check
				var removeSupporterCheck = exp["osu.GameModes.Menus.Menu"]["checkPermissions"].Editor;
				var permsCheckLoc = removeSupporterCheck.Locate(new[]
				{
					OpCodes.Callvirt,
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Ldc_I4_0,
					OpCodes.Ble,
					OpCodes.Ldarg_0,
				});
				removeSupporterCheck.NopAt(permsCheckLoc + 1, 6);
				// Allow to click osu!direct button
				// literally first 6 instructions
				exp["osu.GameModes.Menus.Menu"]["startOsuDirect"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Brtrue_S,
					OpCodes.Ret
				});
				return new PatchResult(patch, PatchStatus.Success);
			}),
		};

		public void Load(ModuleDef originalObfOsuModule) { }
	}
}
