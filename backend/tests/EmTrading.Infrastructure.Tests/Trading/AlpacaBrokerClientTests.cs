using System.Net;
using System.Text;
using System.Text.Json;
using EmTrading.Application.Trading;
using EmTrading.Domain.MarketData;
using EmTrading.Domain.Trading;
using EmTrading.Infrastructure.Alpaca;
using EmTrading.Infrastructure.Alpaca.Trading;
using Microsoft.Extensions.Options;

namespace EmTrading.Infrastructure.Tests.Trading;

public class AlpacaBrokerClientTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 14, 30, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string AcceptedOrder =
        """{"id":"b-1","client_order_id":"emt-1","symbol":"AAPL","status":"accepted","qty":"10","filled_qty":"0","filled_avg_price":null,"updated_at":"2026-10-19T14:30:01Z"}""";

    [Fact]
    public async Task Submits_the_order_with_its_client_id_and_trading_keys()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, AcceptedOrder));
        var client = CreateClient(handler, new AlpacaOptions { ApiKey = "data", ApiSecret = "data-secret", TradingApiKey = "paper", TradingApiSecret = "paper-secret" });
        var order = Order.Create(ClientOrderId.From("emt-1"), Symbol.Create("AAPL"), OrderSide.Sell, OrderType.StopLimit, 10, 180.5m, 181, TimeInForce.Gtc, T0);

        var accepted = await client.SubmitOrderAsync(order, Ct);

        Assert.Equal(("b-1", OrderStatus.Submitted), (accepted.BrokerOrderId, accepted.Status));
        var request = Assert.Single(handler.Requests);
        Assert.Equal((HttpMethod.Post, "/v2/orders"), (request.Method, request.Uri.AbsolutePath));
        Assert.Equal("paper", request.Headers["APCA-API-KEY-ID"]);

        using var body = JsonDocument.Parse(request.Body!);
        var json = body.RootElement;
        Assert.Equal("emt-1", json.GetProperty("client_order_id").GetString());
        Assert.Equal("10", json.GetProperty("qty").GetString());
        Assert.Equal(("sell", "stop_limit", "gtc"), (json.GetProperty("side").GetString(), json.GetProperty("type").GetString(), json.GetProperty("time_in_force").GetString()));
        Assert.Equal(("180.5", "181"), (json.GetProperty("limit_price").GetString(), json.GetProperty("stop_price").GetString()));
    }

    [Fact]
    public async Task Market_orders_send_no_prices_and_fractional_quantities_as_text()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, AcceptedOrder));
        var order = Order.Create(ClientOrderId.From("emt-2"), Symbol.Create("AAPL"), OrderSide.Buy, OrderType.Market, 0.123456789m, null, null, TimeInForce.Day, T0);

        await CreateClient(handler).SubmitOrderAsync(order, Ct);

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("0.123456789", body.RootElement.GetProperty("qty").GetString());
        Assert.False(body.RootElement.TryGetProperty("limit_price", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, true)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task Client_errors_are_rejections_and_server_errors_unknown(HttpStatusCode status, bool isRejection)
    {
        var handler = new StubHandler(_ => Json(status, """{"code":40310000,"message":"insufficient buying power"}"""));

        var error = await Assert.ThrowsAsync<BrokerException>(() => CreateClient(handler).GetAccountAsync(Ct));

        Assert.Equal(isRejection, error.IsRejection);
        Assert.Equal("insufficient buying power", error.Message);
    }

    [Fact]
    public async Task No_answer_is_an_unknown_outcome()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection reset"));

        var error = await Assert.ThrowsAsync<BrokerException>(() =>
            CreateClient(handler).SubmitOrderAsync(Order.Create(ClientOrderId.From("emt-3"), Symbol.Create("AAPL"), OrderSide.Buy, OrderType.Market, 1, null, null, TimeInForce.Day, T0), Ct));

        Assert.False(error.IsRejection);
    }

    [Fact]
    public async Task An_order_the_broker_does_not_know_is_null()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.NotFound, """{"code":40410000,"message":"order not found"}"""));

        Assert.Null(await CreateClient(handler).GetOrderAsync(ClientOrderId.From("emt-4"), Ct));
        Assert.Contains("client_order_id=emt-4", handler.Requests[0].Uri.Query);
    }

    [Fact]
    public async Task Account_and_positions_are_parsed_from_string_numbers()
    {
        var handler = new StubHandler(request => request.Uri.AbsolutePath == "/v2/account"
            ? Json(HttpStatusCode.OK, """{"account_number":"PA1","currency":"USD","equity":"101000.5","last_equity":"100000","cash":"50000","buying_power":"200000","trading_blocked":false,"account_blocked":false}""")
            : Json(HttpStatusCode.OK, """[{"symbol":"MSFT","qty":"-3","avg_entry_price":"400","current_price":"390","market_value":"-1170","unrealized_pl":"30","unrealized_plpc":"0.025"}]"""));
        var client = CreateClient(handler);

        var account = await client.GetAccountAsync(Ct);
        var position = Assert.Single(await client.GetPositionsAsync(Ct));

        Assert.Equal(1000.5m, account.DailyProfitLoss);
        Assert.True(account.IsPaper);
        Assert.Equal((-3m, 2.5m), (position.Quantity, position.UnrealizedProfitLossPercent));
    }

    [Fact]
    public async Task Missing_keys_fail_before_any_request()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));

        var error = await Assert.ThrowsAsync<BrokerException>(() => CreateClient(handler, new AlpacaOptions()).GetAccountAsync(Ct));

        Assert.True(error.IsRejection);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("accepted", OrderStatus.Submitted)]
    [InlineData("pending_cancel", OrderStatus.Submitted)]
    [InlineData("partially_filled", OrderStatus.PartiallyFilled)]
    [InlineData("filled", OrderStatus.Filled)]
    [InlineData("canceled", OrderStatus.Cancelled)]
    [InlineData("expired", OrderStatus.Expired)]
    [InlineData("rejected", OrderStatus.Rejected)]
    public void Alpaca_statuses_map_onto_order_statuses(string alpaca, OrderStatus expected) =>
        Assert.Equal(expected, AlpacaBrokerClient.MapStatus(alpaca));

    [Theory]
    [InlineData("https://paper-api.alpaca.markets", "wss://paper-api.alpaca.markets/stream", true)]
    [InlineData("https://api.alpaca.markets", "wss://paper-api.alpaca.markets/stream", false)]
    [InlineData("https://paper-api.alpaca.markets", "wss://api.alpaca.markets/stream", false)]
    public void Only_the_paper_trading_endpoints_are_accepted(string tradingUrl, string streamUrl, bool valid)
    {
        var result = new PaperTradingOnlyValidation().Validate(null, new AlpacaOptions { TradingBaseUrl = tradingUrl, TradingStreamUrl = streamUrl });

        Assert.Equal(valid, result.Succeeded);
    }

    private static AlpacaBrokerClient CreateClient(HttpMessageHandler handler, AlpacaOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://paper-api.alpaca.markets/") },
            Options.Create(options ?? new AlpacaOptions { ApiKey = "key", ApiSecret = "secret" }));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string? Body);

    private sealed class StubHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(recorded);
            return respond(recorded);
        }
    }
}
