using SharedKernel;

namespace Domain.Users;

public class User : Entity
{
    public User(Guid id, string name, string email)
        : base(id)
    {
        Name = name;
        Email = email;
    }

    public User()
    {

    }

    public string Name { get; set; }

    public string Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public static User Create(string name, string email)
    {
        var user = new User(Guid.NewGuid(), name, email)
        {
            CreatedAt = DateTime.UtcNow
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }
}
