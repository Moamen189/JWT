using JWT.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Linq;
using JWT.Helpers;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace JWT.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly JWTT jwt;

        public AuthService(UserManager<ApplicationUser> userManager , RoleManager<IdentityRole> roleManager, IOptions<JWTT> jwt)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            jwt = jwt;
        }

        public async Task<string> AddRoleAsync(RoleModel model)
        {
            var user = await userManager.FindByIdAsync(model.UserId);

            if (user is null || !await roleManager.RoleExistsAsync(model.Role))
                return "Invalid user ID or Role";

            if (await userManager.IsInRoleAsync(user, model.Role))
                return "User already assigned to this role";

            var result = await userManager.AddToRoleAsync(user, model.Role);

            return result.Succeeded ? string.Empty : "Something went wrong";
        }

        public async  Task<AuthModel> GetTokenAsync(Login model)
        {
            var authModel = new AuthModel();

            var user = await userManager.FindByEmailAsync(model.Email);

            if (user is null || !await userManager.CheckPasswordAsync(user, model.Password))
            {
                authModel.Message = "Email or Password is incorrect!";
                return authModel;
            }

            var jwtSecurityToken = await CreateJwtToken(user);
            var rolesList = await userManager.GetRolesAsync(user);

            authModel.IsAuthenticated = true;
            authModel.Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
            authModel.Email = user.Email;
            authModel.Username = user.UserName;
            //authModel.ExpiresOn = jwtSecurityToken.ValidTo;
            authModel.Roles = rolesList.ToList();

            if(user.RefreshTokens.Any(t => t.IsActive))
            {
                var activeRefreshToken = user.RefreshTokens.FirstOrDefault(t => t.IsActive);
                authModel.RefreshToken = activeRefreshToken.Token;
                authModel.RefreshTokenDate = activeRefreshToken.ExpiresOn;
            }else
            {
                var RefreshToken = GenerateRefreshToken();
                authModel.RefreshToken = RefreshToken.Token;
                authModel.RefreshTokenDate = RefreshToken.ExpiresOn;

                user.RefreshTokens.Add(RefreshToken);

                await userManager.UpdateAsync(user);
            }

            return authModel;
        }

        public async Task<AuthModel> RefreshTokenAsync(string token)
        {
            var authModel = new AuthModel();

            var user = await userManager.Users.SingleOrDefaultAsync(t => t.RefreshTokens.Any(t => t.Token == token));
            if(user == null)
            {
                authModel.IsAuthenticated=false;
                authModel.Message = "Invalid Token";
                return authModel;
            }

            var refreshToken = user.RefreshTokens.Single(t => t.Token == token);

            if (!refreshToken.IsActive)
            {
                authModel.IsAuthenticated = false;
                authModel.Message = "Invalid Token";
                return authModel;
            }

            refreshToken.RevokedOn = DateTime.UtcNow;

            var newRefreshToken = GenerateRefreshToken();
            user.RefreshTokens.Add(newRefreshToken);
            await userManager.UpdateAsync(user);

            var jwtToken = await CreateJwtToken(user);
            authModel.IsAuthenticated = true;
            authModel.Token = new JwtSecurityTokenHandler().WriteToken(jwtToken);
            authModel.Email = user.Email;
            authModel.Username = user.UserName;
            var roles = await userManager.GetRolesAsync(user);
            authModel.Roles = roles.ToList();
            authModel.RefreshToken = newRefreshToken.Token;
            authModel.RefreshTokenDate = newRefreshToken.ExpiresOn;

            return authModel;
        }

        public async Task<AuthModel> RegisterAsync(RegisterModel model)
        {
            if(userManager.FindByEmailAsync(model.Email) is not null) {
                return new AuthModel { Message = " Email already Registtred"};
            }

            if (userManager.FindByNameAsync(model.Username) is not null)
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

            var jwtSecurityToken = await CreateJwtToken(user);

            return new AuthModel {
                Email = user.Email,
                //ExpiresOn = jwtSecurityToken.ValidTo,
                IsAuthenticated = true,
                Roles = new List<string> { "User" },
                Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken),
                Username = user.UserName

            };
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            var user = await userManager.Users.SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

            if (user == null)
                return false;

            var refreshToken = user.RefreshTokens.Single(t => t.Token == token);

            if (!refreshToken.IsActive)
                return false;

            refreshToken.RevokedOn = DateTime.UtcNow;

            await userManager.UpdateAsync(user);

            return true;
        }

        private async Task<JwtSecurityToken> CreateJwtToken(ApplicationUser user)
        {
            var userClaims = await userManager.GetClaimsAsync(user);
            var roles = await userManager.GetRolesAsync(user);
            var roleClaims = new List<Claim>();

            foreach (var role in roles)
                roleClaims.Add(new Claim("roles", role));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("uid", user.Id)
            }
            .Union(userClaims)
            .Union(roleClaims);

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
                issuer: jwt.Issuer,
                audience: jwt.Audience,
                claims: claims,
                expires: DateTime.Now.AddDays(jwt.DurationInDays),
                signingCredentials: signingCredentials);

            return jwtSecurityToken;
        }

        private RefreshToken GenerateRefreshToken()
        {
            var randomNumber = new byte[32];

            using var generator = new RNGCryptoServiceProvider();

            generator.GetBytes(randomNumber);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomNumber),
                ExpiresOn = DateTime.UtcNow.AddDays(10),
                CreatedOn = DateTime.UtcNow
            };
        }
    }
}
