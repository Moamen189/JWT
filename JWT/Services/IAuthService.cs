using JWT.Models;
using System.Threading.Tasks;

namespace JWT.Services
{
    public interface IAuthService
    {
        Task<AuthModel> RegisterAsync(RegisterModel model);

        Task<AuthModel> GetTokenAsync(Login model);

        Task<string> AddRoleAsync(RoleModel model);
    }
}
