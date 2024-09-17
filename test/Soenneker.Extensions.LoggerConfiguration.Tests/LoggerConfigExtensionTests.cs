using System.IO;
using FluentAssertions;
using Soenneker.Enums.DeployEnvironment;
using Soenneker.Tests.Unit;
using Xunit;
using Xunit.Abstractions;

namespace Soenneker.Extensions.LoggerConfiguration.Tests;

public class LoggerConfigExtensionTests : UnitTest
{
    public LoggerConfigExtensionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(nameof(DeployEnvironment.Local), new[] { "logs", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Test),  new[] { "logs", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Development), new [] { "D:", "home", "LogFiles", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Staging), new [] { "D:", "home", "LogFiles", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Production), new [] { "D:", "home", "LogFiles", "log.log" })]
    public void GetPathFromEnvironment_should_be_expected(string environment, string[] expected)
    {
        DeployEnvironment? env = DeployEnvironment.FromName(environment);

        string result = LoggerConfigExtension.GetPathFromEnvironment(env);
        result.Should().Be(Path.Join(expected));
    }
}