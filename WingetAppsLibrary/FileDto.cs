using System.Diagnostics.CodeAnalysis;

namespace WingetAppsLibrary
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
    public class FileDto
    {
        public string path { get; init; }
        public string sha { get; init; }
        public string type { get; init; }
    }
}