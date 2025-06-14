﻿using Spiderly.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class TableFilterContext
    {
        public object Value { get; set; }
        public MatchModeCodes MatchMode { get; set; }
        public string Operator { get; set; }
    }
}
