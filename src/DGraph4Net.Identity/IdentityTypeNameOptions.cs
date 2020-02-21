namespace DGraph4Net.Identity
{
    public static class IdentityTypeNameOptions
    {
        public static string UserTypeName { get; set; } = "AspNetUser";

        public static string RoleTypeName { get; set; } = "AspNetRole";

        public static string UserTokenTypeName { get; set; } = "AspNetUserToken";

        public static string UserClaimTypeName { get; set; } = "AspNetUserClaim";

        public static string RoleClaimTypeName { get; set; } = "AspNetRoleClaim";

        public static string UserLoginTypeName { get; set; } = "AspNetUserLogin";
    }
}
