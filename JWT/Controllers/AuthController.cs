using JWT.Models;
using JWT.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace JWT.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService authService;

        public AuthController(IAuthService authService)
        {
            this.authService = authService;
        }

        [HttpPost("register")]

        public async Task<IActionResult> ResultAsync([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await authService.RegisterAsync(model);

            if(!result.IsAuthenticated)
            {
                return BadRequest(result.Message);
            }

            return Ok(result);
        }



        [HttpPost("token")]

        public async Task<IActionResult> Login([FromBody] Login model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await authService.GetTokenAsync(model);

            if (!result.IsAuthenticated)
            {
                return BadRequest(result.Message);
            }

            return Ok(result);
        }
    }
}
