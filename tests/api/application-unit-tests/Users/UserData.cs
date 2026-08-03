using Domain.Users;

namespace Application.UnitTests.Users;

internal static class UserData
{
    public static User Create() => User.Create(Name, Email);

    public static readonly string Name = new("Usman");
    public static readonly string Email = new("test@test.com");
}
