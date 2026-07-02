using System.IO;
using System.Reflection;

namespace MultiplayerChat.Core.Addons;

// Mono cannot unload assemblies; load from bytes so updated DLLs can be picked up on menu reload.
internal static class AddonLoadContext
{
    internal static Assembly LoadFromFile(string dllPath)
    {
        var bytes = File.ReadAllBytes(dllPath);
        return Assembly.Load(bytes);
    }
}
