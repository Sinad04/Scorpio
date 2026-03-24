namespace WebCrawler;

public class RequestRateMonitor(TimeSpan window, int maxRequests)
{
    private readonly Queue<DateTime> _requestTimes = new();

    public void RecordRequest()
    {
        _requestTimes.Enqueue(DateTime.UtcNow);
    }

    public bool IsRateExceeded()
    {
        var cutoff = DateTime.UtcNow.Subtract(window);
        while (_requestTimes.TryPeek(out var oldest) && oldest < cutoff)
        {
            _requestTimes.TryDequeue(out _);
        }
        return _requestTimes.Count > maxRequests;
    }
}