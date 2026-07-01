# Deployment Guide

## 1. Local Frontend

```powershell
npm install
npm run dev -- --host 127.0.0.1 --port 5173
```

Build:

```powershell
npm run build
```

## 2. Planned Local Full Stack

Required services:

- Frontend: Vite
- Backend: ASP.NET Core
- Database: SQL Server
- Cache: Redis
- Object storage: local emulator or Azure Blob later

Recommended ports:

- Frontend: `5173`
- Backend: `5000` or `5001`
- SQL Server: `1433`
- Redis: `6379`

## 3. Environment Variables

```text
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__LearnOsDb=Server=localhost;Database=LearnOS;User Id=sa;Password=...
Redis__ConnectionString=localhost:6379
Jwt__Issuer=learnos
Jwt__Audience=learnos-web
Jwt__SigningKey=<long-secret>
OpenAI__ApiKey=<secret>
Storage__Provider=Local
Storage__LocalPath=./storage
```

## 4. Production Deployment Target

Azure recommendation:

- Azure App Service or Azure Container Apps for backend.
- Azure Static Web Apps or CDN for frontend.
- Azure SQL Database.
- Azure Cache for Redis.
- Azure Blob Storage.
- Azure Key Vault.
- Application Insights.

## 5. CI/CD Pipeline

GitHub Actions stages:

1. Checkout.
2. Install Node dependencies.
3. Run frontend lint and build.
4. Restore .NET dependencies.
5. Run backend tests.
6. Build Docker images.
7. Push images to registry.
8. Apply migrations.
9. Deploy to staging.
10. Run smoke tests.
11. Promote to production.

## 6. Smoke Tests

- Frontend loads.
- Backend health endpoint returns healthy.
- Database connection healthy.
- Redis connection healthy.
- Login works.
- Dashboard API works.
- AI provider configuration exists.

## 6.1 Database Migrations

Local development now uses EF Core migrations.

```powershell
dotnet ef migrations add <MigrationName> --project backend\LearnOS.Api\LearnOS.Api.csproj --startup-project backend\LearnOS.Api\LearnOS.Api.csproj
dotnet ef database update --project backend\LearnOS.Api\LearnOS.Api.csproj --startup-project backend\LearnOS.Api\LearnOS.Api.csproj
```

The API also applies pending migrations on startup.

## 7. Rollback

- Keep previous container image.
- Use migration scripts with down/rollback plan.
- Feature-flag risky AI features.
- Roll back frontend and backend together if API contracts changed.

## 8. Monitoring

Track:

- Request latency.
- Error rate.
- AI latency.
- AI token usage.
- Job queue length.
- SQL CPU and slow queries.
- Redis memory.
- Storage growth.
