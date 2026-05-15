namespace DropInBadAPI.Modules.Admin;

public record BankUpsertDto(string BankName, string? BankCode, bool IsActive);

public record FacilityUpsertDto(string FacilityName, string? IconUrl, bool IsActive);

public record GameTypeUpsertDto(string TypeName, bool IsActive);

public record PairingMethodUpsertDto(string MethodName, bool IsActive);

public record ShuttlecockBrandUpsertDto(string BrandName, bool IsActive);

public record ShuttlecockModelUpsertDto(string ModelName, int BrandId, bool IsActive);

