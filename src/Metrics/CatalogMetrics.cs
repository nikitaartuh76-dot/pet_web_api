using System.Diagnostics.Metrics;

namespace ASP_FINAL_PROJECT.Metrics;

public sealed class CatalogDbMetrics
{
    public const string MeterName = "CatalogMeter";

    private readonly Counter<long> _listReads;
    private readonly Histogram<double> _listReadDuration;

    public CatalogDbMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _listReads = meter.CreateCounter<long>(
            "DbReadCounter", description: "Successful lesson list reads from PostgreSQL.");
        _listReadDuration = meter.CreateHistogram<double>(
            "catalog.db.list.read.duration",
            unit: "s",
            description: "Time spent loading the lesson list from PostgreSQL.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
            });
    }

    public void CountListRead() => _listReads.Add(1);

    public void RecordListReadDuration(TimeSpan elapsed) => _listReadDuration.Record(elapsed.TotalSeconds);
}
