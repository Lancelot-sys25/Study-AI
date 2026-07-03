param(
    [string]$BaseUrl = "http://127.0.0.1:5000/api/v1"
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$registerBody = @{
    email = "smoke-$stamp@learnos.local"
    password = "Password123!"
    displayName = "Smoke Test"
    role = "Student"
} | ConvertTo-Json

$auth = Invoke-RestMethod -Uri "$BaseUrl/auth/register" -Method Post -ContentType "application/json" -Body $registerBody
Assert-True ([bool]$auth.accessToken) "register should return access token"
$headers = @{ Authorization = "Bearer $($auth.accessToken)" }

$me = Invoke-RestMethod -Uri "$BaseUrl/me" -Headers $headers
Assert-True ($me.email -eq "smoke-$stamp@learnos.local") "me should return registered user"

$verification = Invoke-RestMethod -Uri "$BaseUrl/auth/email-verification/request" -Method Post -ContentType "application/json" -Body (@{ email = "smoke-$stamp@learnos.local" } | ConvertTo-Json)
Assert-True ([bool]$verification.verificationToken) "email verification request should generate token in dev mode"
$verified = Invoke-RestMethod -Uri "$BaseUrl/auth/email-verification/confirm" -Method Post -ContentType "application/json" -Body (@{ token = $verification.verificationToken } | ConvertTo-Json)
Assert-True ($verified.status -eq "Email verified.") "email verification should confirm token"

$forgot = Invoke-RestMethod -Uri "$BaseUrl/auth/forgot-password" -Method Post -ContentType "application/json" -Body (@{ email = "smoke-$stamp@learnos.local" } | ConvertTo-Json)
Assert-True ([bool]$forgot.resetToken) "forgot password should generate reset token in dev mode"
$resetPassword = "Password456!"
$reset = Invoke-RestMethod -Uri "$BaseUrl/auth/reset-password" -Method Post -ContentType "application/json" -Body (@{ token = $forgot.resetToken; newPassword = $resetPassword } | ConvertTo-Json)
Assert-True ($reset.status.StartsWith("Password reset")) "reset password should succeed"
$auth = Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = "smoke-$stamp@learnos.local"; password = $resetPassword } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($auth.accessToken)" }

$sessions = Invoke-RestMethod -Uri "$BaseUrl/auth/sessions" -Headers $headers
Assert-True (@($sessions).Count -ge 1) "sessions endpoint should list refresh token sessions"

$mfa = Invoke-RestMethod -Uri "$BaseUrl/auth/mfa/setup" -Method Post -Headers $headers
Assert-True ([bool]$mfa.secret) "mfa setup should return secret"
$mfaEnabled = Invoke-RestMethod -Uri "$BaseUrl/auth/mfa/enable" -Method Post -ContentType "application/json" -Headers $headers -Body (@{ code = $mfa.currentCode } | ConvertTo-Json)
Assert-True ($mfaEnabled.status -eq "MFA enabled.") "mfa enable should accept current TOTP code"
try {
    Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = "smoke-$stamp@learnos.local"; password = $resetPassword } | ConvertTo-Json) | Out-Null
    throw "Login without MFA code should fail"
} catch {
    if ($_.Exception.Response) {
        Assert-True ([int]$_.Exception.Response.StatusCode -eq 409) "mfa login without code should return 409"
    } else {
        throw
    }
}
$auth = Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = "smoke-$stamp@learnos.local"; password = $resetPassword; mfaCode = $mfa.currentCode } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($auth.accessToken)" }

$cardBody = @{
    question = "What is active recall?"
    answer = "Retrieving knowledge from memory before checking the answer."
    hint = "Think retrieval practice."
    difficulty = "Easy"
    tag = "Smoke"
} | ConvertTo-Json
$card = Invoke-RestMethod -Uri "$BaseUrl/flashcards" -Method Post -ContentType "application/json" -Headers $headers -Body $cardBody
Assert-True ([bool]$card.id) "flashcard should be created"

$reviewBody = @{ quality = 5; responseTimeSeconds = 30 } | ConvertTo-Json
$reviewed = Invoke-RestMethod -Uri "$BaseUrl/reviews/$($card.id)" -Method Post -ContentType "application/json" -Headers $headers -Body $reviewBody
Assert-True ($reviewed.repetition -ge 1) "review should update SM-2 repetition"

$quizBody = @{ title = "Smoke Quiz"; difficulty = "Mixed"; questionCount = 3 } | ConvertTo-Json
$quiz = Invoke-RestMethod -Uri "$BaseUrl/quizzes/generate-from-flashcards" -Method Post -ContentType "application/json" -Headers $headers -Body $quizBody
Assert-True ($quiz.questions.Count -ge 1) "quiz should include questions"

$answers = @{}
foreach ($question in $quiz.questions) {
    $answers[$question.id] = $question.correctAnswer
}
$attemptBody = @{ quizId = $quiz.id; title = $quiz.title; durationSeconds = 60; answers = $answers } | ConvertTo-Json -Depth 5
$attempt = Invoke-RestMethod -Uri "$BaseUrl/quiz-attempts" -Method Post -ContentType "application/json" -Headers $headers -Body $attemptBody
Assert-True ($attempt.accuracy -eq 100) "correct quiz answers should score 100"

$game = Invoke-RestMethod -Uri "$BaseUrl/gamification/me" -Headers $headers
Assert-True ($game.xp -gt 0) "learning actions should award XP"

$memberRegisterBody = @{
    email = "smoke-member-$stamp@learnos.local"
    password = "Password123!"
    displayName = "Smoke Member"
    role = "Student"
} | ConvertTo-Json

$memberAuth = Invoke-RestMethod -Uri "$BaseUrl/auth/register" -Method Post -ContentType "application/json" -Body $memberRegisterBody
$memberHeaders = @{ Authorization = "Bearer $($memberAuth.accessToken)" }

$roomBody = @{ name = "Smoke Study Room"; topic = "Smoke collaboration" } | ConvertTo-Json
$room = Invoke-RestMethod -Uri "$BaseUrl/collaboration/rooms" -Method Post -ContentType "application/json" -Headers $headers -Body $roomBody
Assert-True ([bool]$room.joinCode) "collaboration room should return a join code"

$joinBody = @{ joinCode = $room.joinCode } | ConvertTo-Json
$joined = Invoke-RestMethod -Uri "$BaseUrl/collaboration/rooms/join" -Method Post -ContentType "application/json" -Headers $memberHeaders -Body $joinBody
Assert-True ($joined.role -eq "Member") "second user should join as room member"

$messageBody = @{ content = "Smoke collaboration message" } | ConvertTo-Json
$message = Invoke-RestMethod -Uri "$BaseUrl/collaboration/rooms/$($room.id)/messages" -Method Post -ContentType "application/json" -Headers $memberHeaders -Body $messageBody
Assert-True ($message.content -eq "Smoke collaboration message") "room message should be saved"

$messages = Invoke-RestMethod -Uri "$BaseUrl/collaboration/rooms/$($room.id)/messages" -Headers $headers
Assert-True (@($messages | Where-Object { $_.id -eq $message.id }).Count -eq 1) "room owner should read member message"

$reminderDueAt = (Get-Date).ToUniversalTime().AddHours(1).ToString("o")
$reminderBody = @{
    title = "Smoke reminder"
    note = "Verify notification reminder persistence"
    channel = "InApp"
    dueAt = $reminderDueAt
} | ConvertTo-Json
$reminder = Invoke-RestMethod -Uri "$BaseUrl/notifications/reminders" -Method Post -ContentType "application/json" -Headers $headers -Body $reminderBody
Assert-True ([bool]$reminder.id) "reminder should be created"

$reminders = Invoke-RestMethod -Uri "$BaseUrl/notifications/reminders" -Headers $headers
Assert-True (@($reminders | Where-Object { $_.id -eq $reminder.id }).Count -eq 1) "created reminder should be listed"

$completedReminder = Invoke-RestMethod -Uri "$BaseUrl/notifications/reminders/$($reminder.id)/complete" -Method Put -Headers $headers
Assert-True ($completedReminder.isCompleted -eq $true) "reminder should be completed"

$parentRegisterBody = @{
    email = "smoke-parent-$stamp@learnos.local"
    password = "Password123!"
    displayName = "Smoke Parent"
    role = "Parent"
} | ConvertTo-Json
$parentAuth = Invoke-RestMethod -Uri "$BaseUrl/auth/register" -Method Post -ContentType "application/json" -Body $parentRegisterBody
$parentHeaders = @{ Authorization = "Bearer $($parentAuth.accessToken)" }

$guardianInvite = Invoke-RestMethod -Uri "$BaseUrl/parent/invitations" -Method Post -Headers $headers
Assert-True ([bool]$guardianInvite.code) "student should create guardian invitation code"

$guardianLink = Invoke-RestMethod -Uri "$BaseUrl/parent/links" -Method Post -ContentType "application/json" -Headers $parentHeaders -Body (@{ code = $guardianInvite.code } | ConvertTo-Json)
Assert-True ($guardianLink.status -eq "Linked") "parent should link to student with invitation code"

$parentStudents = Invoke-RestMethod -Uri "$BaseUrl/parent/students" -Headers $parentHeaders
Assert-True (@($parentStudents | Where-Object { $_.id -eq $auth.user.id }).Count -eq 1) "parent should list linked student"

$parentDashboard = Invoke-RestMethod -Uri "$BaseUrl/parent/students/$($auth.user.id)/dashboard" -Headers $parentHeaders
Assert-True ($parentDashboard.displayName -eq "Smoke Test") "parent dashboard should return linked student data"

$teacherRegisterBody = @{
    email = "smoke-teacher-$stamp@learnos.local"
    password = "Password123!"
    displayName = "Smoke Teacher"
    role = "Teacher"
} | ConvertTo-Json
$teacherAuth = Invoke-RestMethod -Uri "$BaseUrl/auth/register" -Method Post -ContentType "application/json" -Body $teacherRegisterBody
$teacherHeaders = @{ Authorization = "Bearer $($teacherAuth.accessToken)" }

$course = Invoke-RestMethod -Uri "$BaseUrl/courses" -Method Post -ContentType "application/json" -Headers $teacherHeaders -Body (@{ name = "Smoke Course"; subject = "Study Skills" } | ConvertTo-Json)
Assert-True ([bool]$course.joinCode) "teacher should create course with join code"

$joinedCourse = Invoke-RestMethod -Uri "$BaseUrl/courses/join" -Method Post -ContentType "application/json" -Headers $headers -Body (@{ code = $course.joinCode } | ConvertTo-Json)
Assert-True ($joinedCourse.role -eq "Student") "student should join course"

$assignmentDueAt = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
$assignment = Invoke-RestMethod -Uri "$BaseUrl/courses/$($course.id)/assignments" -Method Post -ContentType "application/json" -Headers $teacherHeaders -Body (@{ title = "Smoke Assignment"; instructions = "Explain active recall."; rubric = "Accuracy 60, clarity 40"; dueAt = $assignmentDueAt } | ConvertTo-Json)
Assert-True ([bool]$assignment.id) "teacher should create assignment"
Assert-True ($assignment.rubric -eq "Accuracy 60, clarity 40") "assignment should persist rubric"

$studentAssignments = Invoke-RestMethod -Uri "$BaseUrl/courses/$($course.id)/assignments" -Headers $headers
Assert-True (@($studentAssignments | Where-Object { $_.id -eq $assignment.id }).Count -eq 1) "student should see course assignment"

$submission = Invoke-RestMethod -Uri "$BaseUrl/assignments/$($assignment.id)/submissions" -Method Post -ContentType "application/json" -Headers $headers -Body (@{ content = "Active recall means retrieving before checking." } | ConvertTo-Json)
Assert-True ($submission.content -like "Active recall*") "student should submit assignment"

$courseSubmissions = Invoke-RestMethod -Uri "$BaseUrl/courses/$($course.id)/submissions" -Headers $teacherHeaders
Assert-True (@($courseSubmissions | Where-Object { $_.id -eq $submission.id }).Count -eq 1) "teacher should see course submission"

$gradedSubmission = Invoke-RestMethod -Uri "$BaseUrl/assignments/$($assignment.id)/submissions/$($submission.id)/grade" -Method Put -ContentType "application/json" -Headers $teacherHeaders -Body (@{ score = 95; feedback = "Strong retrieval explanation." } | ConvertTo-Json)
Assert-True ($gradedSubmission.score -eq 95) "teacher should grade submission score"
Assert-True ($gradedSubmission.feedback -eq "Strong retrieval explanation.") "teacher should save submission feedback"
Assert-True ([bool]$gradedSubmission.gradedAt) "graded submission should include graded timestamp"

Write-Host "Smoke tests passed."
