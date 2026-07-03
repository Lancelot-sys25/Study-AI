# LearnOS AI

LearnOS AI is a full-stack smart learning platform prototype built around personalized study, AI-assisted content generation, spaced repetition, quizzes, gamification, classroom workflows, parent visibility, collaboration, reminders, and document ingestion.

The project is designed as a commercial-grade learning product foundation inspired by tools such as Quizlet, Duolingo, Khan Academy, Coursera, and Notion AI.

## Highlights

- Next.js, React, and TypeScript frontend with dashboard, flashcards, documents, quiz, roadmap, classroom, collaboration, planner, analytics, parent, teacher, and admin views.
- ASP.NET Core Web API backend with EF Core and SQL Server LocalDB persistence.
- JWT authentication with refresh tokens, logout, session management, email verification, password reset, and TOTP MFA.
- Role-based authorization for Student, Teacher, Parent, and Admin flows.
- Owner-scoped data isolation across learning records, documents, AI conversations, quizzes, analytics, reminders, and roadmaps.
- Flashcard CRUD with SM-2 scheduling and real review state.
- OpenAI-backed AI tutor, flashcard generation, image OCR, and audio/video transcription when `OPENAI_API_KEY` is configured.
- Document ingestion for TXT, Markdown, PDF, images, audio, video, and YouTube transcripts.
- Quiz engine with persisted quizzes, questions, scored attempts, and gamification rewards.
- Parent/guardian linking with invite codes and scoped student progress dashboards.
- Classroom workflow with courses, join codes, assignments, rubrics, submissions, grading, feedback, and teacher review.
- Collaboration rooms with join codes, persisted member-only messages, and SignalR realtime delivery.
- Planner reminders with in-app notification badge, SMTP email delivery, and offline queue sync.
- PWA/offline MVP with manifest, service worker, offline banner, queued reminders, manual flashcards, flashcard reviews, and quiz attempt sync.
- Docker Compose and GitHub Actions CI.

## Architecture

```text
Next.js + React frontend
        |
        | HTTP + JWT bearer auth
        v
ASP.NET Core Web API
        |
        | EF Core migrations
        v
SQL Server LocalDB / SQL Server
        |
        +-- OpenAI Responses API
        +-- OpenAI Audio Transcriptions API
        +-- YouTube public captions via YoutubeExplode
        +-- SMTP provider for reminder email
```

## Tech Stack

| Layer | Technology |
| --- | --- |
| Frontend | Next.js 16, React 19, TypeScript, Recharts, Lucide React |
| Backend | ASP.NET Core, EF Core, JWT Bearer Auth |
| Database | SQL Server LocalDB by default |
| AI | OpenAI Responses API and Audio Transcriptions API |
| Ingestion | PdfPig, YoutubeExplode, multipart uploads |
| DevOps | Docker, Docker Compose, GitHub Actions |
| Testing | PowerShell smoke tests, frontend lint/build, backend build |

## Getting Started

### Prerequisites

- Node.js 24+
- .NET 10 SDK
- SQL Server LocalDB or SQL Server
- Optional: Docker Desktop
- Optional: `OPENAI_API_KEY` for AI generation, OCR, and transcription

### Install frontend dependencies

```powershell
npm install
```

### Run the backend

```powershell
dotnet run --project backend\LearnOS.Api\LearnOS.Api.csproj --urls http://localhost:5000
```

Health check:

```text
http://localhost:5000/api/v1/health
```

The backend applies EF Core migrations on startup through `Database.MigrateAsync()`.

Default development database:

```text
Server=(localdb)\MSSQLLocalDB;Database=LearnOSAI_Migrated;Trusted_Connection=True;TrustServerCertificate=True
```

### Run the frontend

```powershell
npm run dev
```

Frontend URL:

```text
http://localhost:3000
```

## Configuration

### OpenAI

Set an API key before using AI tutor, AI flashcard generation, image OCR, or audio/video transcription.

```powershell
$env:OPENAI_API_KEY="your_api_key_here"
```

Optional app settings:

```json
{
  "OpenAI": {
    "Model": "gpt-5",
    "TranscriptionModel": "gpt-4o-mini-transcribe",
    "BaseUrl": "https://api.openai.com/v1"
  }
}
```

Without a key, AI endpoints and OCR/transcription uploads return `503 Service Unavailable` instead of mock data.

### SMTP

SMTP reminder email delivery is optional. If SMTP is not configured, email send endpoints return `503 Service Unavailable`.

```json
{
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "smtp_user",
    "Password": "smtp_password",
    "From": "noreply@example.com"
  }
}
```

## Verification

Run the main verification commands before opening a pull request or deployment branch.

```powershell
npm run lint
npm run build
dotnet build backend\LearnOS.slnx
dotnet build backend\LearnOS.slnx --configuration Release
powershell -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

The smoke test covers authentication hardening, SignalR negotiation, flashcards, SM-2 review, quiz attempts, gamification, collaboration, reminders, parent linking, classroom assignments, submissions, rubrics, and grading.

## Docker

```powershell
docker compose up --build
```

Services:

- Frontend: `http://localhost:3000`
- Backend: `http://localhost:5000`
- SQL Server: `localhost,1433`

## Documentation

Product and engineering documentation lives in `docs/`.

1. Product Vision
2. PRD
3. SRS
4. User Stories
5. Use Case Diagram
6. Activity Diagram
7. Sequence Diagram
8. Class Diagram
9. ERD
10. Database Script
11. API Specification
12. Backend Architecture
13. Frontend Architecture
14. AI Architecture
15. UI Wireframe
16. Source Code Modules
17. Test Cases
18. Deployment Guide

Project implementation status is tracked in `PROJECT_STATUS.md`.

## API Surface

### Authentication

- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `POST /api/v1/auth/email-verification/request`
- `POST /api/v1/auth/email-verification/confirm`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`
- `GET /api/v1/auth/sessions`
- `DELETE /api/v1/auth/sessions/{sessionId}`
- `POST /api/v1/auth/mfa/setup`
- `POST /api/v1/auth/mfa/enable`
- `POST /api/v1/auth/mfa/disable`
- `GET /api/v1/me`

### Dashboards and Analytics

- `GET /api/v1/dashboard/student`
- `GET /api/v1/dashboard/teacher`
- `GET /api/v1/dashboard/admin`
- `GET /api/v1/analytics/study-series`

### Parent and Classroom

- `POST /api/v1/parent/invitations`
- `POST /api/v1/parent/links`
- `GET /api/v1/parent/students`
- `GET /api/v1/parent/students/{studentId}/dashboard`
- `GET /api/v1/courses`
- `POST /api/v1/courses`
- `POST /api/v1/courses/join`
- `GET /api/v1/courses/{courseId}/assignments`
- `POST /api/v1/courses/{courseId}/assignments`
- `POST /api/v1/assignments/{assignmentId}/submissions`
- `GET /api/v1/courses/{courseId}/submissions`
- `PUT /api/v1/assignments/{assignmentId}/submissions/{submissionId}/grade`

### Learning

- `GET /api/v1/gamification/me`
- `GET /api/v1/flashcards`
- `GET /api/v1/flashcards/due`
- `GET /api/v1/flashcards/{cardId}`
- `POST /api/v1/flashcards`
- `PUT /api/v1/flashcards/{cardId}`
- `DELETE /api/v1/flashcards/{cardId}`
- `POST /api/v1/flashcards/generate`
- `POST /api/v1/reviews/{cardId}`
- `GET /api/v1/quizzes/today`
- `GET /api/v1/quizzes`
- `GET /api/v1/quizzes/{quizId}`
- `POST /api/v1/quizzes/generate-from-flashcards`
- `POST /api/v1/quiz-attempts`
- `POST /api/v1/roadmaps`
- `GET /api/v1/roadmaps`

### Documents and AI

- `GET /api/v1/documents`
- `GET /api/v1/documents/{documentId}`
- `POST /api/v1/documents`
- `POST /api/v1/documents/youtube`
- `POST /api/v1/documents/{documentId}/flashcards`
- `POST /api/v1/ai/chat`
- `GET /api/v1/ai/conversations/{conversationId}/messages`

### Collaboration and Notifications

- `SignalR /hubs/collaboration`
- `GET /api/v1/collaboration/rooms`
- `POST /api/v1/collaboration/rooms`
- `POST /api/v1/collaboration/rooms/join`
- `GET /api/v1/collaboration/rooms/{roomId}/messages`
- `POST /api/v1/collaboration/rooms/{roomId}/messages`
- `GET /api/v1/notifications/reminders`
- `POST /api/v1/notifications/reminders`
- `PUT /api/v1/notifications/reminders/{reminderId}/complete`
- `POST /api/v1/notifications/reminders/{reminderId}/send-email`
- `DELETE /api/v1/notifications/reminders/{reminderId}`

## Current Roadmap

- Google and Microsoft OAuth login.
- Offline document upload sync and richer conflict handling.
- Push/mobile notification delivery.
- Deeper unit, integration, UI, security, and performance test suites.
- Production secrets, monitoring, and Azure deployment.

## License

This repository is currently prepared as a private project prototype. Add a license before publishing it as an open-source project.
