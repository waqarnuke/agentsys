using System;
using System.ComponentModel.DataAnnotations;

namespace API.Entities;

public class AppUser :BaseEntity
{
    [Required, MaxLength(50)]
    public string DisplayName { get; set; } = null!;
    [Required, MaxLength(50)]
    [EmailAddress]
    public string Email { get; set; } = null!;
    [Required]
    public byte[] PasswordHash { get; set; } = null!;
    [Required]
    public byte[] PasswordSalt { get; set; } = null!;

}
