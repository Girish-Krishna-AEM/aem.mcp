using CoordinatorAgent.Models;
using CoordinatorAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoordinatorAgent.Tests.Services;

public class IntentClassifierTests
{
    private readonly IntentClassifier _classifier = new(NullLogger<IntentClassifier>.Instance);

    [Theory]
    [InlineData("Is there lightning near Austin, TX?", Intent.Strike)]
    [InlineData("Any strike activity close to Dallas?", Intent.Strike)]
    [InlineData("Get all the LX for Germantown MD around 50 miles", Intent.Strike)]
    [InlineData("What's the weather forecast for Houston?", Intent.Weather)]
    [InlineData("Will it rain tomorrow?", Intent.Weather)]
    [InlineData("Show me hourly forecast near Urbana, MD in details", Intent.Weather)]
    [InlineData("What's the weather forecast near Austin, TX?", Intent.Weather)]
    [InlineData("Show me sensor diagnostics for sensor-001", Intent.Sensor)]
    [InlineData("What's the calibration status of TX-123?", Intent.Sensor)]
    [InlineData("What is the status of informer-001?", Intent.Informer)]
    [InlineData("Is the horn in zone-a powered on?", Intent.Informer)]
    public void Classify_ReturnsExpectedIntent(string query, Intent expected)
    {
        var result = _classifier.Classify(query);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_UnrecognizedQuery_ReturnsNull()
    {
        var result = _classifier.Classify("Hello, how are you today?");
        Assert.Null(result);
    }
}
