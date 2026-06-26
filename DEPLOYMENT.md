# CounselMate Deployment Notes

## Neon

Create a PostgreSQL database in Neon and copy the pooled or direct connection string.

Use one of these backend environment variables:

- `ConnectionStrings__DefaultConnection`
- `DATABASE_URL`
- `NEON_DATABASE_URL`

The API accepts both normal Npgsql connection strings and Neon URL strings such as `postgresql://...`.

## Render Backend

Create a Render Web Service for `backend`.

Recommended settings:

- Root directory: repository root
- Build command: `dotnet restore backend/EducationCrm.Api.csproj && dotnet publish backend/EducationCrm.Api.csproj -c Release -o backend/publish`
- Start command: `dotnet backend/publish/EducationCrm.Api.dll`
- Environment variable: `DATABASE_URL` with the Neon PostgreSQL connection string
- Environment variable: `DefaultTenantSlug=demo-academy`
- Environment variable: `Cors__AllowedOrigins__0=https://your-vercel-domain.vercel.app`

Apply migrations before first production use:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/EducationCrm.Api.csproj --startup-project backend/EducationCrm.Api.csproj
```

## Vercel Frontend

Create a Vercel project for `frontend`.

Recommended settings:

- Framework preset: Vite
- Build command: `npm run build`
- Output directory: `dist`
- Environment variable: `VITE_API_BASE_URL=https://your-render-service.onrender.com/api`

The frontend still uses local mock data. The next development step is to add an API client that reads `VITE_API_BASE_URL`.
