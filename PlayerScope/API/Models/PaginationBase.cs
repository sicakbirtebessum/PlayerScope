using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class PaginationBase<T>
    {
        [JsonProperty("L")]
        public int LastCursor { get; set; }

        [JsonProperty("N")]
        public int NextCount { get; set; } 

        [JsonProperty("D")]
        public List<T> Data { get; set; }

        public PaginationBase()
        {
            Data = new List<T>();
        }

        public PaginationBase(int lastCursor, int nextCount, List<T> data)
        {
            LastCursor = lastCursor;
            NextCount = nextCount;
            Data = data ?? new List<T>();
        }
    }
}
