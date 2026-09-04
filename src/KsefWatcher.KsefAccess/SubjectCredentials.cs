namespace KsefWatcher.KsefAccess;

/// <summary>Read from validated config; never logged (I-14). docs/08_ksef_access_tactical_model.md.</summary>
public sealed record SubjectCredentials(string Nip, string Token, string Environment);
