using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class TaskXmlBuilderTests
{
    [Fact]
    public void BuildStartupItemTaskXml_UsesHighestAvailablePrivilegesAndLogonTrigger()
    {
        var item = new StartupItem { Id = "abc", Name = "Demo", Enabled = true };
        item.EnsureTaskName();

        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, "C:\\Tools\\AutoElevateLauncher.exe", "TEST-PC\\me");

        Assert.Contains("<LogonTrigger>", xml);
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml);
        Assert.Contains("<Command>C:\\Tools\\AutoElevateLauncher.exe</Command>", xml);
        Assert.Contains("<Arguments>--run-item abc</Arguments>", xml);
        Assert.Contains("<UserId>TEST-PC\\me</UserId>", xml);
    }

    [Fact]
    public void BuildStartupItemTaskXml_DisablesTaskWhenItemDisabled()
    {
        var item = new StartupItem { Id = "abc", Name = "Demo", Enabled = false };
        item.EnsureTaskName();

        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, "C:\\Tools\\AutoElevateLauncher.exe", "TEST-PC\\me");

        Assert.Contains("<Enabled>false</Enabled>", xml);
    }
}