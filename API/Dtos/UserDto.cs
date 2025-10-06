using System;

namespace API.Dtos;

public class UserDto
{
    public required int Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? ImageUrl { get; set; }
    public required string Token { get; set; }
}
