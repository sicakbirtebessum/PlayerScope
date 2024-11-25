using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Query
{
    public class RetainerQueryObject
    {
        public string? Name { get; set; } = null;
        public int Cursor { get; set; } = 0;
        public bool IsFetching { get; set; }
        public List<short> F_WorldIds { get; set; } = new List<short>();
        public bool? F_MatchAnyPartOfName { get; set; } = false;
    }
}
