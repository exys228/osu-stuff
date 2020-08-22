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
				var osuDirectServer = exp["osu.Online.OsuDirectDownload"].FindMethodRaw(".ctor", 
					MethodSig.CreateInstance(exp.CorLibTypes.Void,
						exp.CorLibTypes.Int32,
						exp.CorLibTypes.String,
						exp.CorLibTypes.String,
						exp.CorLibTypes.Boolean,
						exp.CorLibTypes.Int32)).Editor;
				var serverLoc = osuDirectServer.Locate(new[]
				{
					OpCodes.Ldarg_S,
					OpCodes.Brfalse_S,
					null,
					null,
					OpCodes.Br_S,
					null,
					null,
					OpCodes.Stloc_1,
				});
				osuDirectServer.NopAt(serverLoc + 2, 1);
				osuDirectServer.NopAt(serverLoc + 5, 1);
				osuDirectServer.ReplaceAt(serverLoc + 3, Instruction.Create(OpCodes.Ldstr, "https://storage.ainu.pw/d/{0}n"));
				osuDirectServer.ReplaceAt(serverLoc + 6, Instruction.Create(OpCodes.Ldstr, "https://storage.ainu.pw/d/{0}"));

				var downloadServerBackup = exp["osu.Online.OsuDirectDownload"]["DownloadFallback"].Editor;
				var downloadServerLoc = downloadServerBackup.Locate(new[]
				{
					null,
					null,
					OpCodes.Ldarg_0,
					OpCodes.Ldfld,
					OpCodes.Ldfld,
					OpCodes.Box,
					OpCodes.Call,
					OpCodes.Ldnull,
					OpCodes.Call,
					OpCodes.Ret,
				});
				downloadServerBackup.NopAt(serverLoc, 1);
				downloadServerBackup.ReplaceAt(downloadServerLoc + 1, Instruction.Create(OpCodes.Ldstr, "https://storage.ainu.pw/d/{0}"));

				return new PatchResult(patch, PatchStatus.Success);
			}),
			/*
			// This is old patching method
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
					Instruction.Create(OpCodes.Ldstr, "osu.ppy.sh/d/"),
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
			*/
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
				// Enable auto beatmap download when spectating
				exp["osu.GameModes.Play.Player"]["OnLoadStart"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Ldc_I4_0,
					OpCodes.Ble_S,
				});
				exp["osu.Online.StreamingManager"]["HandleSongChange"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Brtrue,
					OpCodes.Ldsfld,
					OpCodes.Ldfld,
					OpCodes.Ldc_I4_0,
					OpCodes.Ble,
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Ldc_I4_0,
					OpCodes.Ble_S,
				});
				// Download beatmap from link
				exp["osu.GameModes.Select.Drawable.OnlineBeatmap"]["FromId"].Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Brtrue_S,
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_S,
					OpCodes.And,
					OpCodes.Brfalse_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
					OpCodes.Ret,
				});
				/*
				exp["osu.Online.OsuDirect"].FindMethod("HandlePickup",
					MethodSig.CreateInstance(
						exp.CorLibTypes.String,
						exp.ImportAsTypeSig(typeof(EventHandler)),
						exp.ImportAsTypeSig(typeof(EventHandler))
						)
					).Editor.LocateAndNop(new[]
				{
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_4,
					OpCodes.And,
					OpCodes.Ldc_I4_0,
					OpCodes.Bgt_S,
					OpCodes.Ldsfld,
					OpCodes.Call,
					OpCodes.Ldc_I4_S,
					OpCodes.And,
					OpCodes.Ldc_I4_0,
					OpCodes.Ble_S,
					OpCodes.Ldsfld,
					OpCodes.Brtrue_S,
				});
				*/
				return new PatchResult(patch, PatchStatus.Success);
			}),
			new Patch("Search server patch", (patch, exp) =>
			{
				return new PatchResult(patch, PatchStatus.Success);
			}),
		};

		public void Load(ModuleDef originalObfOsuModule) { }
	}
}
