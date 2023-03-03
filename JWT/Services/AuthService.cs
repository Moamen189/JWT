using JWT.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace JWT.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> userManager;

        public AuthService(UserManager<ApplicationUser> userManager)
        {
            this.userManager = userManager;
        }
        public async Task<AuthModel> RegisterAsync(RegisterModel model)
        {
            if(userManager.FindByEmailAsync(model.Email) is not null) {
                return new AuthModel { Message = " Email already Registtred"};
            }

            if (userManager.FindByEmailAsync(model.Username) is not null)
            {
                return new AuthModel { Message = "UserName is already Existed" };
            }

            var user = new ApplicationUser {
                UserName = model.Username,
                Email = model.Email,
                FirstName= model.FirstName,
                LastName= model.LastName,
            };

            var result = await userManager.CreateAsync(user , model.Password);

            if(!result.Succeeded)
            {
                var errors = string.Empty;

                foreach(var error in result.Errors)
                {
                    errors = $"{error.Description} , ";
                }

                return new AuthModel { Message = errors};

            }

            await userManager.AddToRoleAsync(user, "User");
        }
    }
}
