﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Security.Entities
{
    public class UserRole
    {
        public int RoleId { get; set; }
        public long UserId { get; set; }
    }
}
