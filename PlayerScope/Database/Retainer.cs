using System.ComponentModel.DataAnnotations;

namespace PlayerScope.Database;

public class Retainer
{
    [Key, Required]
    public ulong LocalContentId { get; set; }

    [MaxLength(24), Required]
    public string? Name { get; set; }

    [Required]
    public ushort WorldId { get; set; }

    [Required]
    public ulong OwnerLocalContentId { get; set; }
}
