# LearnOS AI Project Status

## Current Rule

Work module by module. After each module is completed and verified, stop and ask the user to confirm before continuing.

## Current Stack

- Frontend: React, TypeScript, Vite
- Backend: ASP.NET Core Web API
- Database: SQL Server LocalDB through EF Core
- Dev database: `LearnOSAI_Migrated`
- Frontend URL: `http://127.0.0.1:5173`
- Backend URL: `http://127.0.0.1:5000`

## Completed

- Frontend MVP UI
- Backend API scaffold
- SQL Server LocalDB persistence
- Dashboard from real DB data
- Flashcard creation/generation from user text saved to DB
- Quiz generated from saved flashcards
- AI chat saved to DB with local rule-based answer
- Roadmap saved to DB
- Analytics from study sessions and quiz attempts
- Documentation files `docs/01` to `docs/18`

## Completed Auth Module

- Register endpoint
- Login endpoint
- JWT access token
- Refresh token storage and rotation
- Logout by refresh token revocation
- Protected `/api/v1/me` endpoint
- PBKDF2 password hashing
- SQL Server tables: `Users`, `RefreshTokens`

## Completed Role Authorization Module

- Backend authorization policies:
  - `StudentOrTeacher`: Student, Teacher, Admin
  - `TeacherOnly`: Teacher, Admin
  - `AdminOnly`: Admin
- Protected student learning endpoints with bearer token.
- Added protected teacher dashboard route.
- Added protected admin dashboard route.
- Frontend login/register screen.
- Frontend stores JWT/refresh token in `localStorage`.
- Frontend API helper attaches `Authorization: Bearer <token>`.
- Frontend logout revokes refresh token.

## Completed EF Core Migrations Module

- Added `Microsoft.EntityFrameworkCore.Design`.
- Created initial migration:
  - `backend/LearnOS.Api/Migrations/20260701040337_InitialCreate.cs`
  - `backend/LearnOS.Api/Migrations/LearnOsDbContextModelSnapshot.cs`
- Switched startup from `EnsureCreated` and manual schema bootstrap to `Database.MigrateAsync()`.
- Applied migration to SQL Server LocalDB database `LearnOSAI_Migrated`.
- Previous dev database `LearnOSAI` was not deleted.

## Completed Flashcard CRUD and SM-2 Module

- Added flashcard SM-2 fields:
  - `Repetition`
  - `EaseFactor`
  - `IntervalDays`
  - `ForgetCount`
  - `LastReviewedAt`
- Added migrations:
  - `AddFlashcardSm2Fields`
  - `SetFlashcardEaseFactorDefault`
- Added backend endpoints:
  - `GET /api/v1/flashcards/due`
  - `GET /api/v1/flashcards/{cardId}`
  - `PUT /api/v1/flashcards/{cardId}`
  - `DELETE /api/v1/flashcards/{cardId}`
- Updated review endpoint to apply SM-2 scheduling.
- Frontend flashcard screen now shows due date, repetition, ease, interval, and review count.
- Frontend flashcard screen now has Again, Hard, Good, Easy, and Delete actions.

## Completed OpenAI Integration Module

- Added `OpenAiLearningService`.
- Registered typed `HttpClient` for OpenAI calls.
- Added OpenAI configuration:
  - `OpenAI:ApiKey`
  - `OpenAI:Model`
  - `OpenAI:BaseUrl`
- AI tutor endpoint now calls OpenAI Responses API when `OPENAI_API_KEY` is configured.
- Flashcard generation endpoint now calls OpenAI Responses API and parses JSON flashcards.
- Removed silent mock fallback for AI calls.
- If no API key is configured, AI endpoints return `503 Service Unavailable` with a clear configuration message.

## Completed Document Upload Module

- Added `LearningDocument` entity and migration `AddLearningDocuments`.
- Added local file storage under `backend/LearnOS.Api/App_Data/uploads`.
- Added backend endpoints:
  - `GET /api/v1/documents`
  - `GET /api/v1/documents/{documentId}`
  - `POST /api/v1/documents`
  - `POST /api/v1/documents/{documentId}/flashcards`
- TXT and Markdown files extract text immediately.
- PDF files upload and extract text for text-based PDFs.
- Frontend has a Documents tab with upload UI and saved document list.
- Verified TXT upload with real multipart request and DB-backed document list.

## Completed PDF Text Extraction

- Added `UglyToad.PdfPig` package.
- Added PDF text extraction during document upload.
- Verified text-based PDF upload returns `TextExtracted` and non-zero `textLength`.

## Completed Quiz Engine Expansion

- Added `Quiz` and `QuizQuestion` entities.
- Added migration `AddQuizEngine`.
- Added backend endpoints:
  - `GET /api/v1/quizzes`
  - `GET /api/v1/quizzes/{quizId}`
  - `POST /api/v1/quizzes/generate-from-flashcards`
- Expanded `POST /api/v1/quiz-attempts` to score submitted answers.
- Supported basic question types:
  - Multiple choice
  - True/false
  - Fill blank / typing
- Frontend quiz tab can generate quiz from flashcards, answer questions, and submit.
- Verified API quiz flow with 4 generated questions and 100% scored attempt.

## Completed Gamification Module

- Added `GamificationProfile` entity and migration `AddGamification`.
- Added backend endpoint:
  - `GET /api/v1/gamification/me`
- Awards XP, coins, energy changes, streak, and league progression on:
  - flashcard review
  - quiz attempt completion
- Frontend dashboard shows XP, energy, coins, and league.
- Verified reward flow after flashcard review:
  - XP increased
  - coins increased
  - energy decreased
  - streak updated

## Completed Teacher/Admin Dashboard Module

- Teacher dashboard endpoint now returns real aggregate metrics:
  - total attempts
  - active learners proxy
  - students at risk
  - average accuracy
  - weak topics
- Admin dashboard endpoint returns platform usage counts.
- Frontend shows Teacher dashboard panel for Teacher/Admin roles.
- Frontend shows Admin dashboard panel for Admin role.
- Verified protected Teacher/Admin endpoints by role.

## Completed Smoke Tests Module

- Added `scripts/smoke-test.ps1`.
- Smoke test covers:
  - register
  - `/me`
  - create flashcard
  - SM-2 review
  - generate quiz
  - submit quiz attempt
  - gamification reward
- Verified script passes against local backend.

## Completed Docker/Deployment Module

- Added backend Dockerfile.
- Added frontend Dockerfile.
- Added nginx config for SPA fallback.
- Added `docker-compose.yml` with SQL Server, ASP.NET Core API, and React/nginx frontend.

## Completed Owner-Scoped Data Isolation Module

- Added `OwnerId`/`UserId` fields and EF Core indexes for user-owned learning data.
- Added migration `AddOwnerScopedLearningData`.
- Scoped dashboard, flashcards, documents, quizzes, quiz attempts, AI conversations, analytics, subjects, study sessions, and roadmaps to the authenticated user.
- Verified with two real users that user B cannot see a flashcard created by user A.

## Completed Collaboration Module

- Added `CollaborationRoom`, `CollaborationRoomMember`, and `CollaborationMessage` entities.
- Added migration `AddCollaborationRooms`.
- Added backend endpoints for room list, room creation, join by code, message list, and message creation.
- Enforced member-only access for room messages.
- Added frontend Collaboration tab with room create/join, room list, join code display, and persisted message thread.
- Added collaboration coverage to `scripts/smoke-test.ps1`.
- Verified with two real users that a member can join by code, send a message, and the room owner can read it.

## Completed Notification/Reminder MVP

- Added `LearningReminder` entity and migration `AddLearningReminders`.
- Added backend endpoints for reminder list, creation, completion, and deletion.
- Scoped reminders to the authenticated user.
- Replaced the static Planner placeholder with a real reminder planner UI.
- Added an in-app notification badge for pending reminders.
- Added reminder coverage to `scripts/smoke-test.ps1`.
- Verified create/list/complete flow and cross-user isolation with real API calls.

## Completed Offline/PWA MVP

- Added web app manifest at `public/manifest.webmanifest`.
- Added service worker at `public/service-worker.js`.
- Registered the service worker from `src/main.tsx`.
- Added offline status handling in the React app.
- Added local queued reminder creation while offline.
- Added automatic reminder sync when the browser comes back online.
- Verified production build includes `manifest.webmanifest` and `service-worker.js`.

## Completed OCR/Audio/Video Ingestion MVP

- Expanded document upload support to PNG, JPG, JPEG, WEBP, MP3, WAV, M4A, MP4, and WEBM.
- Added OpenAI vision OCR for image uploads through the Responses API.
- Added OpenAI audio/video transcription through `/audio/transcriptions`.
- Added `OpenAI:TranscriptionModel` support with default `gpt-4o-mini-transcribe`.
- Kept the no-mock behavior: OCR/transcription returns `503 Service Unavailable` when `OPENAI_API_KEY` is not configured.
- Updated the Documents tab file picker and copy for OCR/transcription sources.
- Verified no-key image OCR upload returns 503 and the full smoke test still passes.

## Completed YouTube Transcript Ingestion MVP

- Added `YoutubeExplode` package.
- Added `POST /api/v1/documents/youtube`.
- Fetches real YouTube metadata and public captions/transcripts.
- Saves extracted transcript as a `LearningDocument` scoped to the authenticated user.
- Added frontend Documents form for YouTube URL and caption language.
- Returns clear errors when a video has no accessible captions instead of generating fake transcript content.
- Verified with a public YouTube URL that a transcript document was created with 20,988 characters.

## Completed External Notification Provider MVP

- Added SMTP-backed `SmtpNotificationService`.
- Added `POST /api/v1/notifications/reminders/{reminderId}/send-email`.
- Sends reminder email to the authenticated user's email when SMTP is configured.
- Returns `503 Service Unavailable` when SMTP settings are missing instead of pretending to send.
- Added Planner UI action for email notification delivery.
- Verified no-config SMTP send returns 503 and full smoke test still passes.

## Completed CI Hardening MVP

- Added GitHub Actions workflow `.github/workflows/ci.yml`.
- CI installs frontend dependencies, runs `npm run lint`, runs `npm run build`, restores backend, and builds backend in Release mode.
- Verified local Release backend build passes.

## Completed Auth Hardening MVP

- Added `AuthActionToken` entity and migration `AddAuthHardening`.
- Added email verification request/confirm endpoints with hashed persisted tokens.
- Added forgot password and reset password endpoints with session revocation after reset.
- Added MFA setup/enable/disable using TOTP codes.
- Login now returns `409` with `mfaRequired` when MFA is enabled and no code is supplied.
- Added authenticated session list and per-session revoke endpoints.
- Expanded smoke test coverage for email verification, password reset, sessions, and MFA login.
- Verified full smoke test passes after migration.

## Completed Parent/Guardian MVP

- Added `ParentOnly` authorization policy.
- Added `GuardianInvitation` and `GuardianLink` entities with migration `AddGuardianLinks`.
- Students can create guardian invite codes.
- Parent accounts can link to a student by invite code.
- Parents can list linked students and view a scoped student progress dashboard.
- Added frontend guardian invite panel for students and parent dashboard/link panel for parents.
- Expanded smoke test coverage for guardian invite, link, student list, and parent dashboard.
- Verified full smoke test and Release backend build pass.

## Completed Classroom/Course/Assignment API MVP

- Added `Course`, `CourseEnrollment`, `Assignment`, and `AssignmentSubmission` entities.
- Added migration `AddClassroomCourses`.
- Teachers/Admins can create courses with join codes.
- Students can join courses by code.
- Teachers can create assignments for owned courses.
- Students can list assignments for joined courses and submit work.
- Teachers can list submissions for their courses.
- Added frontend Classroom tab for course creation, joining by code, assignment creation, student submission, and teacher submission review.
- Expanded smoke test coverage for course creation, join, assignment creation, submission, and teacher submission review.
- Verified full smoke test and Release backend build pass.

## Next Planned Modules

Most large prompt modules now have a working MVP implementation. Remaining future-hardening work:

1. Google/Microsoft OAuth login.
2. Grading/rubrics and assignment feedback.
3. Broader offline sync for flashcards, quizzes, and documents.
4. Realtime collaboration with SignalR.
5. Push notifications/mobile delivery.
6. More formal unit/integration/UI/security/performance test suites.
7. Production secrets, monitoring, and Azure deployment.

## Verification Commands

```powershell
npm run lint
npm run build
dotnet build backend\LearnOS.slnx
dotnet build backend\LearnOS.slnx --configuration Release
powershell -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

## Resume Instruction

When returning to this project, read this file first, then inspect changed source files if needed.
