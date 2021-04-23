using System;
using System.Collections.Generic;

namespace WingetAppsLibrary
{
    public class AppListDto
    {
        public DateTime CreatedAt { get; init; }
        public List<AppDto> Apps { get; init; }
    }
}