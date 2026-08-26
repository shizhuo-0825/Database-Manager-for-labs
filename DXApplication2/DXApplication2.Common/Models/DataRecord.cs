using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace DXApplication2.Common.Models
{
    public class DataRecord
    {
        [Key]
        public int Id { get; set; }
        public int DataGroupId { get; set; }
        public string RecordName { get; set; }
        public DataGroup DataGroup { get; set; } = null!;
        public string RowIndex { get; set; }
        public string ImageFilePath { get; set; } = string.Empty;
        public double? ResultQuantity { get; set; }
        public string? ResultPath { get; set; }
        public bool IsHidden { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public string ExptParams { get; set; } = "{}";
        public ICollection<AnalysisParams> Analysis { get; set; } = new List<AnalysisParams>();
    }
}
