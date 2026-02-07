# datacom-Kudos-forage

## Setup
1. Ensure MongoDB is running locally on `mongodb://localhost:27017`.
2. Configure AWS Cognito (see next section).
3. Create frontend env file:
   - `cp frontend/.env.example frontend/.env`
4. Install frontend deps:
   - `cd frontend`
   - `npm install`

## AWS Cognito (OAuth2 / OIDC)
Backend config is read from `backend/appsettings.json` and `backend/appsettings.Development.json` (or env vars).
- `Cognito:Region`
- `Cognito:UserPoolId`
- `Cognito:ClientId`

Frontend config is read from `frontend/.env`:
- `VITE_COGNITO_REGION`
- `VITE_COGNITO_USER_POOL_ID`
- `VITE_COGNITO_CLIENT_ID`
- `VITE_COGNITO_DOMAIN`
- `VITE_COGNITO_REDIRECT_URI`

Required Cognito app client settings:
- App client type: SPA (no client secret)
- OAuth grant type: Authorization code grant
- OIDC scopes: `openid`, `email`
- Callback URL: `http://localhost:5173`
- Sign-out URL: `http://localhost:5173`

## Run locally
- Backend: `dotnet run --project backend/Kudos.Api.csproj`
- Frontend: `cd frontend` then `npm run dev`

## Sign in
1. Open `http://localhost:5173`.
2. Click `Sign in`.
3. Complete Cognito login.
4. You should be redirected back to the app.

## Admin moderation
Admin users (Cognito group/role `KudosAdmin` or `Admin`) can hide/show or delete kudos.

## API
- `GET /api/users` (auth required)
- `GET /api/kudos?page=1&pageSize=8&team=Engineering&search=great` (auth required)
- `POST /api/kudos` (auth required)
- `PATCH /api/kudos/{id}/visibility` (admin only)
- `DELETE /api/kudos/{id}` (admin only)

## Dry-run mode
Set `Kudos:DryRun=true` or env var `Kudos__DryRun=true`.
Writes are skipped for `POST /api/kudos` (returns `id: "dry-run"`).

## Tests
- `dotnet test backend/Kudos.Api.Tests/Kudos.Api.Tests.csproj`

## Preview
<img width="2044" height="1758" alt="acb471b8962bdcb8427138dce5a04f4b" src="https://github.com/user-attachments/assets/d0fd32bc-3952-4aa5-bcb3-9c1dfb59ea3b" />
<img width="2024" height="774" alt="ec2e04fe3e36aa97edb2b1f4ea44a7ea" src="https://github.com/user-attachments/assets/bda4db0c-ae6b-409f-b583-56a9aa81eb5c" />

