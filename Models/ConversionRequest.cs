namespace SQLPocoAPI.Models;

public class ConversionRequest
{
    public string SqlScript { get; set; } = string.Empty;
    public string Language { get; set; } = "csharp";
}

public class ConversionResponse
{
    public Dictionary<string, string> GeneratedCode { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}