using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompiLiasse_Sharp.Models
{
    public sealed class SettingsProfile
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string? ExcelPath { get; set; }
        public string? FolderPath { get; set; }
        public bool IsActive { get; set; } = true;

        // Dummy-only pour l'étape 1 (on mettra en DB plus tard)
        public bool IsDraft { get; set; } = false;
    }
}
