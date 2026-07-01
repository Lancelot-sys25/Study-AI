# Product Requirement Document: LearnOS AI

## 1. Product Overview

LearnOS AI is an AI-powered learning platform for students, teachers, parents, and administrators. It converts learning materials into actionable study assets, personalizes review and practice, and motivates learners through analytics and gamification.

## 2. Goals

- Help learners turn documents into flashcards, quizzes, notes, and learning plans.
- Provide an AI tutor that can explain, summarize, quiz, and guide.
- Improve retention through spaced repetition and adaptive review.
- Increase engagement through streaks, goals, rewards, and lightweight challenges.
- Give teachers, parents, and admins visibility into progress and risk signals.

## 3. Non-Goals for MVP

- Native mobile apps.
- Full payment and subscription system.
- Voice chat.
- Marketplace for courses.
- Custom ML model training.
- Enterprise SSO.

## 4. Personas

| Persona | Main Need | Primary Features |
|---|---|---|
| Student | Learn faster and remember longer | Dashboard, flashcards, quiz, AI tutor, roadmap |
| Teacher | Track class and create material | Class dashboard, generated quiz, comments |
| Parent | Understand child progress | Weekly reports, alerts, summary |
| Admin | Operate platform safely | User management, reports, AI usage, audit logs |

## 5. MVP Functional Requirements

### Authentication

- Register with email and password.
- Login with JWT access token and refresh token.
- View and update user profile.
- Role-aware navigation.

### Dashboard

- Show study time, streak, completion rate, due flashcards, today quiz, goals, progress chart, heatmap, and AI recommendation.

### Flashcards

- Create, update, delete flashcard decks.
- Generate flashcards from pasted text and uploaded PDF in MVP.
- Store question, answer, hint, explanation, difficulty, tag, source, topic, confidence score.
- Review flashcards and record answer quality.

### Spaced Repetition

- MVP uses SM-2 compatible scheduling.
- Store ease factor, interval, repetition count, due date, last reviewed date.
- Future support for FSRS and AI adjustment.

### Quiz

- Support multiple choice, true/false, fill blank in MVP.
- Generate quiz from note or deck.
- Track score, accuracy, time spent, wrong answers.
- Adjust next difficulty based on performance.

### AI Tutor

- Accept user question and optional context.
- Explain, summarize, generate quiz, generate flashcard, create study plan.
- Save chat history.
- Show disclaimer when generated content needs verification.

### Roadmap and Planner

- Create goal with deadline, current level, available time.
- Generate weekly roadmap.
- Create study sessions and daily goals.

### Analytics

- Student: accuracy, learning time, completion rate, focus time, review history.
- Teacher: class progress, weak students, high performers.
- Admin: active users, storage, AI usage.

## 6. Post-MVP Functional Requirements

- Google and Microsoft OAuth.
- Email verification, OTP, MFA.
- Word, PowerPoint, image OCR, YouTube, website, audio, video ingestion.
- Study groups, shared notes, chat, comments.
- Advanced quiz modes: matching, ordering, drag/drop, image, audio, coding, boss battle.
- Push/email notification.
- Offline mode and sync.
- Gamification shop, league, rank, daily reward.

## 7. Non-Functional Requirements

| Category | Requirement |
|---|---|
| Performance | Dashboard API p95 under 500ms without AI calls |
| Availability | 99.5% MVP target, 99.9% later |
| Security | JWT, refresh token rotation, RBAC, rate limit, audit log |
| Privacy | User documents isolated per account or tenant |
| Accessibility | WCAG 2.1 AA target |
| Scalability | AI processing through background jobs |
| Maintainability | Modular backend with domain services |

## 8. Success Metrics

- 40% of new users complete first study session.
- 30% of active users return next day.
- 70% generated flashcard acceptance rate.
- 15% quiz accuracy improvement after 7 days.
- AI helpfulness average rating above 4/5.

## 9. MVP Release Criteria

- Auth, dashboard, flashcards, quiz, AI tutor, roadmap, planner, analytics are implemented.
- Database migrations run cleanly.
- API documented with OpenAPI.
- Unit and integration tests cover critical services.
- Docker compose starts frontend, backend, database, and cache.

