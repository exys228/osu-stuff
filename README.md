# osu!stuff
A collection of my projects dedicated to osu! patching.

### NameMapper
Library used to deobfuscate names in obfuscated assembly while having a deobfuscated one already.

This may be useful if:
* You have EazFuscator key to deobfuscate assembly but the key only works on oldoldold assemblies (osu! key was changed after `target=3097`)
* You have deobfuscated-by-hand assembly (very unlikely)

Both assemblies have to be opcode-identical (mostly), and you **WANT** to deobfuscate the control flow of method's bodies using de4dot (**osu!patch** already does this with *obfuscated* module), and after `391aca47` commit you also want to decrypt strings using de4dot/holly's string fixer/**StringFixerMini.CLI** (and again, **osu!patch** already does this with *obfuscated* module).

**TL;DR**
Clean osu! assembly recipe:
1. Download latest osu! assembly which names are encoded with `recorderinthesandybridge` key (hint: [`target=3097`](https://osu.ppy.sh/web/check-updates.php?action=path&stream=Stable&target=3097)).
2. Deobfuscate control flow using `de4dot --only-cflow-deob osu!.exe`.
3. Decrypt strings using either de4dot, holly's string fixer or **StringFixerMini.CLI**.
4. Decrypt names using TODOTODOTODO

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
TODO
