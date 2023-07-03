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
    [InlineData(nameof(DeployEnvironment.Local), Path("logs", "log.log"))]
    [InlineData(nameof(DeployEnvironment.Test), Path("logs", "log.log"))]
    [InlineData(nameof(DeployEnvironment.Development), @"D:\home\LogFiles\log.log")]
    [InlineData(nameof(DeployEnvironment.Staging), @"D:\home\LogFiles\log.log")]
    [InlineData(nameof(DeployEnvironment.Production), @"D:\home\LogFiles\log.log")]
    public void GetPathFromEnvironment_should_be_expected(string environment, string expected)
    {
        DeployEnvironment? env = DeployEnvironment.FromName(environment);

        string result = LoggerConfigExtension.GetPathFromEnvironment(env);
        result.Should().Be(expected);
    }
}
