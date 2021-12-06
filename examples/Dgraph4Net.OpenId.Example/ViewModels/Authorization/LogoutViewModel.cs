
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Dgraph4Net.OpenIddict.ViewModels.Authorization
{
    public class LogoutViewModel
    {
        [BindNever]
        public string RequestId { get; set; } = default!;
    }
}
