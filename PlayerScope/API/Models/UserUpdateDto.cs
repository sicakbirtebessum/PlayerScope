using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PlayerScope.API.Models.User;

namespace PlayerScope.API.Models
{
    public class UserUpdateDto
    {
        public List<UserCharacterDto?> Characters { get; set; }
    }
}
