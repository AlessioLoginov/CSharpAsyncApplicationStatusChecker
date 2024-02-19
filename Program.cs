using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

interface IClient
{
    Task<IResponse> GetApplicationStatus(string id, CancellationToken cancellationToken);
}

interface IResponse { }

record SuccessResponse(string Id, string Status) : IResponse;
record FailureResponse() : IResponse;
record RetryResponse(TimeSpan Delay) : IResponse;

interface IHandler
{
    Task<IApplicationStatus> GetApplicationStatus(string id);
}

interface IApplicationStatus { }

record SuccessStatus(string ApplicationId, string Status) : IApplicationStatus;
record FailureStatus(DateTime? LastRequestTime, int RetriesCount) : IApplicationStatus;

class Handler : IHandler
{
    private readonly IClient _client1;
    private readonly IClient _client2;
    private readonly ILogger<Handler> _logger;

    public Handler(IClient client1, IClient client2, ILogger<Handler> logger = null)
    {
        _client1 = client1;
        _client2 = client2;
        _logger = logger ?? new NullLogger<Handler>();
    }

    public async Task<IApplicationStatus> GetApplicationStatus(string id)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var task1 = _client1.GetApplicationStatus(id, cts.Token);
        var task2 = _client2.GetApplicationStatus(id, cts.Token);

        var task = await Task.WhenAny(task1, task2);
        try
        {
            var response = await task;
            return response switch
            {
                SuccessResponse sr => new SuccessStatus(sr.Id, sr.Status),
                FailureResponse => new FailureStatus(DateTime.Now, 0),
                RetryResponse rr => new FailureStatus(DateTime.Now, 1), // Simplified for demonstration
                _ => new FailureStatus(null, 0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application status");
            return new FailureStatus(DateTime.Now, 0);
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Example clients and logger
        IClient client1 = new MockClient();
        IClient client2 = new MockClient();
        ILogger<Handler> logger = new NullLogger<Handler>();

        var handler = new Handler(client1, client2, logger);
        var status = await handler.GetApplicationStatus("123");
        Console.WriteLine(status);
    }
}

// Mock implementation for demonstration purposes
class MockClient : IClient
{
    public async Task<IResponse> GetApplicationStatus(string id, CancellationToken cancellationToken)
    {
        await Task.Delay(1000); // Simulate network delay
        return new SuccessResponse(id, "Processed");
    }
}
