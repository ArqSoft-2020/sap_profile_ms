using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace sap_profile_ms.Model.Identity
{
    [Serializable]
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Country { get; set; }

        [Required]
        public string Picture { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string LastName { get; set; }


        [Required]
        public bool Verified { get; set; }

        public int WonGames { get; set; }
        public int LostGames { get; set; }
        public int TotalGames { get; set; }
    }
}
