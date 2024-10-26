using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlayerScope.API.Models
{
    public class UserRegister
    {
        public int GameAccountId { get; set; }
        public long UserLocalContentId { get; set; }
        public string Name { get; set; }
        public string ClientId { get; set; }
        public string Version { get; set; }
    }
}
