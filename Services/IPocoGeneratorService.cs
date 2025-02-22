using SQLPocoAPI.Models;

namespace SQLPocoAPI.Services;

public interface IPocoGeneratorService
{
    Task<ConversionResponse> GeneratePocoAsync(ConversionRequest request);
}