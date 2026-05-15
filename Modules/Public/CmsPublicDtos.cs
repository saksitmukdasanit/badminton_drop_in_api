namespace DropInBadAPI.Modules.Public;

public record PublicAppProfileAboutDto(
    string AppLogoUrl,
    string AppName,
    string AppVersion,
    string SupportEmail,
    string Description,
    string PrivacyPolicy,
    string TermsAndConditions,
    string PolicyUrl,
    string TermsUrl,
    DateTime UpdatedAtUtc);
