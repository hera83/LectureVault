using web.Services.AiGateway.Dtos.Keys;

namespace web.Services.AiGateway;

public partial class AiGatewayService
{
    public async Task<CreateKeyResponseDto> CreateKeyAsync(CreateKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await PostAsync<CreateKeyRequestDto, CreateKeyResponseDto>(client, "Keys/Create", request, cancellationToken);
    }

    public async Task<List<KeyResponseDto>> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await GetAsync<List<KeyResponseDto>>(client, "Keys/List", cancellationToken);
    }

    public async Task<KeyResponseDto> GetKeyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await GetAsync<KeyResponseDto>(client, $"Keys/GetById/{id}", cancellationToken);
    }

    public async Task<KeyResponseDto> UpdateKeyAsync(Guid id, UpdateKeyRequestDto request, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await PutAsync<UpdateKeyRequestDto, KeyResponseDto>(client, $"Keys/Update/{id}", request, cancellationToken);
    }

    public async Task<RolloverKeyResponseDto> RolloverKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await PostAsync<RolloverKeyResponseDto>(client, $"Keys/Rollover/{id}", cancellationToken);
    }

    public async Task<KeyResponseDto> ActivateKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await PostAsync<KeyResponseDto>(client, $"Keys/Activate/{id}", cancellationToken);
    }

    public async Task<KeyResponseDto> DeactivateKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await PostAsync<KeyResponseDto>(client, $"Keys/Deactivate/{id}", cancellationToken);
    }

    public async Task DeleteKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        await DeleteAsync(client, $"Keys/Delete/{id}", cancellationToken);
    }

    public async Task<List<AuditLogEntryResponseDto>> GetKeyAuditLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync(cancellationToken);
        return await GetAsync<List<AuditLogEntryResponseDto>>(client, $"Keys/GetAuditLog/{id}", cancellationToken);
    }
}
