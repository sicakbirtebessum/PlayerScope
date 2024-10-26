using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PlayerScope.API.Models.PlayerDetailed;

namespace PlayerScope.API.Models
{
    public class RetainerWithPaginationDto
    {
        [JsonProperty("C")]
        public int Cursor { get; init; }
        [JsonProperty("N")]
        public int NextCount { get; init; }
        [JsonProperty("D")]
        public List<RetainerDto> Data { get; init; }

        public RetainerWithPaginationDto(int cursor, int nextCount, List<RetainerDto> data)
        {
            Cursor = cursor;
            NextCount = nextCount;
            Data = data;
        }
    }
}
