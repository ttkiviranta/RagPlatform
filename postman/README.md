# RagPlatform Postman Test Suite

This collection supports the validated Phase 10A authentication and Phase 10B owner-isolation workflow.

## Import

1. Import `RagPlatform.postman_collection.json` into Postman.
2. Import `RagPlatform.environment.template.json` and select **RagPlatform Local**.
3. Start the API using the HTTP profile. The default local URL is `http://localhost:5172`.
4. Configure and acquire User A's and User B's delegated access tokens as described below.
5. Store each access token in the corresponding variable in your local Postman environment. Do not save tokens in the collection or commit an environment after adding tokens.

## Environment

The template defines only these variables:

| Variable | Initial value | Purpose |
| --- | --- | --- |
| `baseUrl` | `http://localhost:5172` | Local API base URL |
| `userAToken` | Empty | User A access token; populate locally |
| `userBToken` | Empty | User B access token; populate locally |
| `documentId` | Empty | Set by the create-document request |

## Authentication Requests

The collection includes **Get User A Token** and **Get User B Token** validation requests. Acquire each token separately with Postman's OAuth 2.0 helper, populate the corresponding local environment variable, and then run the matching request. These requests call the protected `GET /api/documents` endpoint to confirm the token works; they do not send credentials to an application token endpoint.

To acquire a token, use a temporary Postman request and configure its Authorization tab with the OAuth 2.0 helper. Set:

- Grant Type: **Authorization Code (With PKCE)**
- Callback URL: `https://oauth.pstmn.io/v1/browser-callback`
- Auth URL: `https://login.microsoftonline.com/6b50c2f8-c252-4fc8-b634-91801ee9cc64/oauth2/v2.0/authorize`
- Access Token URL: `https://login.microsoftonline.com/6b50c2f8-c252-4fc8-b634-91801ee9cc64/oauth2/v2.0/token`
- Client ID: the dedicated Postman client application's client ID
- Client Secret: leave empty
- Code Challenge Method: **SHA-256**
- Scope: `api://de95d3a7-3783-461f-892b-85b517a4ea1f/access_as_user openid profile`

Register the callback URI in the Postman client app registration as a redirect URI compatible with the PKCE flow. Sign in as the intended user, obtain the access token, and copy it only into that user's local environment variable. The client application must have the delegated `access_as_user` permission for the RagPlatform API.

Use the Postman access-token dialog to copy the resulting access token into `userAToken` or `userBToken` in the local environment. Keep the collection requests configured to use their bearer-token environment variables.

## Document Requests

- **Get Documents User A** - calls `GET {{baseUrl}}/api/documents` with `userAToken`.
- **Get Documents User B** - calls `GET {{baseUrl}}/api/documents` with `userBToken` and verifies the User A marker document is absent.
- **Create Document User A** - calls `POST {{baseUrl}}/api/documents` with `userAToken`, creates `USER_A_PRIVATE_DOCUMENT`, and saves the returned document ID to `documentId`.

The create request uses this payload:

```json
{
  "title": "USER_A_PRIVATE_DOCUMENT",
  "content": "Private content used to validate Phase 10B owner-based access."
}
```

## Phase 10B Validation Workflow

1. Run **Get User A Token** and authenticate as User A.
2. Run **Create Document User A**.
3. Run **Get Documents User A** and confirm the created document appears.
4. Run **Get User B Token** and authenticate as User B.
5. Run **Get Documents User B** and confirm `USER_A_PRIVATE_DOCUMENT` is not returned.

Expected results:

- ✅ User A can access owned resources
- ✅ User B cannot access User A resources
- ✅ Resource ownership enforced
- ✅ RBAC validated

## Security

- The committed environment template contains no access tokens, refresh tokens, passwords, client secrets, connection strings, or API keys.
- The collection contains no user credentials or token values.
- Keep acquired tokens in a local Postman environment. Do not commit populated environments, exported token data, or screenshots containing credentials.
- Treat access tokens as credentials. Never paste them into source files, issue comments, or shared logs.
