using osu_patch.Custom;

namespace osu_patch.Misc
{
    public class PluginInfo
    {
        public string AssemblyName { get; }

        public string TypeName { get; }

        public IOsuPatchPlugin Type { get; }

        public PluginInfo(string assemblyName, string typeName, IOsuPatchPlugin type)
        {
            AssemblyName = assemblyName;
            TypeName = typeName;
            Type = type;
        }
    }
}