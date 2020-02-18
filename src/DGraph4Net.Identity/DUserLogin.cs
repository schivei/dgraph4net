using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{

    public class DUserLogin : IdentityUserLogin<Uid>, IEntity
    {
        public Uid Id { get; set; }
    }
}
