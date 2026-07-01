# Frontend Architecture

## 1. Stack

- React
- TypeScript
- Vite
- TailwindCSS-compatible CSS strategy
- Recharts
- Lucide icons

## 2. Proposed Structure

```text
src/
  app/
    App.tsx
    routes.tsx
  components/
    layout/
    dashboard/
    flashcards/
    quiz/
    ai/
    planner/
    analytics/
  features/
    auth/
    documents/
    flashcards/
    quiz/
    roadmap/
    planner/
    analytics/
  services/
    apiClient.ts
    authStore.ts
  styles/
```

## 3. Frontend Modules

- Auth pages: login, register, forgot password, MFA.
- Student workspace: dashboard, flashcard review, quiz, AI chat, roadmap.
- Teacher workspace: class dashboard, assignment, student progress.
- Parent workspace: child progress and weekly reports.
- Admin workspace: users, AI usage, storage, reports, audit log.

## 4. State Management

MVP can use React state and context. When complexity grows, use Zustand or TanStack Query.

Recommended:

- Server state: TanStack Query.
- Auth state: small persisted store.
- UI state: component local state.

## 5. API Client

- Attach bearer token.
- Refresh token on 401 once.
- Normalize problem details.
- Support cancellation through AbortController.

## 6. Accessibility

- Keyboard reachable navigation.
- Visible focus states.
- Semantic headings.
- ARIA labels for icon buttons.
- Color contrast AA.

## 7. Current MVP

The current project contains a React single-page prototype in:

- `src/App.tsx`
- `src/App.css`
- `src/index.css`

It demonstrates the target UX for dashboard, flashcards, quiz, roadmap, planner, analytics, and AI tutor chat.

