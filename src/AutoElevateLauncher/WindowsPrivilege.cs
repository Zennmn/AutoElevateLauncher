using System.Security.Principal;

namespace AutoElevateLauncher;

public static class WindowsPrivilege
{
    public static bool IsCurrentProcessAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
