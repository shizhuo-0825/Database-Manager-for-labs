using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DXApplication2.Common.Models
{
    public class DataGroup
    {
        [Key]
        public int Id { get; set; }
        public string Material { get; set; } = string.Empty;
        public DateTime ExperimentDate { get; set; }
        public string CsvPath { get; set; } = string.Empty;
        public string CsvHash { get; set; } = string.Empty; // validate if csv file is modified
        public string Notes { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public ICollection<DataRecord> Term { get; set; } = new List<DataRecord>();
        public ICollection<GroupExperimentType> GroupExperimentTypes { get; set; } = new List<GroupExperimentType>();
        public ICollection<CollectionMember> CollectionMember { get; set; } = new List<CollectionMember>();
        public string Extras { get; set; } = "{}";
    }
}
