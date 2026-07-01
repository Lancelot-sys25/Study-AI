# Test Cases

## Authentication

| ID | Case | Expected Result |
|---|---|---|
| TC-AUTH-001 | Register with valid email/password | User created and token returned |
| TC-AUTH-002 | Register duplicate email | 409 conflict |
| TC-AUTH-003 | Login invalid password | 401 unauthorized |
| TC-AUTH-004 | Refresh token rotation | New tokens issued, old refresh token revoked |
| TC-AUTH-005 | Access admin endpoint as student | 403 forbidden |

## Flashcards

| ID | Case | Expected Result |
|---|---|---|
| TC-FC-001 | Create deck | Deck appears in list |
| TC-FC-002 | Create flashcard with missing question | Validation error |
| TC-FC-003 | Generate flashcards from text | Cards saved with confidence score |
| TC-FC-004 | Submit review rating Good | Due date moves forward |
| TC-FC-005 | Submit review rating Again | Interval resets and forget count increments |

## Quiz

| ID | Case | Expected Result |
|---|---|---|
| TC-QZ-001 | Start quiz attempt | Attempt is created |
| TC-QZ-002 | Submit correct answer | Score increases |
| TC-QZ-003 | Submit wrong answer | Explanation is returned |
| TC-QZ-004 | Complete attempt | Accuracy and duration saved |
| TC-QZ-005 | Low accuracy quiz | Next recommendation lowers difficulty |

## AI

| ID | Case | Expected Result |
|---|---|---|
| TC-AI-001 | Ask tutor normal question | Answer saved in conversation |
| TC-AI-002 | Generate malformed AI JSON | System repairs or fails gracefully |
| TC-AI-003 | AI provider timeout | Job marked failed and user can retry |
| TC-AI-004 | RAG answer with document context | Citations returned |
| TC-AI-005 | Exceed AI quota | 429 or quota-specific error |

## Dashboard and Analytics

| ID | Case | Expected Result |
|---|---|---|
| TC-AN-001 | New user dashboard | Empty state with first action |
| TC-AN-002 | User completes study session | Study time updates |
| TC-AN-003 | User reviews cards daily | Streak increments |
| TC-AN-004 | Miss one day | Streak resets or freezes by policy |
| TC-AN-005 | Teacher views class | Aggregated student metrics shown |

## Security

| ID | Case | Expected Result |
|---|---|---|
| TC-SEC-001 | Access another user's deck | 404 or 403 |
| TC-SEC-002 | Upload unsupported file | Validation error |
| TC-SEC-003 | Excessive login attempts | Rate limit |
| TC-SEC-004 | Admin role change | Audit log created |
| TC-SEC-005 | Deleted document retrieval | Not returned to normal list |

## Performance

| ID | Case | Expected Result |
|---|---|---|
| TC-PERF-001 | Dashboard under normal load | p95 below 500ms without AI |
| TC-PERF-002 | Generate 100 cards job | Job completes asynchronously |
| TC-PERF-003 | List 1000 cards paginated | Response remains bounded |

## Requirement Traceability Matrix

| Requirement | Test Cases |
|---|---|
| FR-001 Auth | TC-AUTH-001 to TC-AUTH-005 |
| FR-004 Flashcards | TC-FC-001 to TC-FC-005 |
| FR-007 Quiz | TC-QZ-001 to TC-QZ-005 |
| FR-009 AI Tutor | TC-AI-001 to TC-AI-005 |
| FR-012 Planner/Analytics | TC-AN-001 to TC-AN-005 |
| Security | TC-SEC-001 to TC-SEC-005 |

