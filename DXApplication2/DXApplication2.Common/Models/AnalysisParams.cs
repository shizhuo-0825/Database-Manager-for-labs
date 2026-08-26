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
    public class AnalysisParams
    {
        [Key]
        public int Id { get; set; }
        public string HashParamsPath { get; set; } = string.Empty;
        public int DataRecordId { get; set; }
        public DataRecord DataRecord { get; set; } = null!;
    }
}
