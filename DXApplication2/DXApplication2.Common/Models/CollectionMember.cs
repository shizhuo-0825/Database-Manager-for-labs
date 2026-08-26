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
    public class CollectionMember
    {
        [Key]
        public int Id { get; set; }
        public int DataGroupId { get; set; }
        public DataGroup DataGroup { get; set; } = null!;
        public int CollectionId { get; set; }
        public Collection Collection { get; set; } = null!;
    }
}
