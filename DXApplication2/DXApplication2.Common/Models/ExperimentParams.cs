using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DXApplication2.Common.Models
{
    public class ExperimentParams
    {
        [Key]
        public int Id { get; set; }

        // 必填字段(非可空)
        public string FieldName { get; set; } = string.Empty;

        // 可选字段(可空)
        public string? DisplayName { get; set; }
        public string? Unit { get; set; }
        public string? DataType { get; set; }
        
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }

        // 值类型自然有默认值,不涉及可空

        public bool IsDeprecated { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public int? ExperimentTypeId { get; set; }
        public ExperimentType? ExperimentType { get; set; }
    }
}
