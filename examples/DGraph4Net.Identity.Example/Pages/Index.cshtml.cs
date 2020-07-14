using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Dgraph4Net.Identity.Example.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public async Task OnGetAsync([FromServices] UserManager<DUser> manager)
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = await manager.FindByNameAsync(User.Identity.Name);
                await manager.SetPhoneNumberAsync(user, "+5511970648333");
                await manager.UpdateAsync(user);
                user.PhoneNumber = null;
                await manager.UpdateAsync(user);
            }
        }
    }
}
