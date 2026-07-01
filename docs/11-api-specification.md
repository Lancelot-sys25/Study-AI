# API Specification

Base URL: `/api/v1`

## Conventions

- Auth: `Authorization: Bearer <accessToken>`
- Errors: RFC 7807 problem details
- IDs: GUID strings
- Pagination: `page`, `pageSize`

## Auth

| Method | Path | Description |
|---|---|---|
| POST | `/auth/register` | Create account |
| POST | `/auth/login` | Login and issue tokens |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout` | Revoke refresh token |
| POST | `/auth/forgot-password` | Send reset flow |
| POST | `/auth/verify-email` | Verify email token |
| GET | `/me` | Current profile |

## Dashboard

| Method | Path | Description |
|---|---|---|
| GET | `/dashboard/student` | Student dashboard summary |
| GET | `/dashboard/teacher` | Teacher class summary |
| GET | `/dashboard/admin` | Platform summary |

## Documents

| Method | Path | Description |
|---|---|---|
| POST | `/documents` | Upload document |
| GET | `/documents` | List documents |
| GET | `/documents/{id}` | Get metadata |
| DELETE | `/documents/{id}` | Soft delete |
| POST | `/documents/{id}/process` | Start extraction and AI job |

## Flashcards

| Method | Path | Description |
|---|---|---|
| GET | `/decks` | List decks |
| POST | `/decks` | Create deck |
| GET | `/decks/{id}` | Get deck with cards |
| POST | `/decks/{id}/cards` | Create card |
| PUT | `/cards/{id}` | Update card |
| DELETE | `/cards/{id}` | Delete card |
| POST | `/ai/flashcards` | Generate cards from text/document |
| GET | `/reviews/due` | Get due flashcards |
| POST | `/reviews/{cardId}` | Submit review result |

## Quiz

| Method | Path | Description |
|---|---|---|
| GET | `/quizzes` | List quizzes |
| POST | `/quizzes` | Create quiz |
| GET | `/quizzes/{id}` | Get quiz |
| POST | `/ai/quizzes` | Generate quiz |
| POST | `/quizzes/{id}/attempts` | Start attempt |
| POST | `/attempts/{id}/answers` | Submit answer |
| POST | `/attempts/{id}/complete` | Complete attempt |

## AI Tutor

| Method | Path | Description |
|---|---|---|
| POST | `/ai/chat` | Ask AI tutor |
| GET | `/ai/conversations` | List conversations |
| GET | `/ai/conversations/{id}` | Get messages |
| POST | `/ai/roadmap` | Generate roadmap |
| POST | `/ai/summary` | Generate summary |

## Planner

| Method | Path | Description |
|---|---|---|
| GET | `/goals` | List study goals |
| POST | `/goals` | Create goal |
| GET | `/goals/{id}/roadmap` | Get roadmap |
| POST | `/sessions` | Create study session |
| GET | `/sessions` | List study sessions |

## Analytics

| Method | Path | Description |
|---|---|---|
| GET | `/analytics/student` | Student metrics |
| GET | `/analytics/student/reviews` | Review history |
| GET | `/analytics/teacher/classes/{id}` | Class analytics |
| GET | `/analytics/admin/usage` | AI/storage/user usage |

## Example: Generate Flashcards

Request:

```json
{
  "sourceType": "Text",
  "title": "React Hooks",
  "content": "useState stores state in function components...",
  "targetCount": 12,
  "difficulty": "Mixed"
}
```

Response:

```json
{
  "deckId": "7b7b7556-7a61-45b7-90f0-7366e49822c4",
  "cards": [
    {
      "id": "6b226a4a-9b2f-47a8-a7c1-93f9350a53d0",
      "question": "What does useState do?",
      "answer": "It stores local state in a function component.",
      "hint": "Think component memory.",
      "difficulty": "Easy",
      "confidenceScore": 94.5
    }
  ]
}
```

