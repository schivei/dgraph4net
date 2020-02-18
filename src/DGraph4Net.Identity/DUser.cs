using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{
    public class DUser : IdentityUser<Uid>, IEntity
    {
        public ICollection<DUserClaim> Claims { get; set; }

        public ICollection<DRole> Roles { get; set; }

        public ICollection<DUserLogin> Logins { get; set; }

        public ICollection<DUserToken> Tokens { get; set; }
    }
}
