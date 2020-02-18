using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DGraph4Net.Services;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;

namespace DGraph4Net.Identity
{
    public class DUserStore : IUserAuthenticationTokenStore<DUser>,
        IUserAuthenticatorKeyStore<DUser>,
        IUserClaimStore<DUser>,
        IUserEmailStore<DUser>,
        IUserLockoutStore<DUser>,
        IUserLoginStore<DUser>,
        IUserPasswordStore<DUser>,
        IUserPhoneNumberStore<DUser>,
        IUserRoleStore<DUser>,
        IUserSecurityStampStore<DUser>,
        IUserTwoFactorRecoveryCodeStore<DUser>,
        IUserTwoFactorStore<DUser>,
        IProtectedUserStore<DUser>
    {
        private readonly DGraph _graph;
        private readonly IdentityErrorDescriber _describer;

        public DUserStore(DGraph graph, IdentityErrorDescriber describer) : this (describer) => _graph = graph;

        private DUserStore(IdentityErrorDescriber describer) => _describer = describer;

        private async Task<Response> AddClaimAsync(DUser user, Claim claim, CancellationToken cancellationToken)
        {
            await using var txn = _graph.NewTransaction(cancellationToken: cancellationToken);

            const string q = @"
            query Q($user: string, $claimType: string, $claimValue) {
                u as var(func: uid($user))
                c as var(func: eq(claim.value, $claimValue)) @filter(eq(claim.type, $claimType))
            }";

            var mu1 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(@"
                    uid(c) <claim.type> $claimType .
                    uid(c) <claim.value> $claimValue .
                    uid(c) <dgraph.type> Claim .
                "),
                CommitNow = true,
                Cond = "@if(eq(len(u), 1) AND eq(len(c), 0))"
            };

            var mu2 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("uid(u) <claims> uid(c) ."),
                CommitNow = false,
                Cond = "@if(eq(len(u), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.AddRange(new[] { mu1, mu2 });
            req.Vars.Add("$user", user.Id);
            req.Vars.Add("$claimType", claim.Type);
            req.Vars.Add("$claimValue", claim.Value);

            return await txn.Do(req);
        }

        public Task AddClaimsAsync(DUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken) =>
            Task.WhenAll(claims.AsParallel().Select(claim => AddClaimAsync(user, claim, cancellationToken)));

        public async Task AddLoginAsync(DUser user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            await using var txn = _graph.NewTransaction(cancellationToken: cancellationToken);

            const string q = @"
            query Q($user: string, $providerKey: string, $providerName: string, $loginProvider) {
                u as var(func: uid($user))
                p as var(func: eq(provider.key, $providerKey)) @filter(eq(provider.name, $providerName) AND eq())
            }";

            var mu1 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8(@"
                    uid(p) <provider.name> $providerName .
                    uid(p) <provider.key> $providerKey .
                    uid(p) <dgraph.type> Claim .
                    uid(p) <dgraph.type> Claim .
                "),
                CommitNow = true,
                Cond = "@if(eq(len(u), 1) AND eq(len(p), 0))"
            };

            var mu2 = new Mutation
            {
                SetNquads = ByteString.CopyFromUtf8("uid(u) <claims> uid(c) ."),
                CommitNow = false,
                Cond = "@if(eq(len(u), 1))"
            };

            var req = new Request { Query = q, CommitNow = true };
            req.Mutations.AddRange(new[] { mu1, mu2 });
            req.Vars.Add("$user", user.Id);
            req.Vars.Add("$claimType", claim.Type);
            req.Vars.Add("$claimValue", claim.Value);

            return await txn.Do(req);
        }

        public Task AddToRoleAsync(DUser user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<int> CountCodesAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityResult> CreateAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IdentityResult> DeleteAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public Task<DUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<DUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<DUser> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<int> GetAccessFailedCountAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetAuthenticatorKeyAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<Claim>> GetClaimsAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<string> GetEmailAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> GetEmailConfirmedAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> GetLockoutEnabledAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetNormalizedEmailAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<string> GetNormalizedUserNameAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<string> GetPasswordHashAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetPhoneNumberAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> GetPhoneNumberConfirmedAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetRolesAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetSecurityStampAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetTokenAsync(DUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> GetTwoFactorEnabledAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetUserIdAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<string> GetUserNameAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<DUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<DUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> HasPasswordAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<int> IncrementAccessFailedCountAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsInRoleAsync(DUser user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RedeemCodeAsync(DUser user, string code, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveClaimsAsync(DUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task RemoveFromRoleAsync(DUser user, string roleName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveLoginAsync(DUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveTokenAsync(DUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task ReplaceClaimAsync(DUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task ReplaceCodesAsync(DUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task ResetAccessFailedCountAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetAuthenticatorKeyAsync(DUser user, string key, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetEmailAsync(DUser user, string email, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetEmailConfirmedAsync(DUser user, bool confirmed, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetLockoutEnabledAsync(DUser user, bool enabled, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetLockoutEndDateAsync(DUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetNormalizedEmailAsync(DUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetNormalizedUserNameAsync(DUser user, string normalizedName, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetPasswordHashAsync(DUser user, string passwordHash, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetPhoneNumberAsync(DUser user, string phoneNumber, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetPhoneNumberConfirmedAsync(DUser user, bool confirmed, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetSecurityStampAsync(DUser user, string stamp, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetTokenAsync(DUser user, string loginProvider, string name, string value, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task SetTwoFactorEnabledAsync(DUser user, bool enabled, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SetUserNameAsync(DUser user, string userName, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Task<IdentityResult> UpdateAsync(DUser user, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
