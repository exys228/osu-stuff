using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using osu_patch;
using osu_patch.Custom;
using osu_patch.Misc;

namespace OsuPatchPlugin.NoFlags
{
    public class NoFlags : IOsuPatchPlugin
    {
        public IEnumerable<Patch> GetPatches() => new[]
        {
           new Patch("No flags", true, (patch, exp) =>
           {
               // Startup flags
               exp["osu.Helpers.Scrobbler"]["sendCurrentTrack"].Editor.LocateAndNop(new[] {
                   OpCodes.Ldsfld,
                   OpCodes.Ldc_I4_0,
                   OpCodes.Bgt_S
               });
			   var Editor = exp["osu.GameplayElements.Scoring.Score"]["get_onlineFormatted"].Editor;
               // Submit flags
               Editor.LocateAndNop(new [] {
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
        };
        public void Load(ModuleDef originalObfOsuModule) { }
    }
}
