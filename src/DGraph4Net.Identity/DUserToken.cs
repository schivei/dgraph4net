using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{

    public class DUserToken : IdentityUserToken<Uid>, IEntity
    {
        public Uid Id { get; set; }
    }
}
