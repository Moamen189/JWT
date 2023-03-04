﻿using JWT.Models;
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

namespace JWT.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly JWTT jwt;

        public AuthService(UserManager<ApplicationUser> userManager , IOptions<JWTT> jwt)
        {
            this.userManager = userManager;
            jwt = jwt;
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
                ExpiresOn = jwtSecurityToken.ValidTo,
                IsAuthenticated = true,
                Roles = new List<string> { "User" },
                Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken),
                Username = user.UserName

            };
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
    }
}
