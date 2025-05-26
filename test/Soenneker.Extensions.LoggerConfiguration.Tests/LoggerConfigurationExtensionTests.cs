using System.IO;
using AwesomeAssertions;
using Soenneker.Enums.DeployEnvironment;
using Xunit;


namespace Soenneker.Extensions.LoggerConfiguration.Tests;

public class LoggerConfigurationExtensionTests
{
    [Theory]
    [InlineData(nameof(DeployEnvironment.Local), new[] { "logs", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Test),  new[] { "logs", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Development), new [] { "D:", "home", "LogFiles", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Staging), new [] { "D:", "home", "LogFiles", "log.log" })]
    [InlineData(nameof(DeployEnvironment.Production), new [] { "D:", "home", "LogFiles", "log.log" })]
    public void GetPathFromEnvironment_should_be_expected(string environment, string[] expected)
    {
        DeployEnvironment? env = DeployEnvironment.FromName(environment);

        string result = LoggerConfigurationExtension.GetPathFromEnvironment(env);
        result.Should().Be(Path.Join(expected));
    }
}