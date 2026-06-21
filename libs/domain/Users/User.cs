using SharedKernel;

namespace Domain.Users;

public class User : Entity
{
    public string Name { get; set; }

    public string Email { get; set; }

    public DateTime CreatedAt { get; set; }
}
