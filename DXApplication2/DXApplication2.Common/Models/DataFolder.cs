using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DXApplication2.Common.Models
{
    public class DataFolder
    {
        [Key]
        public int Id { get; set; }
        public string FolderPath { get; set; } = string.Empty;  // 完整路径
        public DateTime ImportedAt { get; set; }
    }
}