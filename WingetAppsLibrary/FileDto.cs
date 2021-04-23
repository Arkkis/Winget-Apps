using System.Diagnostics.CodeAnalysis;

namespace WingetAppsLibrary
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
    public abstract class FileDto
    {
        public string path { get; }
        public string sha { get; }
        public string type { get; }
    }
}