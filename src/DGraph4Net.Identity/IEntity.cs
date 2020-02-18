using System;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{
    public interface IEntity
    {
        Uid Id { get; }
    }


}
