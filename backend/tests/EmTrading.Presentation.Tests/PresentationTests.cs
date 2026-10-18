using EmTrading.Application.Backtesting.Strategies;
using EmTrading.Presentation.Backtesting;
using EmTrading.Presentation.Common;
using EmTrading.Presentation.Logging;

namespace EmTrading.Presentation.Tests;

public class ThrottledActionTests
{
    [Fact]
    public async Task A_burst_of_requests_from_many_threads_runs_the_action_once()
    {
        var runs = 0;
        var throttle = new ThrottledAction(TimeSpan.FromMilliseconds(100), () => Interlocked.Increment(ref runs), uiContext: null);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
                throttle.Request();
        })));
        await Task.Delay(400, TestContext.Current.CancellationToken);

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task A_request_after_the_interval_runs_again()
    {
        var runs = 0;
        var throttle = new ThrottledAction(TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref runs), uiContext: null);

        throttle.Request();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        throttle.Request();
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(2, runs);
    }
}

public class LogFeedTests
{
    [Fact]
    public void Keeps_only_the_newest_entries()
    {
        var feed = new LogFeed();
        for (var i = 0; i < LogFeed.Capacity + 10; i++)
            feed.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Information, "Test", $"line {i}"));

        var entries = feed.Snapshot();

        Assert.Equal(LogFeed.Capacity, entries.Count);
        Assert.Equal("line 10", entries[0].Message);
        Assert.Equal($"line {LogFeed.Capacity + 9}", entries[^1].Message);
    }

    [Fact]
    public async Task Appending_from_many_threads_loses_nothing()
    {
        var feed = new LogFeed();
        var notifications = 0;
        feed.Appended += (_, _) => Interlocked.Increment(ref notifications);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
                feed.Append(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Debug, $"T{t}", "x"));
        })));

        Assert.Equal(1000, feed.Snapshot().Count);
        Assert.Equal(1000, notifications);
    }
}

[Collection(nameof(CultureCollection))]
public class StrategyParameterInputTests
{
    private static readonly StrategyParameterDescriptor Period =
        new("fast-period", "Fast EMA period", StrategyParameterKind.Integer, 26, 2, 500, null, "In 15-minute bars.");

    [Fact]
    public void Starts_with_the_default_and_no_error()
    {
        var input = new StrategyParameterInput(Period);

        Assert.Equal("26", input.Value);
        Assert.Null(input.Error);
    }

    [Theory]
    [InlineData("1", "Fast EMA period must be at least 2.")]
    [InlineData("2.5", "Fast EMA period must be a whole number.")]
    [InlineData("x", "Fast EMA period: 'x' is not a number.")]
    public void Shows_the_validation_message_as_soon_as_the_value_changes(string value, string expected)
    {
        var input = new StrategyParameterInput(Period);
        var errorChanged = false;
        input.PropertyChanged += (_, e) => errorChanged |= e.PropertyName == nameof(StrategyParameterInput.Error);

        input.Value = value;

        Assert.Equal(expected, input.Error);
        Assert.True(errorChanged);
    }

    [Fact]
    public void Hint_combines_description_and_range()
    {
        Assert.Contains("In 15-minute bars.", new StrategyParameterInput(Period).Hint);
        Assert.Contains("2 – 500", new StrategyParameterInput(Period).Hint);
    }
}
