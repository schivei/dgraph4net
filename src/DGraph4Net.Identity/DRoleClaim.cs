using System;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{

    public class DRoleClaim : IdentityRoleClaim<Uid>, IEntity
    {
        public new Uid Id { get; set; }
    }
}
