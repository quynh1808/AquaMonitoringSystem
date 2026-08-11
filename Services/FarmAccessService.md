# FarmAccessService

This service is the server-side authorization boundary for farm mutations and pond access.
An empty `pond_access` claim set means all ponds allowed by the user's role; otherwise only
explicitly assigned pond IDs are visible and operable. Administrator bypasses pond scoping.

UI `AuthorizeView` elements are presentation helpers only and must not replace calls to this
service from state-changing application services.

