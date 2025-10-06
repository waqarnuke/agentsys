using System;
using API.Dtos;
using API.Entities;
using API.Interfaces;

namespace API.Extension;

public static class AppUserExtensions
{
    public static UserDto ToDto(this AppUser user, ITokenServices tokenService)
    {
        return new UserDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Token = tokenService.CreateToken(user)
        };
    }
}
