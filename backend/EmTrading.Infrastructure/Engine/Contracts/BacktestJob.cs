using System.Globalization;
using System.Text.Json;
using EmTrading.Application.Backtesting;

namespace EmTrading.Infrastructure.Engine.Contracts;

/// <summary>
/// Everything the engine runner needs for one backtest. The app writes it as JSON next to the results
/// and passes the file path as the runner's only argument.
/// </summary>
/// <param name="ResultsFolder">Folder the engine writes <c>{RunId}.json</c> into.</param>
public sealed record BacktestJob(
    string RunId,
    string StrategyName,
    string Symbol,
    DateOnly Start,
    DateOnly End,
    decimal InitialCash,
    string DataFolder,
    string ResultsFolder,
    IReadOnlyDictionary<string, string> Parameters)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<BacktestJob> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<BacktestJob>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidDataException($"Job file '{path}' is empty.");
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, this, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parameters handed to the algorithm; the strategy reads them with <c>GetParameter</c>.
    /// </summary>
    public Dictionary<string, string> ToAlgorithmParameters() => new(Parameters)
    {
        [StrategyParameterNames.Symbol] = Symbol,
        [StrategyParameterNames.StartDate] = Start.ToString(StrategyParameterNames.DateFormat, CultureInfo.InvariantCulture),
        [StrategyParameterNames.EndDate] = End.ToString(StrategyParameterNames.DateFormat, CultureInfo.InvariantCulture),
        [StrategyParameterNames.Cash] = InitialCash.ToString(CultureInfo.InvariantCulture)
    };
}
