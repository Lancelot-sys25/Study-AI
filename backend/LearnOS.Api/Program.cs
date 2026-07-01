using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UglyToad.PdfPig;
using YoutubeExplode;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LearnOsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LearnOsDb")));
builder.Services.AddHttpClient<OpenAiLearningService>();
builder.Services.AddScoped<SmtpNotificationService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOrTeacher", policy =>
        policy.RequireRole("Student", "Teacher", "Admin"));
    options.AddPolicy("TeacherOnly", policy =>
        policy.RequireRole("Teacher", "Admin"));
    options.AddPolicy("ParentOnly", policy =>
        policy.RequireRole("Parent", "Admin"));
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LearnOsDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/v1/health", async (LearnOsDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return Results.Ok(new
    {
        status = canConnect ? "Healthy" : "Degraded",
        service = "LearnOS.Api",
        database = canConnect ? "Connected" : "Unavailable",
        checkedAt = DateTimeOffset.UtcNow
    });
});

app.MapPost("/api/v1/auth/register", async (RegisterRequest request, LearnOsDbContext db, IConfiguration config) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Email and password are required." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });
    }

    var exists = await db.Users.AnyAsync(user => user.Email == email);
    if (exists)
    {
        return Results.Conflict(new { error = "Email is already registered." });
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName.Trim(),
        PasswordHash = PasswordHasher.Hash(request.Password),
        Role = string.IsNullOrWhiteSpace(request.Role) ? "Student" : request.Role.Trim(),
        EmailVerified = false,
        MfaEnabled = false,
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    var token = CreateRefreshToken(user.Id, config);
    db.RefreshTokens.Add(token);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/users/{user.Id}", BuildAuthResponse(user, token.Token, config));
});

app.MapPost("/api/v1/auth/login", async (LoginRequest request, LearnOsDbContext db, IConfiguration config) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email);

    if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    if (user.MfaEnabled)
    {
        if (string.IsNullOrWhiteSpace(request.MfaCode))
        {
            return Results.Json(new { error = "MFA code is required.", mfaRequired = true }, statusCode: StatusCodes.Status409Conflict);
        }

        if (!Totp.Verify(user.MfaSecret, request.MfaCode))
        {
            return Results.Unauthorized();
        }
    }

    var token = CreateRefreshToken(user.Id, config);
    db.RefreshTokens.Add(token);
    await db.SaveChangesAsync();

    return Results.Ok(BuildAuthResponse(user, token.Token, config));
});

app.MapPost("/api/v1/auth/refresh", async (RefreshRequest request, LearnOsDbContext db, IConfiguration config) =>
{
    var token = await db.RefreshTokens
        .Include(refresh => refresh.User)
        .FirstOrDefaultAsync(refresh => refresh.Token == request.RefreshToken);

    if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    token.RevokedAt = DateTime.UtcNow;
    var nextToken = CreateRefreshToken(token.UserId, config);
    db.RefreshTokens.Add(nextToken);
    await db.SaveChangesAsync();

    return Results.Ok(BuildAuthResponse(token.User, nextToken.Token, config));
});

app.MapPost("/api/v1/auth/logout", async (RefreshRequest request, LearnOsDbContext db) =>
{
    var token = await db.RefreshTokens.FirstOrDefaultAsync(refresh => refresh.Token == request.RefreshToken);
    if (token is not null && token.RevokedAt is null)
    {
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return Results.NoContent();
});

app.MapPost("/api/v1/auth/email-verification/request", async (EmailRequest request, LearnOsDbContext db) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email);
    if (user is null)
    {
        return Results.Ok(new { status = "If the account exists, a verification token was generated." });
    }

    var token = GenerateSecureToken();
    db.AuthActionTokens.Add(new AuthActionToken
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Purpose = "EmailVerification",
        TokenHash = HashToken(token),
        ExpiresAt = DateTime.UtcNow.AddHours(24),
        CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "Verification token generated.", verificationToken = token });
});

app.MapPost("/api/v1/auth/email-verification/confirm", async (ConfirmTokenRequest request, LearnOsDbContext db) =>
{
    var tokenHash = HashToken(request.Token);
    var actionToken = await db.AuthActionTokens
        .Include(item => item.User)
        .FirstOrDefaultAsync(item => item.Purpose == "EmailVerification" && item.TokenHash == tokenHash && item.ConsumedAt == null && item.ExpiresAt > DateTime.UtcNow);

    if (actionToken is null)
    {
        return Results.BadRequest(new { error = "Verification token is invalid or expired." });
    }

    actionToken.ConsumedAt = DateTime.UtcNow;
    actionToken.User.EmailVerified = true;
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "Email verified." });
});

app.MapPost("/api/v1/auth/forgot-password", async (EmailRequest request, LearnOsDbContext db) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email);
    if (user is null)
    {
        return Results.Ok(new { status = "If the account exists, a reset token was generated." });
    }

    var token = GenerateSecureToken();
    db.AuthActionTokens.Add(new AuthActionToken
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Purpose = "PasswordReset",
        TokenHash = HashToken(token),
        ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "Password reset token generated.", resetToken = token });
});

app.MapPost("/api/v1/auth/reset-password", async (ResetPasswordRequest request, LearnOsDbContext db) =>
{
    if (request.NewPassword.Length < 8)
    {
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });
    }

    var tokenHash = HashToken(request.Token);
    var actionToken = await db.AuthActionTokens
        .Include(item => item.User)
        .FirstOrDefaultAsync(item => item.Purpose == "PasswordReset" && item.TokenHash == tokenHash && item.ConsumedAt == null && item.ExpiresAt > DateTime.UtcNow);

    if (actionToken is null)
    {
        return Results.BadRequest(new { error = "Reset token is invalid or expired." });
    }

    actionToken.ConsumedAt = DateTime.UtcNow;
    actionToken.User.PasswordHash = PasswordHasher.Hash(request.NewPassword);

    var activeTokens = await db.RefreshTokens
        .Where(refresh => refresh.UserId == actionToken.UserId && refresh.RevokedAt == null && refresh.ExpiresAt > DateTime.UtcNow)
        .ToListAsync();
    foreach (var refreshToken in activeTokens)
    {
        refreshToken.RevokedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { status = "Password reset. Active sessions were revoked." });
});

app.MapGet("/api/v1/auth/sessions", [Authorize] async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var sessions = await db.RefreshTokens
        .AsNoTracking()
        .Where(refresh => refresh.UserId == userId)
        .OrderByDescending(refresh => refresh.CreatedAt)
        .Take(20)
        .Select(refresh => new SessionDto(refresh.Id, refresh.CreatedAt, refresh.ExpiresAt, refresh.RevokedAt))
        .ToListAsync();

    return Results.Ok(sessions);
});

app.MapDelete("/api/v1/auth/sessions/{sessionId:guid}", [Authorize] async (Guid sessionId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var session = await db.RefreshTokens.FirstOrDefaultAsync(refresh => refresh.Id == sessionId && refresh.UserId == userId);
    if (session is null)
    {
        return Results.NotFound();
    }

    if (session.RevokedAt is null)
    {
        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return Results.NoContent();
});

app.MapPost("/api/v1/auth/mfa/setup", [Authorize] async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId);
    if (user is null)
    {
        return Results.NotFound();
    }

    user.MfaSecret = Totp.GenerateSecret();
    await db.SaveChangesAsync();

    return Results.Ok(new MfaSetupDto(user.MfaSecret, Totp.GetCode(user.MfaSecret)));
});

app.MapPost("/api/v1/auth/mfa/enable", [Authorize] async (MfaCodeRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId);
    if (user is null)
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(user.MfaSecret) || !Totp.Verify(user.MfaSecret, request.Code))
    {
        return Results.BadRequest(new { error = "MFA code is invalid." });
    }

    user.MfaEnabled = true;
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "MFA enabled." });
});

app.MapPost("/api/v1/auth/mfa/disable", [Authorize] async (MfaCodeRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId);
    if (user is null)
    {
        return Results.NotFound();
    }

    if (!Totp.Verify(user.MfaSecret, request.Code))
    {
        return Results.BadRequest(new { error = "MFA code is invalid." });
    }

    user.MfaEnabled = false;
    user.MfaSecret = string.Empty;
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "MFA disabled." });
});

app.MapGet("/api/v1/me", [Authorize] async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userId, out var id))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id);
    return user is null
        ? Results.NotFound()
        : Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.Role, user.EmailVerified, user.MfaEnabled));
});

app.MapGet("/api/v1/dashboard/student", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var today = DateTime.UtcNow.Date;
    var sessions = await db.StudySessions.AsNoTracking().Where(x => x.OwnerId == userId).ToListAsync();
    var cards = await db.Flashcards.AsNoTracking().Where(x => x.OwnerId == userId).ToListAsync();
    var subjects = await db.Subjects.AsNoTracking().Where(x => x.OwnerId == userId).OrderBy(x => x.Name).ToListAsync();
    var attempts = await db.QuizAttempts.AsNoTracking().Where(x => x.OwnerId == userId).ToListAsync();

    var activeDays = sessions
        .Select(session => session.StartedAt.Date)
        .Distinct()
        .ToHashSet();

    var streak = 0;
    for (var day = today; activeDays.Contains(day); day = day.AddDays(-1))
    {
        streak++;
    }

    var studyHours = Math.Round(sessions.Sum(session => session.FocusMinutes) / 60.0, 1);
    var completionRate = attempts.Count == 0
        ? 0
        : (int)Math.Round(attempts.Average(attempt => attempt.Accuracy));

    var recommendation = subjects.Count == 0
        ? "Import material or create your first flashcard to unlock recommendations."
        : $"Focus on {subjects.OrderBy(x => x.Progress).First().Name} for 18 minutes, then take a short quiz.";

    return Results.Ok(new StudentDashboardDto(
        StudyHours: studyHours,
        StudyStreakDays: streak,
        CompletionRate: completionRate,
        DueFlashcards: cards.Count(card => card.DueAt <= DateTime.UtcNow),
        TodayQuiz: attempts.Any(attempt => attempt.StartedAt.Date == today) ? "Completed" : "Not started",
        AiRecommendation: recommendation,
        Subjects: subjects.Select(subject => new SubjectProgressDto(subject.Name, subject.Progress))
    ));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/gamification/me", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var profile = await GetOrCreateGamificationProfile(db, userId);
    await db.SaveChangesAsync();
    return Results.Ok(ToGamificationDto(profile));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/dashboard/teacher", [Authorize(Policy = "TeacherOnly")] async (LearnOsDbContext db) =>
{
    var attempts = await db.QuizAttempts.AsNoTracking().ToListAsync();
    var subjects = await db.Subjects.AsNoTracking().OrderBy(subject => subject.Progress).ToListAsync();
    var sessions = await db.StudySessions.AsNoTracking().ToListAsync();
    var averageAccuracy = attempts.Count == 0 ? 0 : (int)Math.Round(attempts.Average(attempt => attempt.Accuracy));

    return Results.Ok(new
    {
        classes = 1,
        totalAttempts = attempts.Count,
        activeLearnersProxy = sessions.Select(session => session.StartedAt.Date).Distinct().Count(),
        studentsAtRisk = attempts.Count(attempt => attempt.Accuracy < 60),
        averageAccuracy,
        weakTopics = subjects.Take(5).Select(subject => new { subject.Name, subject.Progress }),
        message = "Teacher/Admin role can see aggregate learning risk signals."
    });
});

app.MapGet("/api/v1/dashboard/admin", [Authorize(Policy = "AdminOnly")] async (LearnOsDbContext db) =>
{
    return Results.Ok(new
    {
        users = await db.Users.CountAsync(),
        flashcards = await db.Flashcards.CountAsync(),
        quizAttempts = await db.QuizAttempts.CountAsync(),
        aiMessages = await db.AiMessages.CountAsync()
    });
});

app.MapPost("/api/v1/parent/invitations", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var studentId = GetCurrentUserId(principal);
    var code = await CreateUniqueGuardianInviteCode(db);
    var invitation = new GuardianInvitation
    {
        Id = Guid.NewGuid(),
        StudentId = studentId,
        Code = code,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    };

    db.GuardianInvitations.Add(invitation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/parent/invitations/{invitation.Id}", new GuardianInvitationDto(invitation.Id, invitation.Code, invitation.ExpiresAt, invitation.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/parent/links", [Authorize(Policy = "ParentOnly")] async (JoinGuardianLinkRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var parentId = GetCurrentUserId(principal);
    var code = NormalizeJoinCode(request.Code);
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { error = "Invitation code is required." });
    }

    var invitation = await db.GuardianInvitations.FirstOrDefaultAsync(item => item.Code == code && item.ConsumedAt == null && item.ExpiresAt > DateTime.UtcNow);
    if (invitation is null)
    {
        return Results.NotFound(new { error = "Invitation code is invalid or expired." });
    }

    var existing = await db.GuardianLinks.FirstOrDefaultAsync(item => item.ParentId == parentId && item.StudentId == invitation.StudentId);
    if (existing is null)
    {
        db.GuardianLinks.Add(new GuardianLink
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StudentId = invitation.StudentId,
            CreatedAt = DateTime.UtcNow
        });
    }

    invitation.ConsumedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { status = "Linked", studentId = invitation.StudentId });
});

app.MapGet("/api/v1/parent/students", [Authorize(Policy = "ParentOnly")] async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var parentId = GetCurrentUserId(principal);
    var students = await db.GuardianLinks
        .AsNoTracking()
        .Where(link => link.ParentId == parentId)
        .Join(db.Users.AsNoTracking(), link => link.StudentId, user => user.Id, (link, user) => new { user.Id, user.DisplayName, user.Email, link.CreatedAt })
        .OrderBy(student => student.DisplayName)
        .Select(student => new ParentStudentDto(student.Id, student.DisplayName, student.Email, student.CreatedAt))
        .ToListAsync();

    return Results.Ok(students);
});

app.MapGet("/api/v1/parent/students/{studentId:guid}/dashboard", [Authorize(Policy = "ParentOnly")] async (Guid studentId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var parentId = GetCurrentUserId(principal);
    var linked = await db.GuardianLinks.AnyAsync(link => link.ParentId == parentId && link.StudentId == studentId);
    if (!linked)
    {
        return Results.NotFound();
    }

    var student = await db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == studentId);
    if (student is null)
    {
        return Results.NotFound();
    }

    var sessions = await db.StudySessions.AsNoTracking().Where(session => session.OwnerId == studentId).ToListAsync();
    var cards = await db.Flashcards.AsNoTracking().Where(card => card.OwnerId == studentId).ToListAsync();
    var attempts = await db.QuizAttempts.AsNoTracking().Where(attempt => attempt.OwnerId == studentId).ToListAsync();
    var reminders = await db.LearningReminders.AsNoTracking().Where(reminder => reminder.OwnerId == studentId && !reminder.IsCompleted).CountAsync();

    var totalQuestions = attempts.Sum(attempt => attempt.TotalCount);
    var correctQuestions = attempts.Sum(attempt => attempt.CorrectCount);
    var accuracy = totalQuestions == 0 ? 0 : (int)Math.Round(correctQuestions * 100.0 / totalQuestions);

    return Results.Ok(new ParentStudentDashboardDto(
        student.Id,
        student.DisplayName,
        Math.Round(sessions.Sum(session => session.FocusMinutes) / 60.0, 1),
        sessions.Select(session => session.StartedAt.Date).Distinct().Count(),
        cards.Count(card => card.DueAt <= DateTime.UtcNow),
        attempts.Count,
        accuracy,
        reminders));
});

app.MapGet("/api/v1/courses", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var role = principal.FindFirstValue(ClaimTypes.Role);
    if (role is "Teacher" or "Admin")
    {
        var owned = await db.Courses
            .AsNoTracking()
            .Where(course => course.TeacherId == userId || role == "Admin")
            .OrderByDescending(course => course.CreatedAt)
            .Select(course => new CourseDto(course.Id, course.Name, course.Subject, course.JoinCode, course.TeacherId, "Teacher", course.CreatedAt))
            .ToListAsync();
        return Results.Ok(owned);
    }

    var enrolled = await db.CourseEnrollments
        .AsNoTracking()
        .Where(enrollment => enrollment.StudentId == userId)
        .Join(db.Courses.AsNoTracking(), enrollment => enrollment.CourseId, course => course.Id, (enrollment, course) => new { course, enrollment })
        .OrderBy(item => item.course.Name)
        .Select(item => new CourseDto(item.course.Id, item.course.Name, item.course.Subject, item.course.JoinCode, item.course.TeacherId, "Student", item.course.CreatedAt))
        .ToListAsync();

    return Results.Ok(enrolled);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/courses", [Authorize(Policy = "TeacherOnly")] async (CreateCourseRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Course name is required." });
    }

    var teacherId = GetCurrentUserId(principal);
    var course = new Course
    {
        Id = Guid.NewGuid(),
        TeacherId = teacherId,
        Name = request.Name.Trim(),
        Subject = string.IsNullOrWhiteSpace(request.Subject) ? "General" : request.Subject.Trim(),
        JoinCode = await CreateUniqueCourseJoinCode(db),
        CreatedAt = DateTime.UtcNow
    };

    db.Courses.Add(course);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/courses/{course.Id}", new CourseDto(course.Id, course.Name, course.Subject, course.JoinCode, course.TeacherId, "Teacher", course.CreatedAt));
});

app.MapPost("/api/v1/courses/join", async (JoinCourseRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var code = NormalizeJoinCode(request.Code);
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { error = "Course join code is required." });
    }

    var course = await db.Courses.FirstOrDefaultAsync(item => item.JoinCode == code);
    if (course is null)
    {
        return Results.NotFound(new { error = "Course was not found." });
    }

    var existing = await db.CourseEnrollments.FirstOrDefaultAsync(item => item.CourseId == course.Id && item.StudentId == userId);
    if (existing is null && course.TeacherId != userId)
    {
        db.CourseEnrollments.Add(new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = userId,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    return Results.Ok(new CourseDto(course.Id, course.Name, course.Subject, course.JoinCode, course.TeacherId, course.TeacherId == userId ? "Teacher" : "Student", course.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/courses/{courseId:guid}/assignments", async (Guid courseId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (!await CanAccessCourse(db, courseId, userId, principal))
    {
        return Results.NotFound();
    }

    var assignments = await db.Assignments
        .AsNoTracking()
        .Where(assignment => assignment.CourseId == courseId)
        .OrderBy(assignment => assignment.DueAt)
        .Select(assignment => new AssignmentDto(assignment.Id, assignment.CourseId, assignment.Title, assignment.Instructions, assignment.DueAt, assignment.CreatedAt))
        .ToListAsync();

    return Results.Ok(assignments);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/courses/{courseId:guid}/assignments", [Authorize(Policy = "TeacherOnly")] async (Guid courseId, CreateAssignmentRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var teacherId = GetCurrentUserId(principal);
    if (!await IsCourseTeacher(db, courseId, teacherId, principal))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Assignment title is required." });
    }

    var assignment = new Assignment
    {
        Id = Guid.NewGuid(),
        CourseId = courseId,
        Title = request.Title.Trim(),
        Instructions = request.Instructions?.Trim() ?? string.Empty,
        DueAt = request.DueAt,
        CreatedAt = DateTime.UtcNow
    };

    db.Assignments.Add(assignment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/courses/{courseId}/assignments/{assignment.Id}", new AssignmentDto(assignment.Id, assignment.CourseId, assignment.Title, assignment.Instructions, assignment.DueAt, assignment.CreatedAt));
});

app.MapPost("/api/v1/assignments/{assignmentId:guid}/submissions", async (Guid assignmentId, CreateAssignmentSubmissionRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var studentId = GetCurrentUserId(principal);
    var assignment = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == assignmentId);
    if (assignment is null || !await CanAccessCourse(db, assignment.CourseId, studentId, principal))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(new { error = "Submission content is required." });
    }

    var submission = await db.AssignmentSubmissions.FirstOrDefaultAsync(item => item.AssignmentId == assignmentId && item.StudentId == studentId);
    if (submission is null)
    {
        submission = new AssignmentSubmission
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentId,
            StudentId = studentId,
            SubmittedAt = DateTime.UtcNow
        };
        db.AssignmentSubmissions.Add(submission);
    }

    submission.Content = request.Content.Trim();
    submission.SubmittedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(ToAssignmentSubmissionDto(submission));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/courses/{courseId:guid}/submissions", [Authorize(Policy = "TeacherOnly")] async (Guid courseId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var teacherId = GetCurrentUserId(principal);
    if (!await IsCourseTeacher(db, courseId, teacherId, principal))
    {
        return Results.NotFound();
    }

    var submissions = await db.AssignmentSubmissions
        .AsNoTracking()
        .Join(db.Assignments.AsNoTracking().Where(assignment => assignment.CourseId == courseId), submission => submission.AssignmentId, assignment => assignment.Id, (submission, assignment) => submission)
        .OrderByDescending(submission => submission.SubmittedAt)
        .Select(submission => new AssignmentSubmissionDto(submission.Id, submission.AssignmentId, submission.StudentId, submission.Content, submission.Score, submission.Feedback, submission.SubmittedAt, submission.GradedAt))
        .ToListAsync();

    return Results.Ok(submissions);
});

app.MapGet("/api/v1/flashcards", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var cards = await db.Flashcards
        .AsNoTracking()
        .Where(card => card.OwnerId == userId)
        .OrderByDescending(card => card.CreatedAt)
        .Select(card => new FlashcardDto(
            card.Id,
            card.Tag,
            card.Question,
            card.Answer,
            card.Hint,
            card.Difficulty,
            card.ConfidenceScore,
            card.DueAt,
            card.Repetition,
            card.EaseFactor,
            card.IntervalDays,
            card.ReviewCount))
        .ToListAsync();

    return Results.Ok(cards);
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/documents", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var documents = await db.LearningDocuments
        .AsNoTracking()
        .Where(document => document.OwnerId == userId)
        .OrderByDescending(document => document.CreatedAt)
        .Select(document => new DocumentDto(
            document.Id,
            document.Title,
            document.FileName,
            document.ContentType,
            document.Status,
            document.TextContent.Length,
            document.CreatedAt))
        .ToListAsync();

    return Results.Ok(documents);
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/documents/{documentId:guid}", async (Guid documentId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var document = await db.LearningDocuments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == documentId && item.OwnerId == userId);
    return document is null
        ? Results.NotFound()
        : Results.Ok(new DocumentDetailDto(
            document.Id,
            document.Title,
            document.FileName,
            document.ContentType,
            document.Status,
            document.TextContent,
            document.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/documents", async (HttpRequest request, ClaimsPrincipal principal, LearnOsDbContext db, IWebHostEnvironment env, OpenAiLearningService ai) =>
{
    var userId = GetCurrentUserId(principal);
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "File is required." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var supported = new HashSet<string> { ".txt", ".md", ".markdown", ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".mp3", ".wav", ".m4a", ".mp4", ".webm" };
    if (!supported.Contains(extension))
    {
        return Results.BadRequest(new { error = "Supported files: TXT, Markdown, PDF, PNG, JPG, WEBP, MP3, WAV, M4A, MP4, and WEBM." });
    }

    var uploadDir = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
    Directory.CreateDirectory(uploadDir);

    var storedName = $"{Guid.NewGuid()}{extension}";
    var storedPath = Path.Combine(uploadDir, storedName);
    await using (var stream = File.Create(storedPath))
    {
        await file.CopyToAsync(stream);
    }

    string textContent;
    try
    {
        textContent = extension switch
        {
            ".txt" or ".md" or ".markdown" => await File.ReadAllTextAsync(storedPath),
            ".pdf" => ExtractPdfText(storedPath),
            ".png" or ".jpg" or ".jpeg" or ".webp" => await ai.ExtractImageTextAsync(storedPath, file.ContentType),
            ".mp3" or ".wav" or ".m4a" or ".mp4" or ".webm" => await ai.TranscribeAudioAsync(storedPath, file.FileName),
            _ => string.Empty
        };
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var document = new LearningDocument
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Title = form["title"].FirstOrDefault() ?? Path.GetFileNameWithoutExtension(file.FileName),
        FileName = file.FileName,
        ContentType = file.ContentType,
        StoragePath = storedPath,
        TextContent = textContent,
        Status = string.IsNullOrWhiteSpace(textContent) ? "Uploaded" : "TextExtracted",
        CreatedAt = DateTime.UtcNow
    };

    db.LearningDocuments.Add(document);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/documents/{document.Id}", new DocumentDto(
        document.Id,
        document.Title,
        document.FileName,
        document.ContentType,
        document.Status,
        document.TextContent.Length,
        document.CreatedAt));
}).DisableAntiforgery().RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/documents/youtube", async (YouTubeIngestRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Url))
    {
        return Results.BadRequest(new { error = "YouTube URL is required." });
    }

    var youtube = new YoutubeClient();
    try
    {
        var video = await youtube.Videos.GetAsync(request.Url.Trim());
        var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(video.Id);
        var trackInfo = string.IsNullOrWhiteSpace(request.Language)
            ? manifest.Tracks.FirstOrDefault()
            : manifest.TryGetByLanguage(request.Language.Trim()) ?? manifest.Tracks.FirstOrDefault();

        if (trackInfo is null)
        {
            return Results.UnprocessableEntity(new { error = "This YouTube video does not expose captions/transcripts." });
        }

        var captions = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);
        var transcript = string.Join(Environment.NewLine, captions.Captions.Select(caption => caption.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return Results.UnprocessableEntity(new { error = "Captions were found, but the transcript was empty." });
        }

        var document = new LearningDocument
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? video.Title : request.Title.Trim(),
            FileName = video.Url,
            ContentType = "text/youtube-transcript",
            StoragePath = video.Url,
            TextContent = transcript,
            Status = $"TranscriptExtracted:{trackInfo.Language.Code}",
            CreatedAt = DateTime.UtcNow
        };

        db.LearningDocuments.Add(document);
        await db.SaveChangesAsync();

        return Results.Created($"/api/v1/documents/{document.Id}", new DocumentDto(
            document.Id,
            document.Title,
            document.FileName,
            document.ContentType,
            document.Status,
            document.TextContent.Length,
            document.CreatedAt));
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException)
    {
        return Results.Problem($"Could not ingest YouTube transcript: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/documents/{documentId:guid}/flashcards", async (Guid documentId, GenerateDocumentFlashcardsRequest request, ClaimsPrincipal principal, LearnOsDbContext db, OpenAiLearningService ai) =>
{
    var userId = GetCurrentUserId(principal);
    var document = await db.LearningDocuments.FirstOrDefaultAsync(item => item.Id == documentId && item.OwnerId == userId);
    if (document is null)
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(document.TextContent))
    {
        return Results.BadRequest(new { error = "This document has no extracted text yet. PDF text extraction is not implemented in this module." });
    }

    List<GeneratedFlashcard> generatedCards;
    try
    {
        generatedCards = await ai.GenerateFlashcardsAsync(request.Topic ?? document.Title, document.TextContent, request.Difficulty ?? "Medium");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var cards = generatedCards.Select(card => new Flashcard
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Tag = request.Topic ?? document.Title,
        Question = card.Question,
        Answer = card.Answer,
        Hint = card.Hint,
        Difficulty = card.Difficulty,
        ConfidenceScore = Math.Clamp(card.ConfidenceScore, 0, 100),
        DueAt = DateTime.UtcNow,
        EaseFactor = 2.5,
        CreatedAt = DateTime.UtcNow
    }).ToList();

    db.Flashcards.AddRange(cards);
    await UpsertSubject(db, userId, request.Topic ?? document.Title, Math.Min(20, cards.Count * 4));
    await db.SaveChangesAsync();

    return Results.Created("/api/v1/flashcards", cards.Select(ToFlashcardDto));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/flashcards", async (CreateFlashcardRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Answer))
    {
        return Results.BadRequest(new { error = "Question and answer are required." });
    }

    var card = new Flashcard
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Tag = string.IsNullOrWhiteSpace(request.Tag) ? "General" : request.Tag.Trim(),
        Question = request.Question.Trim(),
        Answer = request.Answer.Trim(),
        Hint = request.Hint?.Trim() ?? string.Empty,
        Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
        ConfidenceScore = 100,
        DueAt = DateTime.UtcNow,
        EaseFactor = 2.5,
        CreatedAt = DateTime.UtcNow
    };

    db.Flashcards.Add(card);
    await UpsertSubject(db, userId, card.Tag, 5);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/flashcards/{card.Id}", ToFlashcardDto(card));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/flashcards/generate", async (GenerateFlashcardsRequest request, ClaimsPrincipal principal, LearnOsDbContext db, OpenAiLearningService ai) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Topic) || string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(new { error = "Topic and content are required." });
    }

    List<GeneratedFlashcard> generatedCards;
    try
    {
        generatedCards = await ai.GenerateFlashcardsAsync(request.Topic.Trim(), request.Content.Trim(), request.Difficulty ?? "Medium");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    if (generatedCards.Count == 0)
    {
        return Results.Problem("OpenAI did not return any flashcards.", statusCode: StatusCodes.Status502BadGateway);
    }

    var cards = generatedCards.Select(card => new Flashcard
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Tag = request.Topic.Trim(),
        Question = card.Question,
        Answer = card.Answer,
        Hint = card.Hint,
        Difficulty = string.IsNullOrWhiteSpace(card.Difficulty) ? request.Difficulty ?? "Medium" : card.Difficulty,
        ConfidenceScore = Math.Clamp(card.ConfidenceScore, 0, 100),
        DueAt = DateTime.UtcNow,
        EaseFactor = 2.5,
        CreatedAt = DateTime.UtcNow
    }).ToList();

    db.Flashcards.AddRange(cards);
    await UpsertSubject(db, userId, request.Topic.Trim(), Math.Min(20, cards.Count * 4));
    await db.SaveChangesAsync();

    return Results.Created("/api/v1/flashcards", cards.Select(ToFlashcardDto));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/reviews/{cardId:guid}", async (Guid cardId, ReviewRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var card = await db.Flashcards.FirstOrDefaultAsync(item => item.Id == cardId && item.OwnerId == userId);
    if (card is null)
    {
        return Results.NotFound();
    }

    ApplySm2(card, Math.Clamp(request.Quality, 0, 5));
    card.ReviewCount++;
    card.UpdatedAt = DateTime.UtcNow;

    db.StudySessions.Add(new StudySession
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Title = $"Reviewed {card.Tag}",
        SessionType = "Flashcard",
        FocusMinutes = Math.Max(1, request.ResponseTimeSeconds / 60),
        StartedAt = DateTime.UtcNow
    });

    var profile = await GetOrCreateGamificationProfile(db, GetCurrentUserId(principal));
    AwardLearningReward(profile, request.Quality >= 3 ? 12 : 5, request.Quality >= 3 ? 2 : 1);

    await UpsertSubject(db, userId, card.Tag, request.Quality >= 3 ? 3 : -2);
    await db.SaveChangesAsync();

    return Results.Ok(ToFlashcardDto(card));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/flashcards/due", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var cards = await db.Flashcards
        .AsNoTracking()
        .Where(card => card.OwnerId == userId && card.DueAt <= DateTime.UtcNow)
        .OrderBy(card => card.DueAt)
        .Select(card => new FlashcardDto(
            card.Id,
            card.Tag,
            card.Question,
            card.Answer,
            card.Hint,
            card.Difficulty,
            card.ConfidenceScore,
            card.DueAt,
            card.Repetition,
            card.EaseFactor,
            card.IntervalDays,
            card.ReviewCount))
        .ToListAsync();

    return Results.Ok(cards);
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/flashcards/{cardId:guid}", async (Guid cardId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var card = await db.Flashcards.AsNoTracking().FirstOrDefaultAsync(item => item.Id == cardId && item.OwnerId == userId);
    return card is null ? Results.NotFound() : Results.Ok(ToFlashcardDto(card));
}).RequireAuthorization("StudentOrTeacher");

app.MapPut("/api/v1/flashcards/{cardId:guid}", async (Guid cardId, UpdateFlashcardRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var card = await db.Flashcards.FirstOrDefaultAsync(item => item.Id == cardId && item.OwnerId == userId);
    if (card is null)
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Answer))
    {
        return Results.BadRequest(new { error = "Question and answer are required." });
    }

    card.Question = request.Question.Trim();
    card.Answer = request.Answer.Trim();
    card.Hint = request.Hint?.Trim() ?? string.Empty;
    card.Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? card.Difficulty : request.Difficulty.Trim();
    card.Tag = string.IsNullOrWhiteSpace(request.Tag) ? card.Tag : request.Tag.Trim();
    card.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();

    return Results.Ok(ToFlashcardDto(card));
}).RequireAuthorization("StudentOrTeacher");

app.MapDelete("/api/v1/flashcards/{cardId:guid}", async (Guid cardId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var card = await db.Flashcards.FirstOrDefaultAsync(item => item.Id == cardId && item.OwnerId == userId);
    if (card is null)
    {
        return Results.NotFound();
    }

    db.Flashcards.Remove(card);
    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/quizzes", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var quizzes = await db.Quizzes
        .AsNoTracking()
        .Include(quiz => quiz.Questions)
        .Where(quiz => quiz.OwnerId == userId)
        .OrderByDescending(quiz => quiz.CreatedAt)
        .Select(quiz => new QuizDto(
            quiz.Id,
            quiz.Title,
            quiz.Difficulty,
            quiz.Questions.OrderBy(question => question.SortOrder).Select(question => new QuizQuestionDto(
                question.Id,
                question.Type,
                question.Prompt,
                question.OptionsJson == "" ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(question.OptionsJson, JsonOptionsProvider.Options) ?? Array.Empty<string>(),
                question.CorrectAnswer,
                question.Explanation))))
        .ToListAsync();

    return Results.Ok(quizzes);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/quizzes/generate-from-flashcards", async (GenerateQuizRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var cards = await db.Flashcards
        .AsNoTracking()
        .Where(card => card.OwnerId == userId)
        .OrderBy(card => card.DueAt)
        .Take(Math.Clamp(request.QuestionCount, 3, 20))
        .ToListAsync();

    if (cards.Count == 0)
    {
        return Results.BadRequest(new { error = "Create flashcards before generating a quiz." });
    }

    var quiz = new Quiz
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Title = string.IsNullOrWhiteSpace(request.Title) ? "Adaptive flashcard quiz" : request.Title.Trim(),
        Difficulty = request.Difficulty ?? "Mixed",
        Source = "Flashcards",
        CreatedAt = DateTime.UtcNow,
        Questions = BuildQuizQuestions(cards)
    };

    db.Quizzes.Add(quiz);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/quizzes/{quiz.Id}", ToQuizDto(quiz));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/quizzes/{quizId:guid}", async (Guid quizId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var quiz = await db.Quizzes
        .AsNoTracking()
        .Include(item => item.Questions)
        .FirstOrDefaultAsync(item => item.Id == quizId && item.OwnerId == userId);

    return quiz is null ? Results.NotFound() : Results.Ok(ToQuizDto(quiz));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/quizzes/today", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var existingQuiz = await db.Quizzes
        .AsNoTracking()
        .Include(quiz => quiz.Questions)
        .Where(quiz => quiz.OwnerId == userId)
        .OrderByDescending(quiz => quiz.CreatedAt)
        .FirstOrDefaultAsync();

    if (existingQuiz is not null)
    {
        return Results.Ok(ToQuizDto(existingQuiz));
    }

    var question = await db.Flashcards
        .AsNoTracking()
        .Where(card => card.OwnerId == userId)
        .OrderBy(card => card.DueAt)
        .FirstOrDefaultAsync();

    if (question is null)
    {
        return Results.Ok(new QuizDto(
            Guid.Empty,
            "Create flashcards first",
            "Easy",
            Array.Empty<QuizQuestionDto>()));
    }

    return Results.Ok(new QuizDto(
        Guid.NewGuid(),
        $"Review: {question.Tag}",
        question.Difficulty,
        new[]
        {
            new QuizQuestionDto(
                Guid.NewGuid(),
                "Typing",
                question.Question,
                Array.Empty<string>(),
                question.Answer,
                "Compare your response with the saved flashcard answer.")
        }));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/analytics/study-series", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var start = DateTime.UtcNow.Date.AddDays(-6);
    var sessions = await db.StudySessions
        .AsNoTracking()
        .Where(session => session.OwnerId == userId && session.StartedAt.Date >= start)
        .ToListAsync();
    var attempts = await db.QuizAttempts
        .AsNoTracking()
        .Where(attempt => attempt.OwnerId == userId && attempt.StartedAt.Date >= start)
        .ToListAsync();

    var points = Enumerable.Range(0, 7).Select(offset =>
    {
        var day = start.AddDays(offset);
        var daySessions = sessions.Where(session => session.StartedAt.Date == day).ToList();
        var dayAttempts = attempts.Where(attempt => attempt.StartedAt.Date == day).ToList();

        return new StudySeriesPointDto(
            Day: day.ToString("ddd"),
            Minutes: daySessions.Sum(session => session.FocusMinutes),
            Accuracy: dayAttempts.Count == 0 ? 0 : (int)Math.Round(dayAttempts.Average(attempt => attempt.Accuracy)));
    });

    return Results.Ok(points);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/quiz-attempts", async (QuizAttemptRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var quiz = request.QuizId is null
        ? null
        : await db.Quizzes
            .AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == request.QuizId.Value && item.OwnerId == userId);

    var accuracy = Math.Clamp(request.Accuracy, 0, 100);
    var correctCount = 0;
    var totalCount = 0;

    if (quiz is not null && request.Answers.Count > 0)
    {
        totalCount = quiz.Questions.Count;
        correctCount = quiz.Questions.Count(question =>
            request.Answers.TryGetValue(question.Id, out var answer)
            && IsAnswerCorrect(answer, question.CorrectAnswer));
        accuracy = totalCount == 0 ? 0 : (int)Math.Round(correctCount * 100.0 / totalCount);
    }

    var attempt = new QuizAttempt
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        QuizId = request.QuizId,
        Title = request.Title,
        Accuracy = accuracy,
        CorrectCount = correctCount,
        TotalCount = totalCount,
        DurationSeconds = Math.Max(0, request.DurationSeconds),
        StartedAt = DateTime.UtcNow
    };

    db.QuizAttempts.Add(attempt);
    db.StudySessions.Add(new StudySession
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Title = request.Title,
        SessionType = "Quiz",
        FocusMinutes = Math.Max(1, request.DurationSeconds / 60),
        StartedAt = DateTime.UtcNow
    });

    var profile = await GetOrCreateGamificationProfile(db, userId);
    AwardLearningReward(profile, 20 + accuracy / 5, 5 + accuracy / 25);

    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/quiz-attempts/{attempt.Id}", attempt);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/ai/chat", async (AiChatRequest request, ClaimsPrincipal principal, LearnOsDbContext db, OpenAiLearningService ai) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    var conversationId = request.ConversationId ?? Guid.NewGuid();
    string answer;
    try
    {
        answer = await ai.AnswerTutorQuestionAsync(request.Message.Trim());
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    db.AiMessages.Add(new AiMessage
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ConversationId = conversationId,
        Role = "You",
        Content = request.Message.Trim(),
        CreatedAt = DateTime.UtcNow
    });

    db.AiMessages.Add(new AiMessage
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ConversationId = conversationId,
        Role = "AI Tutor",
        Content = answer,
        CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    return Results.Ok(new AiChatResponse(
        ConversationId: conversationId,
        Role: "AI Tutor",
        Content: answer,
        Model: "rule-based-local-tutor",
        ConfidenceScore: 72));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/ai/conversations/{conversationId:guid}/messages", async (Guid conversationId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var messages = await db.AiMessages
        .AsNoTracking()
        .Where(message => message.ConversationId == conversationId && message.UserId == userId)
        .OrderBy(message => message.CreatedAt)
        .Select(message => new { message.Role, message.Content, message.CreatedAt })
        .ToListAsync();

    return Results.Ok(messages);
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/collaboration/rooms", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var memberships = await db.CollaborationRoomMembers
        .AsNoTracking()
        .Where(member => member.UserId == userId)
        .Join(
            db.CollaborationRooms.AsNoTracking(),
            member => member.RoomId,
            room => room.Id,
            (member, room) => new { member, room })
        .OrderByDescending(item => item.room.CreatedAt)
        .ToListAsync();

    var roomIds = memberships.Select(item => item.room.Id).ToArray();
    var memberCounts = await db.CollaborationRoomMembers
        .AsNoTracking()
        .Where(member => roomIds.Contains(member.RoomId))
        .GroupBy(member => member.RoomId)
        .Select(group => new { RoomId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.RoomId, item => item.Count);
    var messageCounts = await db.CollaborationMessages
        .AsNoTracking()
        .Where(message => roomIds.Contains(message.RoomId))
        .GroupBy(message => message.RoomId)
        .Select(group => new { RoomId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.RoomId, item => item.Count);

    var rooms = memberships
        .Select(item => new CollaborationRoomDto(
            item.room.Id,
            item.room.Name,
            item.room.Topic,
            item.room.JoinCode,
            item.member.Role,
            item.room.CreatedAt,
            memberCounts.GetValueOrDefault(item.room.Id),
            messageCounts.GetValueOrDefault(item.room.Id)))
        .ToList();

    return Results.Ok(rooms);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/collaboration/rooms", async (CreateCollaborationRoomRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Room name is required." });
    }

    var room = new CollaborationRoom
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Name = request.Name.Trim(),
        Topic = string.IsNullOrWhiteSpace(request.Topic) ? "General study" : request.Topic.Trim(),
        JoinCode = await CreateUniqueJoinCode(db),
        CreatedAt = DateTime.UtcNow
    };

    db.CollaborationRooms.Add(room);
    db.CollaborationRoomMembers.Add(new CollaborationRoomMember
    {
        Id = Guid.NewGuid(),
        RoomId = room.Id,
        UserId = userId,
        Role = "Owner",
        JoinedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/collaboration/rooms/{room.Id}", new CollaborationRoomDto(
        room.Id,
        room.Name,
        room.Topic,
        room.JoinCode,
        "Owner",
        room.CreatedAt,
        1,
        0));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/collaboration/rooms/join", async (JoinCollaborationRoomRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var joinCode = NormalizeJoinCode(request.JoinCode);
    if (string.IsNullOrWhiteSpace(joinCode))
    {
        return Results.BadRequest(new { error = "Join code is required." });
    }

    var room = await db.CollaborationRooms.FirstOrDefaultAsync(item => item.JoinCode == joinCode);
    if (room is null)
    {
        return Results.NotFound(new { error = "Room was not found." });
    }

    var existingMember = await db.CollaborationRoomMembers.FirstOrDefaultAsync(item => item.RoomId == room.Id && item.UserId == userId);
    if (existingMember is null)
    {
        db.CollaborationRoomMembers.Add(new CollaborationRoomMember
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            UserId = userId,
            Role = "Member",
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    var role = existingMember?.Role ?? "Member";
    var memberCount = await db.CollaborationRoomMembers.CountAsync(member => member.RoomId == room.Id);
    var messageCount = await db.CollaborationMessages.CountAsync(message => message.RoomId == room.Id);

    return Results.Ok(new CollaborationRoomDto(
        room.Id,
        room.Name,
        room.Topic,
        room.JoinCode,
        role,
        room.CreatedAt,
        memberCount,
        messageCount));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/collaboration/rooms/{roomId:guid}/messages", async (Guid roomId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (!await IsCollaborationRoomMember(db, roomId, userId))
    {
        return Results.NotFound();
    }

    var messages = await db.CollaborationMessages
        .AsNoTracking()
        .Where(message => message.RoomId == roomId)
        .OrderBy(message => message.CreatedAt)
        .Select(message => new CollaborationMessageDto(
            message.Id,
            message.RoomId,
            message.UserId,
            message.DisplayName,
            message.Content,
            message.CreatedAt))
        .ToListAsync();

    return Results.Ok(messages);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/collaboration/rooms/{roomId:guid}/messages", async (Guid roomId, CreateCollaborationMessageRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(new { error = "Message content is required." });
    }

    if (!await IsCollaborationRoomMember(db, roomId, userId))
    {
        return Results.NotFound();
    }

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
    var message = new CollaborationMessage
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        UserId = userId,
        DisplayName = user?.DisplayName ?? "Learner",
        Content = request.Content.Trim(),
        CreatedAt = DateTime.UtcNow
    };

    db.CollaborationMessages.Add(message);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/collaboration/rooms/{roomId}/messages/{message.Id}", new CollaborationMessageDto(
        message.Id,
        message.RoomId,
        message.UserId,
        message.DisplayName,
        message.Content,
        message.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/notifications/reminders", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var reminders = await db.LearningReminders
        .AsNoTracking()
        .Where(reminder => reminder.OwnerId == userId)
        .OrderBy(reminder => reminder.IsCompleted)
        .ThenBy(reminder => reminder.DueAt)
        .Select(reminder => new LearningReminderDto(
            reminder.Id,
            reminder.Title,
            reminder.Note,
            reminder.Channel,
            reminder.DueAt,
            reminder.IsCompleted,
            reminder.CreatedAt))
        .ToListAsync();

    return Results.Ok(reminders);
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/notifications/reminders", async (CreateLearningReminderRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Reminder title is required." });
    }

    if (request.DueAt <= DateTime.UtcNow.AddMinutes(-1))
    {
        return Results.BadRequest(new { error = "Reminder due time must be in the future." });
    }

    var reminder = new LearningReminder
    {
        Id = Guid.NewGuid(),
        OwnerId = userId,
        Title = request.Title.Trim(),
        Note = request.Note?.Trim() ?? string.Empty,
        Channel = string.IsNullOrWhiteSpace(request.Channel) ? "InApp" : request.Channel.Trim(),
        DueAt = request.DueAt,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.LearningReminders.Add(reminder);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/notifications/reminders/{reminder.Id}", new LearningReminderDto(
        reminder.Id,
        reminder.Title,
        reminder.Note,
        reminder.Channel,
        reminder.DueAt,
        reminder.IsCompleted,
        reminder.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapPut("/api/v1/notifications/reminders/{reminderId:guid}/complete", async (Guid reminderId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var reminder = await db.LearningReminders.FirstOrDefaultAsync(item => item.Id == reminderId && item.OwnerId == userId);
    if (reminder is null)
    {
        return Results.NotFound();
    }

    reminder.IsCompleted = true;
    await db.SaveChangesAsync();

    return Results.Ok(new LearningReminderDto(
        reminder.Id,
        reminder.Title,
        reminder.Note,
        reminder.Channel,
        reminder.DueAt,
        reminder.IsCompleted,
        reminder.CreatedAt));
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/notifications/reminders/{reminderId:guid}/send-email", async (Guid reminderId, ClaimsPrincipal principal, LearnOsDbContext db, SmtpNotificationService mailer) =>
{
    var userId = GetCurrentUserId(principal);
    var reminder = await db.LearningReminders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == reminderId && item.OwnerId == userId);
    if (reminder is null)
    {
        return Results.NotFound();
    }

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
    if (user is null)
    {
        return Results.NotFound();
    }

    try
    {
        await mailer.SendReminderAsync(user.Email, user.DisplayName, reminder);
        return Results.Ok(new { status = "Sent", channel = "Email", reminderId });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization("StudentOrTeacher");

app.MapDelete("/api/v1/notifications/reminders/{reminderId:guid}", async (Guid reminderId, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var reminder = await db.LearningReminders.FirstOrDefaultAsync(item => item.Id == reminderId && item.OwnerId == userId);
    if (reminder is null)
    {
        return Results.NotFound();
    }

    db.LearningReminders.Remove(reminder);
    await db.SaveChangesAsync();

    return Results.NoContent();
}).RequireAuthorization("StudentOrTeacher");

app.MapPost("/api/v1/roadmaps", async (RoadmapRequest request, ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    if (string.IsNullOrWhiteSpace(request.Goal))
    {
        return Results.BadRequest(new { error = "Goal is required." });
    }

    var roadmap = new[]
    {
        new RoadmapItem { Id = Guid.NewGuid(), OwnerId = userId, Goal = request.Goal.Trim(), Week = "Week 1", Title = "Foundation scan", Detail = $"Assess current level for {request.Goal.Trim()}.", State = "Planned", CreatedAt = DateTime.UtcNow },
        new RoadmapItem { Id = Guid.NewGuid(), OwnerId = userId, Goal = request.Goal.Trim(), Week = "Week 2", Title = "Core practice loop", Detail = "Daily review, adaptive quiz, and focus sessions.", State = "Planned", CreatedAt = DateTime.UtcNow },
        new RoadmapItem { Id = Guid.NewGuid(), OwnerId = userId, Goal = request.Goal.Trim(), Week = "Week 3", Title = "Weak topic repair", Detail = "Use tutor explanations and create targeted drills.", State = "Planned", CreatedAt = DateTime.UtcNow },
        new RoadmapItem { Id = Guid.NewGuid(), OwnerId = userId, Goal = request.Goal.Trim(), Week = "Week 4", Title = "Exam simulation", Detail = "Timed mixed quiz, report, and final cheat sheet.", State = "Planned", CreatedAt = DateTime.UtcNow }
    };

    db.RoadmapItems.AddRange(roadmap);
    await db.SaveChangesAsync();

    return Results.Created("/api/v1/roadmaps", roadmap.Select(ToRoadmapDto));
}).RequireAuthorization("StudentOrTeacher");

app.MapGet("/api/v1/roadmaps", async (ClaimsPrincipal principal, LearnOsDbContext db) =>
{
    var userId = GetCurrentUserId(principal);
    var items = await db.RoadmapItems
        .AsNoTracking()
        .Where(item => item.OwnerId == userId)
        .OrderByDescending(item => item.CreatedAt)
        .ThenBy(item => item.Week)
        .ToListAsync();

    return Results.Ok(items.Select(ToRoadmapDto));
}).RequireAuthorization("StudentOrTeacher");

app.Run();

static FlashcardDto ToFlashcardDto(Flashcard card) => new(
    card.Id,
    card.Tag,
    card.Question,
    card.Answer,
    card.Hint,
    card.Difficulty,
    card.ConfidenceScore,
    card.DueAt,
    card.Repetition,
    card.EaseFactor,
    card.IntervalDays,
    card.ReviewCount);

static QuizDto ToQuizDto(Quiz quiz) => new(
    quiz.Id,
    quiz.Title,
    quiz.Difficulty,
    quiz.Questions
        .OrderBy(question => question.SortOrder)
        .Select(question => new QuizQuestionDto(
            question.Id,
            question.Type,
            question.Prompt,
            string.IsNullOrWhiteSpace(question.OptionsJson)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(question.OptionsJson, JsonOptionsProvider.Options) ?? Array.Empty<string>(),
            question.CorrectAnswer,
            question.Explanation)));

static List<QuizQuestion> BuildQuizQuestions(List<Flashcard> cards)
{
    var questions = new List<QuizQuestion>();
    for (var index = 0; index < cards.Count; index++)
    {
        var card = cards[index];
        var type = index % 3 == 0 ? "MultipleChoice" : index % 3 == 1 ? "TrueFalse" : "FillBlank";
        var options = type == "MultipleChoice"
            ? BuildMultipleChoiceOptions(cards, card)
            : type == "TrueFalse"
                ? ["True", "False"]
                : Array.Empty<string>();

        questions.Add(new QuizQuestion
        {
            Id = Guid.NewGuid(),
            Type = type,
            Prompt = type switch
            {
                "TrueFalse" => $"{card.Question} Answer: {card.Answer}",
                "FillBlank" => card.Question,
                _ => card.Question
            },
            OptionsJson = JsonSerializer.Serialize(options, JsonOptionsProvider.Options),
            CorrectAnswer = type == "TrueFalse" ? "True" : card.Answer,
            Explanation = card.Hint,
            SortOrder = index
        });
    }

    return questions;
}

static string[] BuildMultipleChoiceOptions(List<Flashcard> cards, Flashcard correctCard)
{
    var distractors = cards
        .Where(card => card.Id != correctCard.Id)
        .Select(card => card.Answer)
        .Where(answer => !string.IsNullOrWhiteSpace(answer))
        .Distinct()
        .Take(3)
        .ToList();

    while (distractors.Count < 3)
    {
        distractors.Add($"Not: {correctCard.Tag} option {distractors.Count + 1}");
    }

    return distractors.Append(correctCard.Answer).OrderBy(_ => Guid.NewGuid()).ToArray();
}

static bool IsAnswerCorrect(string answer, string expected)
{
    return string.Equals(NormalizeAnswer(answer), NormalizeAnswer(expected), StringComparison.OrdinalIgnoreCase);
}

static string NormalizeAnswer(string value)
{
    return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

static Guid GetCurrentUserId(ClaimsPrincipal principal)
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(userId, out var id) ? id : Guid.Empty;
}

static async Task<GamificationProfile> GetOrCreateGamificationProfile(LearnOsDbContext db, Guid userId)
{
    var profile = await db.GamificationProfiles.FirstOrDefaultAsync(item => item.UserId == userId);
    if (profile is not null)
    {
        return profile;
    }

    profile = new GamificationProfile
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Energy = 100,
        League = "Bronze",
        CurrentStreak = 0,
        LastActivityDate = null
    };
    db.GamificationProfiles.Add(profile);
    return profile;
}

static void AwardLearningReward(GamificationProfile profile, int xp, int coins)
{
    profile.Xp += Math.Max(0, xp);
    profile.Coins += Math.Max(0, coins);
    profile.Energy = Math.Max(0, profile.Energy - 5);

    var today = DateTime.UtcNow.Date;
    if (profile.LastActivityDate is null)
    {
        profile.CurrentStreak = 1;
    }
    else if (profile.LastActivityDate.Value.Date == today.AddDays(-1))
    {
        profile.CurrentStreak++;
    }
    else if (profile.LastActivityDate.Value.Date < today.AddDays(-1))
    {
        profile.CurrentStreak = 1;
    }

    profile.LastActivityDate = today;
    profile.League = profile.Xp switch
    {
        >= 2000 => "Diamond",
        >= 1000 => "Gold",
        >= 400 => "Silver",
        _ => "Bronze"
    };
}

static GamificationDto ToGamificationDto(GamificationProfile profile) => new(
    profile.Xp,
    profile.Coins,
    profile.Energy,
    profile.CurrentStreak,
    profile.League);

static RoadmapItemDto ToRoadmapDto(RoadmapItem item) => new(item.Week, item.Title, item.Detail, item.State);

static AssignmentSubmissionDto ToAssignmentSubmissionDto(AssignmentSubmission submission) => new(
    submission.Id,
    submission.AssignmentId,
    submission.StudentId,
    submission.Content,
    submission.Score,
    submission.Feedback,
    submission.SubmittedAt,
    submission.GradedAt);

static async Task<bool> IsCollaborationRoomMember(LearnOsDbContext db, Guid roomId, Guid userId)
{
    return await db.CollaborationRoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId == userId);
}

static async Task<string> CreateUniqueJoinCode(LearnOsDbContext db)
{
    string code;
    do
    {
        code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    }
    while (await db.CollaborationRooms.AnyAsync(room => room.JoinCode == code));

    return code;
}

static async Task<string> CreateUniqueGuardianInviteCode(LearnOsDbContext db)
{
    string code;
    do
    {
        code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
    }
    while (await db.GuardianInvitations.AnyAsync(invitation => invitation.Code == code && invitation.ExpiresAt > DateTime.UtcNow));

    return code;
}

static async Task<string> CreateUniqueCourseJoinCode(LearnOsDbContext db)
{
    string code;
    do
    {
        code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
    }
    while (await db.Courses.AnyAsync(course => course.JoinCode == code));

    return code;
}

static async Task<bool> CanAccessCourse(LearnOsDbContext db, Guid courseId, Guid userId, ClaimsPrincipal principal)
{
    return await IsCourseTeacher(db, courseId, userId, principal)
        || await db.CourseEnrollments.AnyAsync(enrollment => enrollment.CourseId == courseId && enrollment.StudentId == userId);
}

static async Task<bool> IsCourseTeacher(LearnOsDbContext db, Guid courseId, Guid userId, ClaimsPrincipal principal)
{
    if (principal.IsInRole("Admin"))
    {
        return await db.Courses.AnyAsync(course => course.Id == courseId);
    }

    return await db.Courses.AnyAsync(course => course.Id == courseId && course.TeacherId == userId);
}

static string NormalizeJoinCode(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}

static async Task UpsertSubject(LearnOsDbContext db, Guid ownerId, string name, int delta)
{
    var subjectName = string.IsNullOrWhiteSpace(name) ? "General" : name.Trim();
    var subject = await db.Subjects.FirstOrDefaultAsync(x => x.OwnerId == ownerId && x.Name == subjectName);

    if (subject is null)
    {
        db.Subjects.Add(new Subject
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = subjectName,
            Progress = Math.Clamp(delta, 0, 100)
        });
        return;
    }

    subject.Progress = Math.Clamp(subject.Progress + delta, 0, 100);
}

static string ExtractPdfText(string path)
{
    var builder = new StringBuilder();
    using var document = PdfDocument.Open(path);

    foreach (var page in document.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    return builder.ToString().Trim();
}

static void ApplySm2(Flashcard card, int quality)
{
    card.LastReviewedAt = DateTime.UtcNow;

    if (quality < 3)
    {
        card.Repetition = 0;
        card.IntervalDays = 1;
        card.ForgetCount++;
        card.DueAt = DateTime.UtcNow.AddDays(1);
        return;
    }

    card.Repetition++;
    card.IntervalDays = card.Repetition switch
    {
        1 => 1,
        2 => 6,
        _ => Math.Max(1, (int)Math.Round(card.IntervalDays * card.EaseFactor))
    };

    var easeAdjustment = 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
    card.EaseFactor = Math.Max(1.3, Math.Round(card.EaseFactor + easeAdjustment, 2));
    card.DueAt = DateTime.UtcNow.AddDays(card.IntervalDays);
}

static AuthResponse BuildAuthResponse(User user, string refreshToken, IConfiguration config)
{
    return new AuthResponse(
        AccessToken: CreateAccessToken(user, config),
        RefreshToken: refreshToken,
        User: new UserDto(user.Id, user.Email, user.DisplayName, user.Role, user.EmailVerified, user.MfaEnabled));
}

static string CreateAccessToken(User user, IConfiguration config)
{
    var jwt = config.GetSection("Jwt");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(jwt.GetValue("AccessTokenMinutes", 30));

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.DisplayName),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var token = new JwtSecurityToken(
        issuer: jwt["Issuer"],
        audience: jwt["Audience"],
        claims: claims,
        expires: expires,
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static RefreshToken CreateRefreshToken(Guid userId, IConfiguration config)
{
    var tokenBytes = RandomNumberGenerator.GetBytes(64);
    return new RefreshToken
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Token = Convert.ToBase64String(tokenBytes),
        ExpiresAt = DateTime.UtcNow.AddDays(config.GetSection("Jwt").GetValue("RefreshTokenDays", 14)),
        CreatedAt = DateTime.UtcNow
    };
}

static string GenerateSecureToken()
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        .Replace('+', '-')
        .Replace('/', '_')
        .TrimEnd('=');
}

static string HashToken(string token)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
    return Convert.ToHexString(hash);
}

static class Totp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        var bits = new StringBuilder();
        foreach (var value in bytes)
        {
            bits.Append(Convert.ToString(value, 2).PadLeft(8, '0'));
        }

        var output = new StringBuilder();
        for (var index = 0; index < bits.Length; index += 5)
        {
            var chunk = bits.ToString(index, Math.Min(5, bits.Length - index)).PadRight(5, '0');
            output.Append(Alphabet[Convert.ToInt32(chunk, 2)]);
        }

        return output.ToString();
    }

    public static string GetCode(string secret)
    {
        return ComputeCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
    }

    public static bool Verify(string secret, string? code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (ComputeCode(secret, timeStep + offset) == normalized)
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeCode(string secret, long timeStep)
    {
        var key = DecodeBase32(secret);
        var counter = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(timeStep));
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string value)
    {
        var bits = new StringBuilder();
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index >= 0)
            {
                bits.Append(Convert.ToString(index, 2).PadLeft(5, '0'));
            }
        }

        var bytes = new List<byte>();
        for (var index = 0; index + 8 <= bits.Length; index += 8)
        {
            bytes.Add(Convert.ToByte(bits.ToString(index, 8), 2));
        }

        return bytes.ToArray();
    }
}

static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

sealed class LearnOsDbContext(DbContextOptions<LearnOsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthActionToken> AuthActionTokens => Set<AuthActionToken>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<RoadmapItem> RoadmapItems => Set<RoadmapItem>();
    public DbSet<LearningDocument> LearningDocuments => Set<LearningDocument>();
    public DbSet<GamificationProfile> GamificationProfiles => Set<GamificationProfile>();
    public DbSet<CollaborationRoom> CollaborationRooms => Set<CollaborationRoom>();
    public DbSet<CollaborationRoomMember> CollaborationRoomMembers => Set<CollaborationRoomMember>();
    public DbSet<CollaborationMessage> CollaborationMessages => Set<CollaborationMessage>();
    public DbSet<LearningReminder> LearningReminders => Set<LearningReminder>();
    public DbSet<GuardianInvitation> GuardianInvitations => Set<GuardianInvitation>();
    public DbSet<GuardianLink> GuardianLinks => Set<GuardianLink>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentSubmission> AssignmentSubmissions => Set<AssignmentSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();
        modelBuilder.Entity<AuthActionToken>().HasIndex(token => new { token.Purpose, token.TokenHash });
        modelBuilder.Entity<Subject>().HasIndex(subject => new { subject.OwnerId, subject.Name });
        modelBuilder.Entity<Flashcard>().HasIndex(card => card.OwnerId);
        modelBuilder.Entity<LearningDocument>().HasIndex(document => document.OwnerId);
        modelBuilder.Entity<Quiz>().HasIndex(quiz => quiz.OwnerId);
        modelBuilder.Entity<QuizAttempt>().HasIndex(attempt => attempt.OwnerId);
        modelBuilder.Entity<StudySession>().HasIndex(session => session.OwnerId);
        modelBuilder.Entity<RoadmapItem>().HasIndex(item => item.OwnerId);
        modelBuilder.Entity<AiMessage>().HasIndex(message => new { message.UserId, message.ConversationId });
        modelBuilder.Entity<CollaborationRoom>().HasIndex(room => room.JoinCode).IsUnique();
        modelBuilder.Entity<CollaborationRoomMember>().HasIndex(member => new { member.RoomId, member.UserId }).IsUnique();
        modelBuilder.Entity<CollaborationMessage>().HasIndex(message => new { message.RoomId, message.CreatedAt });
        modelBuilder.Entity<LearningReminder>().HasIndex(reminder => new { reminder.OwnerId, reminder.DueAt });
        modelBuilder.Entity<GuardianInvitation>().HasIndex(invitation => invitation.Code).IsUnique();
        modelBuilder.Entity<GuardianLink>().HasIndex(link => new { link.ParentId, link.StudentId }).IsUnique();
        modelBuilder.Entity<Course>().HasIndex(course => course.JoinCode).IsUnique();
        modelBuilder.Entity<Course>().HasIndex(course => course.TeacherId);
        modelBuilder.Entity<CourseEnrollment>().HasIndex(enrollment => new { enrollment.CourseId, enrollment.StudentId }).IsUnique();
        modelBuilder.Entity<Assignment>().HasIndex(assignment => assignment.CourseId);
        modelBuilder.Entity<AssignmentSubmission>().HasIndex(submission => new { submission.AssignmentId, submission.StudentId }).IsUnique();
        modelBuilder.Entity<Flashcard>().Property(card => card.EaseFactor).HasDefaultValue(2.5);
        modelBuilder.Entity<GamificationProfile>().HasIndex(profile => profile.UserId).IsUnique();
        modelBuilder.Entity<Quiz>()
            .HasMany(quiz => quiz.Questions)
            .WithOne(question => question.Quiz)
            .HasForeignKey(question => question.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RefreshToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId);
        modelBuilder.Entity<AuthActionToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId);
        modelBuilder.Entity<CollaborationRoom>()
            .HasMany(room => room.Members)
            .WithOne(member => member.Room)
            .HasForeignKey(member => member.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CollaborationRoom>()
            .HasMany(room => room.Messages)
            .WithOne(message => message.Room)
            .HasForeignKey(message => message.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Course>()
            .HasMany(course => course.Enrollments)
            .WithOne(enrollment => enrollment.Course)
            .HasForeignKey(enrollment => enrollment.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Course>()
            .HasMany(course => course.Assignments)
            .WithOne(assignment => assignment.Course)
            .HasForeignKey(assignment => assignment.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Assignment>()
            .HasMany(assignment => assignment.Submissions)
            .WithOne(submission => submission.Assignment)
            .HasForeignKey(submission => submission.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public bool EmailVerified { get; set; }
    public bool MfaEnabled { get; set; }
    public string MfaSecret { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

sealed class AuthActionToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

sealed class Subject
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Progress { get; set; }
}

sealed class Flashcard
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public int ConfidenceScore { get; set; }
    public DateTime DueAt { get; set; }
    public int ReviewCount { get; set; }
    public int Repetition { get; set; }
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; }
    public int ForgetCount { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

sealed class StudySession
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public int FocusMinutes { get; set; }
    public DateTime StartedAt { get; set; }
}

sealed class Quiz
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Mixed";
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<QuizQuestion> Questions { get; set; } = [];
}

sealed class QuizQuestion
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

sealed class QuizAttempt
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? QuizId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Accuracy { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime StartedAt { get; set; }
}

sealed class AiMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

sealed class RoadmapItem
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string Week { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

sealed class LearningDocument
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public string Status { get; set; } = "Uploaded";
    public DateTime CreatedAt { get; set; }
}

sealed class GamificationProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Xp { get; set; }
    public int Coins { get; set; }
    public int Energy { get; set; } = 100;
    public int CurrentStreak { get; set; }
    public string League { get; set; } = "Bronze";
    public DateTime? LastActivityDate { get; set; }
}

sealed class CollaborationRoom
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CollaborationRoomMember> Members { get; set; } = [];
    public List<CollaborationMessage> Messages { get; set; } = [];
}

sealed class CollaborationRoomMember
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public CollaborationRoom Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; }
}

sealed class CollaborationMessage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public CollaborationRoom Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

sealed class LearningReminder
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Channel { get; set; } = "InApp";
    public DateTime DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

sealed class GuardianInvitation
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

sealed class GuardianLink
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

sealed class Course
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CourseEnrollment> Enrollments { get; set; } = [];
    public List<Assignment> Assignments { get; set; } = [];
}

sealed class CourseEnrollment
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; }
}

sealed class Assignment
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AssignmentSubmission> Submissions { get; set; } = [];
}

sealed class AssignmentSubmission
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
}

static class JsonOptionsProvider
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

record SubjectProgressDto(string Name, int Progress);

record RegisterRequest(string Email, string Password, string? DisplayName, string? Role);

record LoginRequest(string Email, string Password, string? MfaCode);

record RefreshRequest(string RefreshToken);

record EmailRequest(string Email);

record ConfirmTokenRequest(string Token);

record ResetPasswordRequest(string Token, string NewPassword);

record MfaSetupDto(string Secret, string CurrentCode);

record MfaCodeRequest(string Code);

record SessionDto(Guid Id, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt);

record UserDto(Guid Id, string Email, string DisplayName, string Role, bool EmailVerified, bool MfaEnabled);

record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

record GamificationDto(int Xp, int Coins, int Energy, int CurrentStreak, string League);

record StudentDashboardDto(
    double StudyHours,
    int StudyStreakDays,
    int CompletionRate,
    int DueFlashcards,
    string TodayQuiz,
    string AiRecommendation,
    IEnumerable<SubjectProgressDto> Subjects);

record FlashcardDto(
    Guid Id,
    string Tag,
    string Question,
    string Answer,
    string Hint,
    string Difficulty,
    int ConfidenceScore,
    DateTime DueAt,
    int Repetition,
    double EaseFactor,
    int IntervalDays,
    int ReviewCount);

record CreateFlashcardRequest(string Question, string Answer, string? Hint, string? Difficulty, string? Tag);

record UpdateFlashcardRequest(string Question, string Answer, string? Hint, string? Difficulty, string? Tag);

record GenerateFlashcardsRequest(string Topic, string Content, string? Difficulty);

record GeneratedFlashcard(string Question, string Answer, string Hint, string Difficulty, int ConfidenceScore);

record DocumentDto(Guid Id, string Title, string FileName, string ContentType, string Status, int TextLength, DateTime CreatedAt);

record DocumentDetailDto(Guid Id, string Title, string FileName, string ContentType, string Status, string TextContent, DateTime CreatedAt);

record GenerateDocumentFlashcardsRequest(string? Topic, string? Difficulty);

record YouTubeIngestRequest(string Url, string? Title, string? Language);

record ReviewRequest(int Quality, int ResponseTimeSeconds);

record QuizDto(Guid Id, string Title, string Difficulty, IEnumerable<QuizQuestionDto> Questions);

record QuizQuestionDto(Guid Id, string Type, string Prompt, IEnumerable<string> Options, string CorrectAnswer, string Explanation);

record GenerateQuizRequest(string? Title, string? Difficulty, int QuestionCount);

sealed class QuizAttemptRequest
{
    public Guid? QuizId { get; set; }
    public string Title { get; set; } = "Quiz attempt";
    public int Accuracy { get; set; }
    public int DurationSeconds { get; set; }
    public Dictionary<Guid, string> Answers { get; set; } = [];
}

record StudySeriesPointDto(string Day, int Minutes, int Accuracy);

record AiChatRequest(Guid? ConversationId, string Message);

record AiChatResponse(Guid ConversationId, string Role, string Content, string Model, int ConfidenceScore);

record CollaborationRoomDto(Guid Id, string Name, string Topic, string JoinCode, string Role, DateTime CreatedAt, int MemberCount, int MessageCount);

record CreateCollaborationRoomRequest(string Name, string? Topic);

record JoinCollaborationRoomRequest(string? JoinCode);

record CollaborationMessageDto(Guid Id, Guid RoomId, Guid UserId, string DisplayName, string Content, DateTime CreatedAt);

record CreateCollaborationMessageRequest(string Content);

record LearningReminderDto(Guid Id, string Title, string Note, string Channel, DateTime DueAt, bool IsCompleted, DateTime CreatedAt);

record CreateLearningReminderRequest(string Title, string? Note, string? Channel, DateTime DueAt);

record GuardianInvitationDto(Guid Id, string Code, DateTime ExpiresAt, DateTime CreatedAt);

record JoinGuardianLinkRequest(string? Code);

record ParentStudentDto(Guid Id, string DisplayName, string Email, DateTime LinkedAt);

record ParentStudentDashboardDto(Guid StudentId, string DisplayName, double StudyHours, int StudyDays, int DueFlashcards, int QuizAttempts, int Accuracy, int PendingReminders);

record CourseDto(Guid Id, string Name, string Subject, string JoinCode, Guid TeacherId, string Role, DateTime CreatedAt);

record CreateCourseRequest(string Name, string? Subject);

record JoinCourseRequest(string? Code);

record AssignmentDto(Guid Id, Guid CourseId, string Title, string Instructions, DateTime DueAt, DateTime CreatedAt);

record CreateAssignmentRequest(string Title, string? Instructions, DateTime DueAt);

record AssignmentSubmissionDto(Guid Id, Guid AssignmentId, Guid StudentId, string Content, int? Score, string Feedback, DateTime SubmittedAt, DateTime? GradedAt);

record CreateAssignmentSubmissionRequest(string Content);

record RoadmapRequest(string Goal, string Level, DateOnly? Deadline, int WeeklyHours);

record RoadmapItemDto(string Week, string Title, string Detail, string State);

sealed class SmtpNotificationService(IConfiguration config)
{
    public async Task SendReminderAsync(string recipientEmail, string displayName, LearningReminder reminder)
    {
        var host = config["Smtp:Host"];
        var from = config["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Host and Smtp:From before sending email notifications.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, "LearnOS AI"),
            Subject = $"LearnOS reminder: {reminder.Title}",
            Body = $"""
Hi {displayName},

This is your LearnOS AI study reminder.

Title: {reminder.Title}
Due: {reminder.DueAt:u}
Note: {(string.IsNullOrWhiteSpace(reminder.Note) ? "No note." : reminder.Note)}

Keep going.
""",
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(host, config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = config.GetValue("Smtp:EnableSsl", true)
        };

        var username = config["Smtp:Username"];
        var password = config["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(message);
    }
}

sealed class OpenAiLearningService(HttpClient httpClient, IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? config["OpenAI:ApiKey"]
        ?? string.Empty;
    private readonly string _model = config["OpenAI:Model"] ?? "gpt-5";
    private readonly string _transcriptionModel = config["OpenAI:TranscriptionModel"] ?? "gpt-4o-mini-transcribe";
    private readonly string _baseUrl = (config["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');

    public async Task<string> AnswerTutorQuestionAsync(string question)
    {
        var prompt = $"""
You are LearnOS AI, a concise learning tutor.
Answer the student's question with:
1. A simple explanation.
2. One concrete example.
3. One next study action.

Student question:
{question}
""";

        return await CreateTextResponseAsync(prompt);
    }

    public async Task<List<GeneratedFlashcard>> GenerateFlashcardsAsync(string topic, string content, string difficulty)
    {
        var prompt = $"""
Create 5 high-quality study flashcards for topic: {topic}.
Difficulty target: {difficulty}.

Return only valid JSON. No markdown. The JSON must be an array of objects with:
question, answer, hint, difficulty, confidenceScore.

Source material:
{content}
""";

        var text = await CreateTextResponseAsync(prompt);
        var json = ExtractJsonArray(text);
        return JsonSerializer.Deserialize<List<GeneratedFlashcard>>(json, JsonOptions) ?? [];
    }

    public async Task<string> ExtractImageTextAsync(string path, string contentType)
    {
        EnsureApiKey();
        var mediaType = string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType;
        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(path));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new
        {
            model = _model,
            input = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = "Extract all readable study text from this image. Preserve formulas, bullet points, and headings. Return only the extracted text."
                        },
                        new
                        {
                            type = "input_image",
                            image_url = $"data:{mediaType};base64,{base64}"
                        }
                    }
                }
            }
        });

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI image OCR error {(int)response.StatusCode}: {body}");
        }

        return ExtractOutputText(body);
    }

    public async Task<string> TranscribeAudioAsync(string path, string fileName)
    {
        EnsureApiKey();
        await using var stream = File.OpenRead(path);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(_transcriptionModel), "model");
        content.Add(new StringContent("text"), "response_format");
        request.Content = content;

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI transcription error {(int)response.StatusCode}: {body}");
        }

        return body.Trim();
    }

    private async Task<string> CreateTextResponseAsync(string input)
    {
        EnsureApiKey();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(new
        {
            model = _model,
            input
        });

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API error {(int)response.StatusCode}: {body}");
        }

        return ExtractOutputText(body);
    }

    private void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY is not configured. Set the environment variable or OpenAI:ApiKey in appsettings.");
        }
    }

    private static string ExtractOutputText(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var output))
        {
            var builder = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content))
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text))
                    {
                        builder.AppendLine(text.GetString());
                    }
                }
            }

            return builder.ToString().Trim();
        }

        return body;
    }

    private static string ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[', StringComparison.Ordinal);
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("OpenAI response did not contain a JSON array.");
        }

        return text[start..(end + 1)];
    }
}
