using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebUI.Hubs
{
    [Authorize(Roles = "Admin")]
    public class AdminHub : Hub
    {
    }
}