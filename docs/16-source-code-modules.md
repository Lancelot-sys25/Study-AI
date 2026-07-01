# Source Code Modules

## Current Implemented Frontend MVP

Implemented:

- `src/App.tsx`: single-page smart learning cockpit.
- `src/App.css`: responsive product UI.
- `src/index.css`: global reset and accessibility focus.

Features currently visible:

- Dashboard metrics.
- AI recommendation hero.
- Weekly chart.
- Study heatmap.
- Subject progress.
- Flashcard studio.
- Quiz engine preview.
- Roadmap.
- Planner.
- Analytics.
- AI tutor mock chat.

## Backend Modules to Implement

Implemented backend:

- `backend/LearnOS.slnx`: .NET solution.
- `backend/LearnOS.Api/LearnOS.Api.csproj`: ASP.NET Core Web API project.
- `backend/LearnOS.Api/Program.cs`: EF Core API with SQL Server LocalDB persistence for health, student dashboard, flashcards, reviews, quiz attempts, AI chat, roadmap, and analytics.
- `backend/LearnOS.Api/appsettings.json`: LocalDB connection string for `LearnOSAI`.

Current backend endpoints:

- `GET /api/v1/health`
- `GET /api/v1/dashboard/student`
- `GET /api/v1/flashcards`
- `POST /api/v1/flashcards`
- `POST /api/v1/flashcards/generate`
- `POST /api/v1/reviews/{cardId}`
- `GET /api/v1/quizzes/today`
- `POST /api/v1/quiz-attempts`
- `POST /api/v1/ai/chat`
- `GET /api/v1/ai/conversations/{conversationId}/messages`
- `POST /api/v1/roadmaps`
- `GET /api/v1/roadmaps`
- `GET /api/v1/analytics/study-series`

Next backend extraction should split `Program.cs` into controllers or endpoint modules plus Application/Domain/Infrastructure projects and replace `EnsureCreated` with EF Core migrations.

| Module | Main Files |
|---|---|
| Auth | `AuthController`, `AuthService`, `JwtTokenService`, `RefreshTokenRepository` |
| Users | `UsersController`, `UserService`, `LearningProfileService` |
| Documents | `DocumentsController`, `DocumentService`, `TextExtractionService` |
| Flashcards | `DecksController`, `FlashcardService`, `SpacedRepetitionService` |
| Quiz | `QuizzesController`, `QuizService`, `QuizScoringService` |
| AI | `AiController`, `AiTutorService`, `AiGenerationService`, `OpenAiProvider` |
| Planner | `GoalsController`, `StudySessionsController`, `RoadmapService` |
| Analytics | `AnalyticsController`, `AnalyticsService` |
| Gamification | `GamificationService`, `BadgeService`, `RewardService` |
| Admin | `AdminController`, `AuditLogService`, `ReportService` |

## Frontend Modules to Split Next

| Module | Components |
|---|---|
| Layout | `Sidebar`, `Topbar`, `AppShell` |
| Dashboard | `MetricGrid`, `LearningCurve`, `StudyHeatmap`, `RecommendationBanner` |
| Flashcards | `DeckList`, `FlashcardViewer`, `ReviewControls`, `GenerateCardsForm` |
| Quiz | `QuizModePicker`, `QuestionRenderer`, `AttemptSummary` |
| AI | `AiChatDock`, `AiPromptComposer`, `SourceCitationList` |
| Planner | `GoalForm`, `RoadmapTimeline`, `SessionCalendar`, `PomodoroTimer` |
| Analytics | `AccuracyChart`, `RetentionChart`, `WeakTopicList` |

## Implementation Rule

Each module should include:

- DTO/request/response contracts.
- Domain or UI model.
- Service function.
- Unit tests for business logic.
- Integration/API test for backend endpoints.
