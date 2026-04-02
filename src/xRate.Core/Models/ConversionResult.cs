using System;

namespace xRate.Core.Models;

public class ConversionResult
{
    public RateResponse[]? Rates { get; set; }

    public bool IsOffline { get; set; }

    public DateTime? OfflineDate { get; set; }
}