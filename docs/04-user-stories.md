# User Stories

## Student

| ID | Story | Acceptance Criteria |
|---|---|---|
| US-S-001 | As a student, I want to register and login so I can save my learning data. | Account is created, token is returned, dashboard is accessible. |
| US-S-002 | As a student, I want to upload learning material so AI can create study assets. | File is stored, processing status is visible, generated assets are linked to source. |
| US-S-003 | As a student, I want AI-generated flashcards so I can review faster. | Cards include question, answer, hint, difficulty, tag, confidence. |
| US-S-004 | As a student, I want flashcards scheduled automatically so I know what to review today. | Due cards appear on dashboard; review updates next due date. |
| US-S-005 | As a student, I want adaptive quizzes so practice matches my level. | Quiz difficulty changes after weak or strong performance. |
| US-S-006 | As a student, I want an AI tutor so I can ask for explanations. | AI response is saved and may cite source documents. |
| US-S-007 | As a student, I want a roadmap so I can follow a clear plan. | Roadmap is generated from goal, deadline, level, free time. |
| US-S-008 | As a student, I want gamification so I stay motivated. | XP, streak, badge, and daily reward are updated after learning actions. |

## Teacher

| ID | Story | Acceptance Criteria |
|---|---|---|
| US-T-001 | As a teacher, I want to create a class so I can group students. | Class has name, subject, members, and invitation code. |
| US-T-002 | As a teacher, I want to assign quizzes so I can evaluate progress. | Assignment has due date, target students, and completion status. |
| US-T-003 | As a teacher, I want to see weak students so I can intervene. | Dashboard lists low accuracy, low activity, and missed deadlines. |
| US-T-004 | As a teacher, I want to comment on student work. | Student receives comment notification. |

## Parent

| ID | Story | Acceptance Criteria |
|---|---|---|
| US-P-001 | As a parent, I want weekly summaries so I can understand progress. | Summary includes time, streak, completion, weak topics. |
| US-P-002 | As a parent, I want alerts when my child is at risk of quitting. | Alert is sent after configured inactivity or decline pattern. |

## Admin

| ID | Story | Acceptance Criteria |
|---|---|---|
| US-A-001 | As an admin, I want user management so I can operate the platform. | Admin can search, deactivate, and change role with audit log. |
| US-A-002 | As an admin, I want AI usage metrics so I can control cost. | Dashboard shows tokens, calls, model usage, and cost estimate. |
| US-A-003 | As an admin, I want reports so unsafe content can be reviewed. | Report status can be opened, resolved, or escalated. |

