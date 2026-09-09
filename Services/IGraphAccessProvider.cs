namespace _365MigrationTracker.Services;

/// <summary>
/// Supplies what is needed to call Microsoft Graph on behalf of the signed-in user,
/// and identifies which tenant that user belongs to.
///
/// This is the single place the app learns "whose data are we looking at". Nothing is
/// configured per tenant - it all comes from the signed-in user's token.
/// </summary>
public interface IGraphAccessProvider
{
    /// <summary>
    /// Acquires a delegated Graph access token for the signed-in user.
    /// </summary>
    /// <remarks>
    /// May throw <c>MicrosoftIdentityWebChallengeUserException</c> when the user needs to
    /// consent or satisfy a conditional access policy. Callers rendering UI should pass
    /// that to MicrosoftIdentityConsentAndConditionalAccessHandler rather than swallow it,
    /// or the user sees an error where they should have seen a consent prompt.
    /// </remarks>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The signed-in user's tenant id, from the token's tenant claim.
    /// Every stored snapshot is tagged with this, and every query filtered by it.
    /// Null only when no user is signed in.
    /// </summary>
    Task<string?> GetTenantIdAsync();

    /// <summary>
    /// The signed-in user's display name, for the UI. Null when nobody is signed in.
    /// </summary>
    Task<string?> GetUserDisplayNameAsync();

    /// <summary>
    /// The signed-in user's profile photo as a data URI, or null if they have none.
    ///
    /// Returns null rather than throwing on any failure. The photo is decorative, and
    /// plenty of accounts simply have no photo set - Graph answers 404 for those, which
    /// is a normal outcome, not an error. The caller falls back to a generic icon.
    /// </summary>
    Task<string?> GetUserPhotoDataUriAsync(CancellationToken cancellationToken = default);
}
