# datacom-Kudos-forage

## Local dev
- Backend: `dotnet run --project backend/Kudos.Api.csproj`
- Frontend: `cd frontend` then `npm install` and `npm run dev`

## AWS Cognito (OAuth2 / OIDC)
Update backend config in `backend/appsettings.json`:
- `Cognito:Region`
- `Cognito:UserPoolId`
- `Cognito:ClientId`

Update frontend env (copy `frontend/.env.example` to `frontend/.env`):
- `VITE_COGNITO_REGION`
- `VITE_COGNITO_USER_POOL_ID`
- `VITE_COGNITO_CLIENT_ID`
- `VITE_COGNITO_DOMAIN`
- `VITE_COGNITO_REDIRECT_URI`

## API
- `GET /api/users` (auth required)
- `GET /api/kudos?page=1&pageSize=8&team=Engineering&search=great` (auth required)
- `POST /api/kudos` (auth required)

## Dry-run mode
Set `Kudos:DryRun` to `true` in `backend/appsettings.json` or via env var `Kudos__DryRun=true`.
- Writes are skipped for `POST /api/kudos` (returns `id: "dry-run"`).

## Tests
- `dotnet test backend/Kudos.Api.Tests/Kudos.Api.Tests.csproj`
