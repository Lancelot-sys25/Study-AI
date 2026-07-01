# Backend Architecture

## 1. Stack

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- Redis
- SignalR
- OpenAI API
- Background workers
- Azure Blob Storage or Cloudinary

## 2. Solution Structure

```text
backend/
  LearnOS.Api/
  LearnOS.Application/
  LearnOS.Domain/
  LearnOS.Infrastructure/
  LearnOS.Tests/
```

## 3. Layer Responsibilities

| Layer | Responsibility |
|---|---|
| Api | Controllers/endpoints, auth middleware, OpenAPI, request validation |
| Application | Use cases, DTOs, interfaces, orchestration |
| Domain | Entities, value objects, domain rules |
| Infrastructure | EF Core, Redis, OpenAI client, storage, email |
| Tests | Unit, integration, API tests |

## 4. Key Services

- `AuthService`: register, login, refresh, logout.
- `DashboardService`: aggregates student/teacher/admin metrics.
- `DocumentService`: upload, extract text, chunk content.
- `FlashcardService`: CRUD, review, dedup.
- `SpacedRepetitionService`: SM-2 now, FSRS later.
- `QuizService`: quiz CRUD, attempt scoring, adaptive difficulty.
- `AiTutorService`: chat, summary, explanations.
- `RoadmapService`: goals and roadmap generation.
- `GamificationService`: XP, streak, badges, rewards.
- `AuditLogService`: immutable security logs.

## 5. AI Processing Pattern

Long AI tasks should not block request threads.

1. API receives request.
2. API validates ownership and creates job.
3. Worker processes extraction and AI generation.
4. Worker saves result and usage.
5. Frontend polls job or receives SignalR update.

## 6. Security

- JWT access tokens.
- Refresh token rotation.
- RBAC policies: Student, Teacher, Parent, Admin.
- Rate limit auth and AI endpoints.
- Audit sensitive actions.
- Store secrets in environment variables or Azure Key Vault.

## 7. Observability

- Structured logs.
- Request correlation ID.
- AI usage logs.
- Health checks for SQL Server, Redis, storage, AI provider.
- Metrics: request latency, error rate, job duration, AI cost.

