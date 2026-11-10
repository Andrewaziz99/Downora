using Downora.Core.Services;

namespace Downora.Core.Tests;

public class StartupCommandTests
{
    [Fact]
    public void The_path_is_quoted_so_spaces_cannot_split_it()
    {
        var command = StartupCommand.Build(@"C:\Program Files\Downora\Downora.exe");

        Assert.Equal("\"C:\\Program Files\\Downora\\Downora.exe\" --startup", command);
    }

    [Theory]
    [InlineData(new[] { "--startup" }, true)]
    [InlineData(new[] { "--STARTUP" }, true)]
    [InlineData(new[] { "other", "--startup" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--startups", "startup" }, false)]
    public void A_launch_by_windows_is_recognised_from_its_argument(string[] args, bool expected) =>
        Assert.Equal(expected, StartupCommand.IsStartupLaunch(args));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void An_empty_path_is_rejected(string path) =>
        Assert.Throws<ArgumentException>(() => StartupCommand.Build(path));
}
