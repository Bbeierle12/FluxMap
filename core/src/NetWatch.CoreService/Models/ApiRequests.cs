namespace NetWatch.CoreService.Models;

public sealed record CredentialCreateRequest(string Name, string Purpose, string Secret);
public sealed record CredentialTestRequest(string Id);
public sealed record AgentRegisterRequest(string Code, string? Name);
public sealed record AgentRegisterResponse(string Token, string Mode);
