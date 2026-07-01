# Software Requirement Specification

## 1. System Context

LearnOS AI consists of a React TypeScript frontend, ASP.NET Core Web API backend, SQL Server database, Redis cache, background workers, object storage, OpenAI API, and a future vector database.

## 2. Actors

- Anonymous user
- Student
- Teacher
- Parent
- Admin
- AI provider
- Notification provider
- Storage provider

## 3. Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-001 | User can register and login | Must |
| FR-002 | User can manage profile and learning preferences | Must |
| FR-003 | Student can view personalized dashboard | Must |
| FR-004 | Student can create and review flashcards | Must |
| FR-005 | AI can generate flashcards from text | Must |
| FR-006 | System schedules flashcards using SM-2 | Must |
| FR-007 | Student can take quizzes | Must |
| FR-008 | AI can generate quizzes from content | Should |
| FR-009 | Student can chat with AI tutor | Must |
| FR-010 | System stores AI chat history | Should |
| FR-011 | Student can create goals and roadmap | Must |
| FR-012 | Student can create study sessions | Must |
| FR-013 | Teacher can view class progress | Should |
| FR-014 | Parent can view child summary | Could |
| FR-015 | Admin can view platform metrics | Should |
| FR-016 | System records audit logs for sensitive actions | Must |

## 4. Data Requirements

- All primary records use GUID identifiers.
- Timestamps use UTC.
- Soft delete is applied to user-owned learning content.
- User-generated documents keep metadata and storage reference, not raw binary in SQL.
- AI output stores prompt version, model, token usage, confidence score, and source references where available.

## 5. External Interfaces

### Frontend

- Browser-based SPA.
- Calls backend through JSON REST API.
- Uses bearer access token.

### AI

- Backend calls OpenAI through a provider abstraction.
- AI requests are logged with cost metadata.
- RAG calls include document chunk references.

### Storage

- MVP can use local storage path.
- Production uses Cloudinary or Azure Blob Storage.

## 6. Security Requirements

- Passwords are hashed with a strong adaptive algorithm.
- Access token lifetime should be short.
- Refresh tokens are rotated and revocable.
- Role-based authorization protects teacher, parent, and admin routes.
- API rate limiting protects auth and AI endpoints.
- Audit log records login, role changes, document deletion, export, and admin actions.

## 7. Quality Attributes

- Modular backend layers: API, Application, Domain, Infrastructure.
- Frontend components should be reusable and accessible.
- Background AI processing should be idempotent.
- API errors use consistent problem detail format.

## 8. Constraints

- Backend: ASP.NET Core Web API.
- Database: SQL Server.
- ORM: Entity Framework Core.
- Frontend: React, TypeScript, TailwindCSS-compatible styling.
- Realtime: SignalR.
- Cache: Redis.
- Cloud target: Azure-compatible deployment.

