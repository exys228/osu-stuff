# osu!stuff
A collection of my projects dedicated to osu! patching.<br><sub><em>Originally brought to you by @exys228. Maintained by @xxCherry from time to time.</em></sub>

### NameMapper
Library used to deobfuscate names in obfuscated assembly while having a deobfuscated one already.

This may be useful if:
* You have EazFuscator key to deobfuscate assembly but the key only works on oldoldold assemblies (osu! key was changed after `target=3097`)
* You have deobfuscated-by-hand assembly (very unlikely)

Both assemblies have to be opcode-identical (mostly), and you **WANT** to deobfuscate the control flow of method's bodies in clean assembly using de4dot (**osu!patch** already does this with *obfuscated* module), and after `391aca47` commit you also want to decrypt strings using de4dot/holly's string fixer/**StringFixerLib.CLI** (and again, **osu!patch** already does this with *obfuscated* module). Then you'll need to decrypt assembly names using any of two ways above.

**TL;DR**
Clean osu! assembly recipe:
1. Download latest osu! assembly which names are encoded with `recorderinthesandybridge` key (hint: [`target=3097`](https://osu.ppy.sh/web/check-updates.php?action=path&stream=Stable&target=3097)).
2. Deobfuscate control flow using [`de4dot`](https://github.com/0xd4d/de4dot)`--only-cflow-deob osu!.exe`. <!-- SHITCODING EVEN IN README EXCUSE ME WHAT THE FUCK -->
3. Decrypt strings using either [de4dot](https://github.com/0xd4d/de4dot), [HoLLy-HaCKeR](https://github.com/HoLLy-HaCKeR)'s [EazFixer](https://github.com/HoLLy-HaCKeR/EazFixer) or **StringFixerLib.CLI**.
4. Decrypt names using HoLLy-HaCKeR's [osu-decoder](https://github.com/HoLLy-HaCKeR/osu-decoder). I used my own program, `clean.exe` should be somewhere in this repo tho.
5. Done!

Typical usage:
```csharp
var mapper = new NameMapper(cleanModule, obfuscatedModule, Console.Out);
mapper.BeginProcessing();

// obfuscatedModule is passed as reference, it is deobfuscated by now.
```

_Note: semi-hard-coded to work only with osu!, you may change_ `FindEntryPoint` _method implementation and maybe it will work somehow with non-osu! assembly idk._

### NameMapper.CLI
A simple CLI interface for NameMapper.

Typical usage:
```
C:\osu!stuff> NameMapper.CLI clean.exe obf.exe
```

### osu!patch
osu! assembly static-patching framework or something similar.

It cleans up enough of the obfuscation/string mess for patching, maps names when possible, and gives patches small explorer wrappers for classes, methods and fields. It then runs patches and writes the result back with the `-osupatch` tag.

Reference this directly from your own patcher project if you want full control. If you just want a simple tool to play around, use **osu!patch.CLI**.

### osu!patch.CLI
Command-line wrapper around **osu!patch**. It loads plugin DLLs from `plugins\`, reads `config.cfg`, runs enabled patches and writes the patched exe.

Typical usage:
```
C:\osu!stuff> osu!patch.CLI osu!.exe
```

First run creates `config.cfg` near `osu!patch.CLI.exe`. Patch names are used as config keys, value is `Enabled`/anything else. 
Plugins are loaded from `plugins\` folder near `osu!patch.CLI.exe`. 


### Plugins
A plugin is just a .NET class library with at least one `IOsuPatchPlugin` implementation:
```csharp
public class MyPlugin : IOsuPatchPlugin
{
    public void Load(ModuleDef originalObfOsuModule) { }

    public IEnumerable<Patch> GetPatches() => new[]
    {
        new Patch("My patch", (patcher, patch, exp) =>
        {
            exp["osu.GameBase"]["Initialize"].Editor.InsertAt(0, Instruction.Create(OpCodes.Ret));
            return patch.Result(PatchStatus.Success);
        })
    };
}
```

More advanced example:
```csharp
public class DebugNotifyPlugin : IOsuPatchPlugin
{
    public void Load(ModuleDef originalObfOsuModule) { }

    public IEnumerable<Patch> GetPatches() => new[]
    {
        new Patch("Debug notifications", (patcher, patch, exp) =>
        {
            var player = exp["osu.GameModes.Play.Player"];

            var show = player.InsertMethod(
                MethodAttributes.Public | MethodAttributes.Static,
                (Action<string>)DebugNotifier.Show);

            player["Initialize"].Editor.InsertAt(0, new[]
            {
                Instruction.Create(OpCodes.Ldstr, "Player.Initialize"),
                Instruction.Create(OpCodes.Call, show.Method)
            });

            player["HandleScoreSubmission"].Editor.InsertAt(
                0,
                (Action)(() => DebugNotifier.Delayed(() => "score submit")));

            return patch.Result(PatchStatus.Success);
        })
    };

    private static class DebugNotifier
    {
        private static int count;

        public static void Show(string source)
        {
            NotificationManager.ShowMessage($"{source}: {++count}");
        }

        public static void Delayed(Func<string> message)
        {
            GameBase.Scheduler.AddDelayed(() => NotificationManager.ShowMessage(message()), 100, false);
        }
    }
}
```

That means patch code can be normal C# most of the time: methods, helper classes, fields, `Action<>`, `Func<>`, lambdas/delegates. Hook types still need to come from the hook DLL generated for the osu! build you are patching.

Example plugin projects (these might be outdated asf):
* `OsuPatchPlugin.Misc` - random gameplay/ui patches.
* `OsuPatchPlugin.SilentOsuDirect` - free osu!direct.

### HookGenerator
Generates API-like hook assemblies from a clean osu! assembly (although not limited to osu!). This is what lets plugin code reference osu! types/methods/fields, etc.

Typical usage:
```
C:\osu!stuff> HookGenerator.CLI clean.exe
```

Output is written near input assembly:
```
OsuHooks-<first 8 chars of input md5>.dll
```

Hook assemblies contain the useful shape of osu! including:
* types
* nested types
* fields
* methods
* generic parameters
* interfaces and more.

Method bodies are dummies. They exist for compile-time references, `osu!patch` resolves them back into the real target module while patching.

### StringFixer
Small wrapper around the string decryptor code used by `osu!patch`.

Typical usage:
```
C:\osu!stuff> StringFixer.CLI obf.exe
```

Writes:
```
obf-string.exe
```

### OsuVersionDownloader
Downloads osu! versions from osu.ppy.sh update endpoints.

Typical usage:
```
C:\osu!stuff> OsuVersionDownloader Stable40 osu!.exe
C:\osu!stuff> OsuVersionDownloader Stable 3097 clean.exe
```

Streams:
* `Stable` - fallback
* `Stable40` - current stable
* `Beta` - dead now
* `cuttingedge`

Existing output file gets backed up to `osu!bak\`.

### CacheTraceDecoder
Tiny WinForms helper for decoded cache traces. Give it a dictionary/cache file, paste obfuscated trace text, get readable trace text. 