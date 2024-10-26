using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class UserLoginModel
    {
        public int GameAccountId { get; set; }
        public string Password { get; set; } = null!;
    }
}
