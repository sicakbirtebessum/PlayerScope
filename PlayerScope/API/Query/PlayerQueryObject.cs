﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Query
{
    public class PlayerQueryObject
    {
        public long? LocalContentId { get; set; } = null;
        public string? Name { get; set; } = null;
        public int Cursor { get; set; } = 0;
        public bool IsFetching { get; set; }
        public short? F_WorldId { get; set; } = null;
        public bool? F_MatchAnyPartOfName { get; set; } = false;
    }
}