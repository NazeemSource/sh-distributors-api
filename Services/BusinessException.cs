namespace Distributor.Api.Services;

public sealed class BusinessException(string code, string message, int status = 422) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

