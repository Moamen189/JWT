using JWT.Models;
using System.Threading.Tasks;

namespace JWT.Services
{
    public interface IAuthService
    {
        Task<AuthModel> RegisterAsync(RegisterModel model);
    }
}
