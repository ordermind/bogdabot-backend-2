using Bogdabot.Server.Admin;
using Bogdabot.Server.Admin.Bootstrap;
using Bogdabot.Shared.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bogdabot.Server.Tests.Unit.Admin;

public class AdminServiceLoaderTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void AddServices_RegistersSocketOnlyInDevelopment(string environmentName, bool expected)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        new AdminServiceLoader(builder).AddServices();

        var listener = builder.Services.SingleOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(AdminSocketService));

        if (expected)
        {
            Assert.NotNull(listener);
            Assert.Contains(
                builder.Services,
                descriptor => descriptor.ServiceType == typeof(IAdminCommandHandler));
            Assert.Contains(
                builder.Services,
                descriptor => descriptor.ServiceType == typeof(AdminSocketOptions) &&
                              descriptor.ImplementationInstance is AdminSocketOptions
                              {
                                  SocketPath: AdminSocket.SocketPath
                              });
        }
        else
        {
            Assert.Null(listener);
            Assert.DoesNotContain(
                builder.Services,
                descriptor => descriptor.ServiceType == typeof(IAdminCommandHandler));
            Assert.DoesNotContain(
                builder.Services,
                descriptor => descriptor.ServiceType == typeof(AdminSocketOptions));
        }
    }
}
