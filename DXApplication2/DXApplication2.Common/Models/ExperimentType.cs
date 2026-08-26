using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DXApplication2.Common.Models
{
    public class ExperimentType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public string? Category { get; set; }

        public string? DefaultPlotTemplate { get; set; }

        public ICollection<GroupExperimentType> GroupExperimentType { get; set; } = new List<GroupExperimentType>();
    }
}