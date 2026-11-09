using Bogdabot.Shared.Admin;

namespace Bogdabot.Server.Admin;

public interface IAdminCommandHandler
{
    Task<AdminResponse> ExecuteAsync(AdminRequest request, CancellationToken cancellationToken);
}
