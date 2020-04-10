using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sap_profile_ms.Model;
using sap_profile_ms.Model.Identity;

namespace sap_profile_ms
{
    public class Context : IdentityDbContext
    {
        public DbSet<PasswordReminder> PasswordReminder { get; set; }
        public DbSet<ApplicationUser> AspNetUsers { get; set; }



        public Context(DbContextOptions<Context> options)
            : base(options)
        {
        }

    }
}
