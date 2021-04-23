using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WingetAppsLibrary
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
    public class TreeDto
    {
        public IEnumerable<FileDto> tree { get; init; }
    }
}