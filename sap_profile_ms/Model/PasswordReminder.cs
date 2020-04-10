using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace sap_profile_ms.Model
{
    public class PasswordReminder
    {
        [Key]
        public int Id { get; set; }
        public Guid Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public Guid IdUser { get; set; }
    }
}
