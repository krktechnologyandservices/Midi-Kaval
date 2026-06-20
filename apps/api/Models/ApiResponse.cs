namespace MidiKaval.Api.Models;

public sealed class ApiResponse<T>
{
    public ApiResponse(T data, ApiMeta meta)
    {
        Data = data;
        Meta = meta;
    }

    public T Data { get; }

    public ApiMeta Meta { get; }
}
