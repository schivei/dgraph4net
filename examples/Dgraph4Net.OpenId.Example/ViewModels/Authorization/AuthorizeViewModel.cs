using System.ComponentModel.DataAnnotations;

namespace Dgraph4Net.OpenIddict.ViewModels.Authorization
{
    public class AuthorizeViewModel
    {
        [Display(Name = "Application")]
        public string ApplicationName { get; set; } = default!;

        [Display(Name = "Scope")]
        public string Scope { get; set; } = default!;
    }
}
