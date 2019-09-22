# osu!stuff
A collection of my projects dedicated to osu! patching.

### NameMapper
Library used to deobfuscate names in obfuscated assembly while having a deobfuscated one already.

This may be useful if:
* You have EazFuscator key to deobfuscate assembly but key only works on oldoldold assemblies (osu! key was changed after `target=3097`)
* You have deobfuscated-by-hand assembly (very unlikely)

Both assemblies have to be opcode-identical (mostly), you may want to deobfuscate the control flow of method's bodies using de4dot (**osu!patch** already does this with *obfuscated* module).

Typical usage:
```csharp
var mapper = new NameMapper(cleanModule, obfuscatedModule, Console.Out);
mapper.BeginProcessing();

// obfuscatedModule is passed as reference, it is deobfuscated now.
```

Note: _semi-hard-coded to work only with osu!, you may change `FindEntryPoint` method implementation and maybe it will work somehow with non-osu! assembly idk._

### NameMapper.CLI
A simple CLI interface for NameMapper.

Typical usage:
```
C:\osu!stuff> NameMapper.CLI clean.exe obf.exe
```

### osu!patch
TODO
