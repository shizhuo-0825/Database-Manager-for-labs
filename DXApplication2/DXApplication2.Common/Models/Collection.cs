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
    public class Collection
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DefaultPlotTemplate { get; set; }
        public ICollection<CollectionMember> CollectionMember { get; set; } = new List<CollectionMember>();
    }
}
