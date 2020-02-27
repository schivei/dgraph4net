using System.Reflection;
using DGraph4Net.Annotations;

namespace DGraph4Net.Identity
{
    internal sealed class IdentityTypeNameOptions<TUser, TRole, TUserToken, TUserClaim, TUserLogin, TRoleClaim>
    where TUser : DUser
    where TRole : DRole
    where TUserClaim : DUserClaim
    where TUserLogin : DUserLogin
    where TUserToken : DUserToken
    where TRoleClaim : DRoleClaim
    {
        public string UserTypeName => typeof(TUser).GetCustomAttribute<DGraphTypeAttribute>().Name;

        public string RoleTypeName => typeof(TRole).GetCustomAttribute<DGraphTypeAttribute>().Name;

        public string UserTokenTypeName => typeof(TUserToken).GetCustomAttribute<DGraphTypeAttribute>().Name;

        public string UserClaimTypeName => typeof(TUserClaim).GetCustomAttribute<DGraphTypeAttribute>().Name;

        public string RoleClaimTypeName => typeof(TRoleClaim).GetCustomAttribute<DGraphTypeAttribute>().Name;

        public string UserLoginTypeName => typeof(TUserLogin).GetCustomAttribute<DGraphTypeAttribute>().Name;
    }
}
