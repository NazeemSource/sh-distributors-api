using Distributor.Api.Domain;
using Distributor.Api.Services;

namespace API.Tests;

public sealed class UserProfileTests
{
    [Fact]
    public void User_view_includes_sales_rep_profile_fields()
    {
        var user = new User
        {
            Name = "Demo Rep", Username = "demo_rep", PasswordHash = "hash", Role = "Rep",
            Territory = "Colombo", Address = "12 Main Street", Nic = "991234567V",
            Phone = "0771234567", Email = "demo@example.com", MonthlyTarget = 125000
        };

        var view = UserView.From(user);

        Assert.Equal(user.Address, view.Address);
        Assert.Equal(user.Nic, view.Nic);
        Assert.Equal(user.Phone, view.Phone);
        Assert.Equal(user.Email, view.Email);
        Assert.Equal(user.MonthlyTarget, view.MonthlyTarget);
    }
}
