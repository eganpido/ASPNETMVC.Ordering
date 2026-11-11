using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ASPNETMVC.Ordering.Models
{
    public class TableStatusModel
    {
        public int Id { get; set; }
        public string TableCode { get; set; }
        public int TableGroupId { get; set; }
        public bool IsOccupied { get; set; }
        public int SalesId { get; set; }
    }
}