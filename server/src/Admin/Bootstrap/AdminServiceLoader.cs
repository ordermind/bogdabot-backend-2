using Bogdabot.Server.Admin;
using Bogdabot.Server.Shared.Bootstrap;
using Bogdabot.Shared.Admin;

namespace Bogdabot.Server.Admin.Bootstrap;

class AdminServiceLoader(WebApplicationBuilder builder) : IServiceLoader
{
    private WebApplicationBuilder Builder { get; } = builder;

    public void AddServices()
    {
        if (!Builder.Environment.IsDevelopment())
        {
            return;
        }

        Builder.Services.AddSingleton(new AdminSocketOptions(AdminSocket.SocketPath));
        Builder.Services.AddScoped<IAdminCommandHandler, AdminCommandHandler>();
        Builder.Services.AddHostedService<AdminSocketService>();
    }
}
