namespace Budgie.Modules.EmailImport.Application.Connections;

/// <summary>
/// Provider-neutral result of building an OAuth authorization URI: the URI the user must
/// visit, plus the state that was embedded in it (the endpoint verifies the callback's
/// state against this value through the signed OAuth state, never against the provider).
/// </summary>
/// <param name="Uri">The absolute authorization URI, ready to redirect the user to.</param>
/// <param name="State">The state value embedded in the URI.</param>
public sealed record EmailAuthorizationUri(string Uri, string State);
