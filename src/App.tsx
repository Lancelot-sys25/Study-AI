import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  BarChart3,
  Bell,
  BookOpen,
  Bot,
  Brain,
  CalendarDays,
  CheckCircle2,
  Clock3,
  FileText,
  Flame,
  GraduationCap,
  LayoutDashboard,
  Lightbulb,
  MessageSquareText,
  Moon,
  Play,
  Search,
  ShieldCheck,
  Sparkles,
  Target,
  Trophy,
  UploadCloud,
  Users,
  Zap,
} from 'lucide-react'
import './App.css'

const API_BASE = 'http://127.0.0.1:5000/api/v1'

type TabKey = 'dashboard' | 'documents' | 'flashcards' | 'quiz' | 'roadmap' | 'classroom' | 'collaboration' | 'planner' | 'analytics'

type DashboardData = {
  studyHours: number
  studyStreakDays: number
  completionRate: number
  dueFlashcards: number
  todayQuiz: string
  aiRecommendation: string
  subjects: SubjectProgress[]
}

type SubjectProgress = {
  name: string
  progress: number
}

type Flashcard = {
  id: string
  question: string
  answer: string
  hint: string
  difficulty: 'Easy' | 'Medium' | 'Hard' | string
  tag: string
  confidenceScore: number
  dueAt: string
  repetition: number
  easeFactor: number
  intervalDays: number
  reviewCount: number
}

type StudyPoint = {
  day: string
  minutes: number
  accuracy: number
}

type Quiz = {
  id: string
  title: string
  difficulty: string
  questions: Array<{
    id: string
    type: string
    prompt: string
    options: string[]
    correctAnswer: string
    explanation: string
  }>
}

type RoadmapItem = {
  week: string
  title: string
  detail: string
  state: string
}

type ChatMessage = {
  role: string
  text: string
}

type LearningDocument = {
  id: string
  title: string
  fileName: string
  contentType: string
  status: string
  textLength: number
  createdAt: string
}

type CollaborationRoom = {
  id: string
  name: string
  topic: string
  joinCode: string
  role: string
  createdAt: string
  memberCount: number
  messageCount: number
}

type CollaborationMessage = {
  id: string
  roomId: string
  userId: string
  displayName: string
  content: string
  createdAt: string
}

type LearningReminder = {
  id: string
  title: string
  note: string
  channel: string
  dueAt: string
  isCompleted: boolean
  createdAt: string
}

type Gamification = {
  xp: number
  coins: number
  energy: number
  currentStreak: number
  league: string
}

type TeacherDashboard = {
  classes: number
  totalAttempts: number
  activeLearnersProxy: number
  studentsAtRisk: number
  averageAccuracy: number
  weakTopics: Array<{ name: string; progress: number }>
  message: string
}

type AdminDashboard = {
  users: number
  flashcards: number
  quizAttempts: number
  aiMessages: number
}

type ParentStudent = {
  id: string
  displayName: string
  email: string
  linkedAt: string
}

type ParentStudentDashboard = {
  studentId: string
  displayName: string
  studyHours: number
  studyDays: number
  dueFlashcards: number
  quizAttempts: number
  accuracy: number
  pendingReminders: number
}

type Course = {
  id: string
  name: string
  subject: string
  joinCode: string
  teacherId: string
  role: string
  createdAt: string
}

type Assignment = {
  id: string
  courseId: string
  title: string
  instructions: string
  rubric: string
  dueAt: string
  createdAt: string
}

type AssignmentSubmission = {
  id: string
  assignmentId: string
  studentId: string
  content: string
  score?: number
  feedback: string
  submittedAt: string
  gradedAt?: string
}

type AuthUser = {
  id: string
  email: string
  displayName: string
  role: string
  emailVerified?: boolean
  mfaEnabled?: boolean
}

type AuthState = {
  accessToken: string
  refreshToken: string
  user: AuthUser
}

const emptyDashboard: DashboardData = {
  studyHours: 0,
  studyStreakDays: 0,
  completionRate: 0,
  dueFlashcards: 0,
  todayQuiz: 'Not started',
  aiRecommendation: 'Create your first flashcard to unlock recommendations.',
  subjects: [],
}

const tabs: Array<{ key: TabKey; label: string; icon: typeof LayoutDashboard }> = [
  { key: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { key: 'documents', label: 'Documents', icon: FileText },
  { key: 'flashcards', label: 'Flashcards', icon: Brain },
  { key: 'quiz', label: 'Quiz Engine', icon: Trophy },
  { key: 'roadmap', label: 'Roadmap', icon: Target },
  { key: 'classroom', label: 'Classroom', icon: BookOpen },
  { key: 'collaboration', label: 'Collaboration', icon: Users },
  { key: 'planner', label: 'Planner', icon: CalendarDays },
  { key: 'analytics', label: 'Analytics', icon: BarChart3 },
]

const AUTH_STORAGE_KEY = 'learnos.auth'
const OFFLINE_REMINDER_QUEUE_KEY = 'learnos.offline.reminders'

type QueuedReminder = {
  id: string
  title: string
  note: string
  channel: string
  dueAt: string
  queuedAt: string
}

function loadAuth(): AuthState | null {
  const raw = localStorage.getItem(AUTH_STORAGE_KEY)
  if (!raw) return null

  try {
    return JSON.parse(raw) as AuthState
  } catch {
    localStorage.removeItem(AUTH_STORAGE_KEY)
    return null
  }
}

function loadQueuedReminders(): QueuedReminder[] {
  const raw = localStorage.getItem(OFFLINE_REMINDER_QUEUE_KEY)
  if (!raw) return []

  try {
    return JSON.parse(raw) as QueuedReminder[]
  } catch {
    localStorage.removeItem(OFFLINE_REMINDER_QUEUE_KEY)
    return []
  }
}

function saveQueuedReminders(items: QueuedReminder[]) {
  localStorage.setItem(OFFLINE_REMINDER_QUEUE_KEY, JSON.stringify(items))
}

function queueReminder(reminder: Omit<QueuedReminder, 'id' | 'queuedAt'>) {
  const current = loadQueuedReminders()
  saveQueuedReminders([
    ...current,
    {
      ...reminder,
      id: crypto.randomUUID(),
      queuedAt: new Date().toISOString(),
    },
  ])
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const auth = loadAuth()
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(auth?.accessToken ? { Authorization: `Bearer ${auth.accessToken}` } : {}),
      ...init?.headers,
    },
    ...init,
  })

  if (!response.ok) {
    throw new Error(`API ${response.status}: ${await response.text()}`)
  }

  return response.json() as Promise<T>
}

function App() {
  const [auth, setAuth] = useState<AuthState | null>(() => loadAuth())
  const [activeTab, setActiveTab] = useState<TabKey>('dashboard')
  const [dashboard, setDashboard] = useState<DashboardData>(emptyDashboard)
  const [flashcards, setFlashcards] = useState<Flashcard[]>([])
  const [documents, setDocuments] = useState<LearningDocument[]>([])
  const [gamification, setGamification] = useState<Gamification | null>(null)
  const [teacherDashboard, setTeacherDashboard] = useState<TeacherDashboard | null>(null)
  const [adminDashboard, setAdminDashboard] = useState<AdminDashboard | null>(null)
  const [parentStudents, setParentStudents] = useState<ParentStudent[]>([])
  const [parentDashboards, setParentDashboards] = useState<ParentStudentDashboard[]>([])
  const [courses, setCourses] = useState<Course[]>([])
  const [studySeries, setStudySeries] = useState<StudyPoint[]>([])
  const [quiz, setQuiz] = useState<Quiz | null>(null)
  const [roadmap, setRoadmap] = useState<RoadmapItem[]>([])
  const [collaborationRooms, setCollaborationRooms] = useState<CollaborationRoom[]>([])
  const [reminders, setReminders] = useState<LearningReminder[]>([])
  const [selectedCard, setSelectedCard] = useState(0)
  const [aiMessages, setAiMessages] = useState<ChatMessage[]>([])
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [question, setQuestion] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [isOnline, setIsOnline] = useState(() => navigator.onLine)
  const [offlineReminderCount, setOfflineReminderCount] = useState(() => loadQueuedReminders().length)

  const refreshData = useCallback(async () => {
    const currentAuth = loadAuth()
    if (!currentAuth) return
    setError('')
    if (currentAuth.user.role === 'Parent') {
      const students = await api<ParentStudent[]>('/parent/students')
      const dashboards = await Promise.all(
        students.map((student) => api<ParentStudentDashboard>(`/parent/students/${student.id}/dashboard`)),
      )
      setParentStudents(students)
      setParentDashboards(dashboards)
      return
    }

    const [dashboardData, cardsData, documentsData, gamificationData, seriesData, quizData, roadmapData, collaborationData, reminderData, coursesData] = await Promise.all([
      api<DashboardData>('/dashboard/student'),
      api<Flashcard[]>('/flashcards'),
      api<LearningDocument[]>('/documents'),
      api<Gamification>('/gamification/me'),
      api<StudyPoint[]>('/analytics/study-series'),
      api<Quiz>('/quizzes/today'),
      api<RoadmapItem[]>('/roadmaps'),
      api<CollaborationRoom[]>('/collaboration/rooms'),
      api<LearningReminder[]>('/notifications/reminders'),
      api<Course[]>('/courses'),
    ])

    setDashboard(dashboardData)
    setFlashcards(cardsData)
    setDocuments(documentsData)
    setGamification(gamificationData)
    setStudySeries(seriesData)
    setQuiz(quizData)
    setRoadmap(roadmapData)
    setCollaborationRooms(collaborationData)
    setReminders(reminderData)
    setCourses(coursesData)
    setSelectedCard(0)

    if (currentAuth?.user.role === 'Teacher' || currentAuth?.user.role === 'Admin') {
      setTeacherDashboard(await api<TeacherDashboard>('/dashboard/teacher'))
    }
    if (currentAuth?.user.role === 'Admin') {
      setAdminDashboard(await api<AdminDashboard>('/dashboard/admin'))
    }
  }, [])

  const syncQueuedReminders = useCallback(async () => {
    if (!navigator.onLine || !loadAuth()) return
    const queued = loadQueuedReminders()
    if (queued.length === 0) {
      setOfflineReminderCount(0)
      return
    }

    const remaining: QueuedReminder[] = []
    for (const reminder of queued) {
      try {
        await api('/notifications/reminders', {
          method: 'POST',
          body: JSON.stringify({
            title: reminder.title,
            note: reminder.note,
            channel: reminder.channel,
            dueAt: reminder.dueAt,
          }),
        })
      } catch {
        remaining.push(reminder)
      }
    }

    saveQueuedReminders(remaining)
    setOfflineReminderCount(remaining.length)
    if (remaining.length !== queued.length) {
      await refreshData()
    }
  }, [refreshData])

  useEffect(() => {
    if (!auth) {
      setIsLoading(false)
      return
    }

    syncQueuedReminders()
      .then(refreshData)
      .catch((err: Error) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [auth, refreshData, syncQueuedReminders])

  useEffect(() => {
    function handleOnline() {
      setIsOnline(true)
      syncQueuedReminders().catch((err: Error) => setError(err.message))
    }

    function handleOffline() {
      setIsOnline(false)
      setOfflineReminderCount(loadQueuedReminders().length)
    }

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [syncQueuedReminders])

  function handleAuth(nextAuth: AuthState) {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(nextAuth))
    setAuth(nextAuth)
    setIsLoading(true)
  }

  async function logout() {
    if (auth?.refreshToken) {
      await api('/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: auth.refreshToken }),
      }).catch(() => undefined)
    }

    localStorage.removeItem(AUTH_STORAGE_KEY)
    setAuth(null)
    setDashboard(emptyDashboard)
    setFlashcards([])
    setDocuments([])
    setGamification(null)
    setStudySeries([])
    setQuiz(null)
    setRoadmap([])
    setCollaborationRooms([])
    setReminders([])
    setAiMessages([])
    setTeacherDashboard(null)
    setAdminDashboard(null)
    setParentStudents([])
    setParentDashboards([])
    setCourses([])
  }

  const activeFlashcard = flashcards[selectedCard]

  async function sendQuestion() {
    if (!question.trim()) return

    const userMessage = question.trim()
    setAiMessages((current) => [...current, { role: 'You', text: userMessage }])
    setQuestion('')

    try {
      const response = await api<{
        conversationId: string
        role: string
        content: string
      }>('/ai/chat', {
        method: 'POST',
        body: JSON.stringify({ conversationId, message: userMessage }),
      })

      setConversationId(response.conversationId)
      setAiMessages((current) => [
        ...current,
        { role: response.role, text: response.content },
      ])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'AI request failed')
    }
  }

  const chartData = useMemo(
    () =>
      studySeries.length > 0
        ? studySeries
        : [{ day: 'Today', minutes: 0, accuracy: 0 }],
    [studySeries],
  )
  const pendingReminderCount = reminders.filter((reminder) => !reminder.isCompleted).length
  const totalNotificationCount = pendingReminderCount + offlineReminderCount

  if (!auth) {
    return <AuthScreen onAuthenticated={handleAuth} />
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            <GraduationCap size={25} />
          </div>
          <div>
            <strong>LearnOS AI</strong>
            <span>Real data workspace</span>
          </div>
        </div>

        <nav className="nav-list" aria-label="Main navigation">
          {tabs.map((tab) => {
            const Icon = tab.icon
            return (
              <button
                className={activeTab === tab.key ? 'nav-item active' : 'nav-item'}
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                type="button"
              >
                <Icon size={18} />
                <span>{tab.label}</span>
              </button>
            )
          })}
        </nav>

        <div className="security-panel">
          <ShieldCheck size={20} />
          <div>
            <strong>SQL Server data</strong>
            <span>LocalDB + EF Core</span>
          </div>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div>
            <span className="eyebrow">Student workspace</span>
            <h1>Personalized study cockpit</h1>
            <p className="muted">Signed in as {auth.user.displayName} ({auth.user.role})</p>
          </div>
          <div className="top-actions">
            <label className="search-box">
              <Search size={18} />
              <input placeholder="Search real saved flashcards..." />
            </label>
            <button className="icon-button" type="button" aria-label="Toggle dark mode">
              <Moon size={18} />
            </button>
            <button className="icon-button notify" type="button" aria-label="Notifications">
              <Bell size={18} />
              {totalNotificationCount > 0 && <span>{totalNotificationCount}</span>}
            </button>
            <button className="secondary-button compact" onClick={logout} type="button">
              Logout
            </button>
          </div>
        </header>

        {error && <div className="error-banner">{error}</div>}
        {!isOnline && (
          <div className="offline-banner">
            Offline mode is active. New reminders will sync when your connection returns.
          </div>
        )}

        <section className="hero-strip">
          <div className="hero-copy">
            <span className="eyebrow">AI recommendation from database</span>
            <h2>{isLoading ? 'Loading your learning data...' : dashboard.aiRecommendation}</h2>
            <p>
              Dashboard metrics now come from backend tables. Create cards, review them,
              ask the tutor, and your data persists in SQL Server LocalDB.
            </p>
            <div className="hero-actions">
              <button className="primary-button" onClick={() => setActiveTab('flashcards')} type="button">
                <Play size={18} />
                Add study data
              </button>
              <button className="secondary-button" onClick={refreshData} type="button">
                <UploadCloud size={18} />
                Refresh API data
              </button>
            </div>
          </div>
          <div className="learning-visual" aria-hidden="true">
            <div className="orbital one">
              <Brain size={26} />
            </div>
            <div className="orbital two">
              <BookOpen size={24} />
            </div>
            <div className="orbital three">
              <Sparkles size={24} />
            </div>
            <div className="core-visual">
              <Zap size={34} />
              <strong>{dashboard.completionRate}%</strong>
              <span>completion</span>
            </div>
          </div>
        </section>

        {activeTab === 'dashboard' && (
          <Dashboard dashboard={dashboard} gamification={gamification} studySeries={chartData} />
        )}
        {activeTab === 'documents' && (
          <Documents documents={documents} onChanged={refreshData} />
        )}
        {activeTab === 'flashcards' && (
          <FlashcardStudio
            activeFlashcard={activeFlashcard}
            flashcards={flashcards}
            selectedCard={selectedCard}
            setSelectedCard={setSelectedCard}
            onChanged={refreshData}
          />
        )}
        {activeTab === 'quiz' && <QuizEngine quiz={quiz} onChanged={refreshData} />}
        {activeTab === 'roadmap' && <Roadmap roadmap={roadmap} onChanged={refreshData} />}
        {activeTab === 'classroom' && (
          <Classroom courses={courses} userRole={auth.user.role} onChanged={refreshData} />
        )}
        {activeTab === 'collaboration' && (
          <Collaboration rooms={collaborationRooms} onChanged={refreshData} />
        )}
        {activeTab === 'planner' && (
          <Planner
            isOnline={isOnline}
            offlineQueueCount={offlineReminderCount}
            reminders={reminders}
            onChanged={refreshData}
            onQueued={() => setOfflineReminderCount(loadQueuedReminders().length)}
          />
        )}
        {activeTab === 'analytics' && (
          <Analytics studySeries={chartData} subjects={dashboard.subjects} />
        )}
        {(auth.user.role === 'Teacher' || auth.user.role === 'Admin') && (
          <TeacherPanel dashboard={teacherDashboard} />
        )}
        {auth.user.role === 'Parent' && (
          <ParentPanel
            dashboards={parentDashboards}
            students={parentStudents}
            onChanged={refreshData}
          />
        )}
        {auth.user.role !== 'Parent' && (
          <GuardianInvitePanel />
        )}
        {auth.user.role === 'Admin' && (
          <AdminPanel dashboard={adminDashboard} />
        )}

        <section className="ai-dock">
          <div className="panel-header">
            <div>
              <span className="eyebrow">AI learning assistant</span>
              <h3>Messages are saved by the backend</h3>
            </div>
            <Bot size={22} />
          </div>
          <div className="chat-log">
            {aiMessages.length === 0 && (
              <div className="chat-message">
                <strong>AI Tutor</strong>
                <span>Ask a real question. The backend will store your message and answer.</span>
              </div>
            )}
            {aiMessages.map((message, index) => (
              <div
                className={message.role === 'You' ? 'chat-message user' : 'chat-message'}
                key={`${message.role}-${index}`}
              >
                <strong>{message.role}</strong>
                <span>{message.text}</span>
              </div>
            ))}
          </div>
          <div className="chat-input">
            <MessageSquareText size={18} />
            <input
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') sendQuestion()
              }}
              placeholder="Ask about your lesson..."
            />
            <button className="primary-button compact" onClick={sendQuestion} type="button">
              Send
            </button>
          </div>
        </section>
      </main>
    </div>
  )
}

function AuthScreen({ onAuthenticated }: { onAuthenticated: (auth: AuthState) => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('student@learnos.local')
  const [password, setPassword] = useState('Password123!')
  const [displayName, setDisplayName] = useState('Demo Student')
  const [role, setRole] = useState('Student')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function submit() {
    setError('')
    setIsSubmitting(true)

    try {
      const path = mode === 'login' ? '/auth/login' : '/auth/register'
      const body =
        mode === 'login'
          ? { email, password }
          : { email, password, displayName, role }

      const auth = await api<AuthState>(path, {
        method: 'POST',
        body: JSON.stringify(body),
      })
      onAuthenticated(auth)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-card">
        <div className="brand auth-brand">
          <div className="brand-mark">
            <GraduationCap size={25} />
          </div>
          <div>
            <strong>LearnOS AI</strong>
            <span>Secure learning workspace</span>
          </div>
        </div>

        <div>
          <span className="eyebrow">Authentication</span>
          <h1>{mode === 'login' ? 'Welcome back' : 'Create account'}</h1>
          <p className="muted">JWT, refresh token, and role are issued by the backend.</p>
        </div>

        {error && <div className="error-banner">{error}</div>}

        <div className="data-form">
          {mode === 'register' && (
            <>
              <input value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Display name" />
              <select value={role} onChange={(event) => setRole(event.target.value)}>
                <option>Student</option>
                <option>Teacher</option>
                <option>Parent</option>
                <option>Admin</option>
              </select>
            </>
          )}
          <input value={email} onChange={(event) => setEmail(event.target.value)} placeholder="Email" />
          <input value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Password" type="password" />
          <button className="primary-button" disabled={isSubmitting} onClick={submit} type="button">
            {isSubmitting ? 'Please wait...' : mode === 'login' ? 'Login' : 'Register'}
          </button>
          <button
            className="secondary-button"
            onClick={() => setMode(mode === 'login' ? 'register' : 'login')}
            type="button"
          >
            {mode === 'login' ? 'Need an account?' : 'Already have an account?'}
          </button>
        </div>
      </section>
    </main>
  )
}

function Documents({
  documents,
  onChanged,
}: {
  documents: LearningDocument[]
  onChanged: () => Promise<void>
}) {
  const [title, setTitle] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [youtubeUrl, setYoutubeUrl] = useState('')
  const [youtubeLanguage, setYoutubeLanguage] = useState('en')
  const [isUploading, setIsUploading] = useState(false)
  const [error, setError] = useState('')

  async function uploadDocument() {
    if (!file) return
    setIsUploading(true)
    setError('')

    try {
      const auth = loadAuth()
      const form = new FormData()
      form.append('file', file)
      if (title.trim()) {
        form.append('title', title.trim())
      }

      const response = await fetch(`${API_BASE}/documents`, {
        method: 'POST',
        headers: auth?.accessToken ? { Authorization: `Bearer ${auth.accessToken}` } : undefined,
        body: form,
      })

      if (!response.ok) {
        throw new Error(`Upload failed ${response.status}: ${await response.text()}`)
      }

      setTitle('')
      setFile(null)
      await onChanged()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed')
    } finally {
      setIsUploading(false)
    }
  }

  async function ingestYouTube() {
    if (!youtubeUrl.trim()) return
    setError('')
    try {
      await api('/documents/youtube', {
        method: 'POST',
        body: JSON.stringify({
          url: youtubeUrl,
          title: title || undefined,
          language: youtubeLanguage || undefined,
        }),
      })
      setTitle('')
      setYoutubeUrl('')
      await onChanged()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'YouTube ingest failed')
    }
  }

  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Documents</span>
            <h3>Upload TXT, Markdown, or PDF</h3>
          </div>
          <FileText size={22} />
        </div>
        {error && <div className="error-banner">{error}</div>}
        <div className="data-form">
          <input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Optional title" />
          <input
            accept=".txt,.md,.markdown,.pdf,.png,.jpg,.jpeg,.webp,.mp3,.wav,.m4a,.mp4,.webm"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
            type="file"
          />
          <button className="primary-button" disabled={!file || isUploading} onClick={uploadDocument} type="button">
            {isUploading ? 'Uploading...' : 'Upload document'}
          </button>
        </div>
        <div className="data-form">
          <input value={youtubeUrl} onChange={(event) => setYoutubeUrl(event.target.value)} placeholder="YouTube URL with captions" />
          <input value={youtubeLanguage} onChange={(event) => setYoutubeLanguage(event.target.value)} placeholder="Caption language, e.g. en" />
          <button className="secondary-button" disabled={!youtubeUrl.trim()} onClick={ingestYouTube} type="button">
            Import YouTube transcript
          </button>
        </div>
        <p className="muted">
          TXT, Markdown, text-based PDF, image OCR, audio/video transcription, and YouTube captions can feed later AI flashcard generation.
        </p>
      </div>
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Library</span>
            <h3>Saved documents</h3>
          </div>
          <UploadCloud size={22} />
        </div>
        <div className="deck-list">
          {documents.length === 0 && <p className="muted">No documents uploaded yet.</p>}
          {documents.map((document) => (
            <div className="document-item" key={document.id}>
              <strong>{document.title}</strong>
              <span>{document.fileName}</span>
              <em>{document.status} · {document.textLength} chars</em>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}

function Dashboard({
  dashboard,
  gamification,
  studySeries,
}: {
  dashboard: DashboardData
  gamification: Gamification | null
  studySeries: StudyPoint[]
}) {
  return (
    <>
      <section className="metric-grid">
        <Metric icon={Clock3} label="Study time" value={`${dashboard.studyHours}h`} tone="green" />
        <Metric icon={Flame} label="Study streak" value={`${dashboard.studyStreakDays} days`} tone="coral" />
        <Metric icon={CheckCircle2} label="Completion" value={`${dashboard.completionRate}%`} tone="blue" />
        <Metric icon={Brain} label="Due cards" value={`${dashboard.dueFlashcards}`} tone="violet" />
        <Metric icon={Trophy} label="XP" value={`${gamification?.xp ?? 0}`} tone="green" />
        <Metric icon={Zap} label="Energy" value={`${gamification?.energy ?? 100}`} tone="coral" />
        <Metric icon={Sparkles} label="Coins" value={`${gamification?.coins ?? 0}`} tone="blue" />
        <Metric icon={GraduationCap} label="League" value={gamification?.league ?? 'Bronze'} tone="violet" />
      </section>

      <section className="content-grid">
        <div className="panel wide">
          <div className="panel-header">
            <div>
              <span className="eyebrow">Learning curve</span>
              <h3>Stored study sessions</h3>
            </div>
            <Lightbulb size={22} />
          </div>
          <div className="chart-box">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={studySeries}>
                <defs>
                  <linearGradient id="minutes" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="5%" stopColor="#1f8a70" stopOpacity={0.45} />
                    <stop offset="95%" stopColor="#1f8a70" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="day" />
                <YAxis />
                <Tooltip />
                <Area dataKey="minutes" fill="url(#minutes)" stroke="#1f8a70" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        <SubjectPanel subjects={dashboard.subjects} />
      </section>
    </>
  )
}

function SubjectPanel({ subjects }: { subjects: SubjectProgress[] }) {
  return (
    <div className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Subjects</span>
          <h3>Progress from saved cards</h3>
        </div>
        <Sparkles size={22} />
      </div>
      <div className="subject-list">
        {subjects.length === 0 && <p className="muted">No subject data yet.</p>}
        {subjects.map((subject) => (
          <div className="subject-row" key={subject.name}>
            <span>{subject.name}</span>
            <div className="progress-track">
              <span style={{ width: `${subject.progress}%`, background: '#1f8a70' }} />
            </div>
            <strong>{subject.progress}%</strong>
          </div>
        ))}
      </div>
    </div>
  )
}

function FlashcardStudio({
  activeFlashcard,
  flashcards,
  selectedCard,
  setSelectedCard,
  onChanged,
}: {
  activeFlashcard?: Flashcard
  flashcards: Flashcard[]
  selectedCard: number
  setSelectedCard: (index: number) => void
  onChanged: () => Promise<void>
}) {
  const [topic, setTopic] = useState('')
  const [content, setContent] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  async function reviewCard(quality: number) {
    if (!activeFlashcard) return
    await api(`/reviews/${activeFlashcard.id}`, {
      method: 'POST',
      body: JSON.stringify({ quality, responseTimeSeconds: 45 }),
    })
    await onChanged()
  }

  async function deleteCard() {
    if (!activeFlashcard) return
    await api(`/flashcards/${activeFlashcard.id}`, { method: 'DELETE' })
    await onChanged()
  }

  async function generateCards() {
    if (!topic.trim() || !content.trim()) return
    setIsSaving(true)
    try {
      await api('/flashcards/generate', {
        method: 'POST',
        body: JSON.stringify({ topic, content, difficulty: 'Medium' }),
      })
      setTopic('')
      setContent('')
      await onChanged()
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <section className="content-grid">
      <div className="panel wide flashcard-panel">
        {activeFlashcard ? (
          <>
            <div className="panel-header">
              <div>
                <span className="eyebrow">{activeFlashcard.tag}</span>
                <h3>{activeFlashcard.question}</h3>
              </div>
              <span className={`difficulty ${activeFlashcard.difficulty.toLowerCase()}`}>
                {activeFlashcard.difficulty}
              </span>
            </div>
            <p className="flash-answer">{activeFlashcard.answer}</p>
            <div className="hint-line">
              <Lightbulb size={18} />
              <span>{activeFlashcard.hint || 'No hint saved.'}</span>
            </div>
            <div className="confidence">
              <span>Confidence score</span>
              <strong>{activeFlashcard.confidenceScore}%</strong>
            </div>
            <div className="review-meta">
              <span>Due {new Date(activeFlashcard.dueAt).toLocaleDateString()}</span>
              <span>Rep {activeFlashcard.repetition}</span>
              <span>Ease {activeFlashcard.easeFactor.toFixed(2)}</span>
              <span>Interval {activeFlashcard.intervalDays}d</span>
            </div>
            <div className="review-actions">
              <button className="secondary-button compact" onClick={() => reviewCard(1)} type="button">Again</button>
              <button className="secondary-button compact" onClick={() => reviewCard(3)} type="button">Hard</button>
              <button className="primary-button compact" onClick={() => reviewCard(4)} type="button">Good</button>
              <button className="primary-button compact" onClick={() => reviewCard(5)} type="button">Easy</button>
              <button className="danger-button compact" onClick={deleteCard} type="button">Delete</button>
            </div>
          </>
        ) : (
          <div className="empty-state">
            <Brain size={34} />
            <h3>No flashcards in database yet</h3>
            <p>Add content on the right. Cards will be saved to SQL Server LocalDB.</p>
          </div>
        )}
      </div>
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Create real cards</span>
            <h3>Generate from your content</h3>
          </div>
          <UploadCloud size={22} />
        </div>
        <div className="data-form">
          <input value={topic} onChange={(event) => setTopic(event.target.value)} placeholder="Topic, e.g. React hooks" />
          <textarea value={content} onChange={(event) => setContent(event.target.value)} placeholder="Paste lesson text here..." />
          <button className="primary-button" onClick={generateCards} disabled={isSaving} type="button">
            {isSaving ? 'Saving...' : 'Save generated cards'}
          </button>
        </div>
        <div className="deck-list">
          {flashcards.map((card, index) => (
            <button
              className={selectedCard === index ? 'deck-item active' : 'deck-item'}
              key={card.id}
              onClick={() => setSelectedCard(index)}
              type="button"
            >
              <span>{card.tag}</span>
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}

function QuizEngine({ quiz, onChanged }: { quiz: Quiz | null; onChanged: () => Promise<void> }) {
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [result, setResult] = useState<string>('')
  const questions = quiz?.questions ?? []

  async function generateQuiz() {
    await api('/quizzes/generate-from-flashcards', {
      method: 'POST',
      body: JSON.stringify({ title: 'Adaptive flashcard quiz', difficulty: 'Mixed', questionCount: 6 }),
    })
    setAnswers({})
    setResult('')
    await onChanged()
  }

  async function submitQuiz() {
    if (!quiz || quiz.id === '00000000-0000-0000-0000-000000000000') return
    const response = await api<{ accuracy: number; correctCount: number; totalCount: number }>('/quiz-attempts', {
      method: 'POST',
      body: JSON.stringify({
        quizId: quiz.id,
        title: quiz.title,
        durationSeconds: 240,
        answers,
      }),
    })
    setResult(`${response.correctCount}/${response.totalCount} correct · ${response.accuracy}% accuracy`)
    await onChanged()
  }

  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Adaptive quiz</span>
            <h3>{quiz?.title ?? 'No quiz available'}</h3>
          </div>
          <Trophy size={22} />
        </div>
        <div className="quiz-list">
          {questions.length === 0 && <p className="muted">Create flashcards first so the backend can build a quiz from real data.</p>}
          {questions.map((question, index) => (
            <div className="quiz-card" key={question.id}>
              <span className="quiz-type">{index + 1}. {question.type}</span>
              <h3>{question.prompt}</h3>
              {question.options.length > 0 ? (
                <div className="answers">
                  {question.options.map((option) => (
                    <button
                      className={answers[question.id] === option ? 'correct' : ''}
                      key={option}
                      onClick={() => setAnswers((current) => ({ ...current, [question.id]: option }))}
                      type="button"
                    >
                      {option}
                    </button>
                  ))}
                </div>
              ) : (
                <input
                  value={answers[question.id] ?? ''}
                  onChange={(event) => setAnswers((current) => ({ ...current, [question.id]: event.target.value }))}
                  placeholder="Type your answer"
                />
              )}
              <p className="muted">{question.explanation}</p>
            </div>
          ))}
        </div>
        {result && <div className="success-banner">{result}</div>}
      </div>
      <div className="panel">
        <span className="eyebrow">Quiz engine</span>
        <h3>Multiple choice, true/false, and fill blank</h3>
        <div className="data-form">
          <button className="primary-button" onClick={generateQuiz} type="button">
            Generate from flashcards
          </button>
          <button className="secondary-button" disabled={questions.length === 0} onClick={submitQuiz} type="button">
            Submit answers
          </button>
        </div>
      </div>
    </section>
  )
}

function Roadmap({ roadmap, onChanged }: { roadmap: RoadmapItem[]; onChanged: () => Promise<void> }) {
  const [goal, setGoal] = useState('')

  async function createRoadmap() {
    if (!goal.trim()) return
    await api('/roadmaps', {
      method: 'POST',
      body: JSON.stringify({ goal, level: 'Beginner', weeklyHours: 5 }),
    })
    setGoal('')
    await onChanged()
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">AI roadmap</span>
          <h3>Saved roadmap items</h3>
        </div>
        <Target size={22} />
      </div>
      <div className="data-form inline-form">
        <input value={goal} onChange={(event) => setGoal(event.target.value)} placeholder="Goal, e.g. IELTS 7.0" />
        <button className="primary-button" onClick={createRoadmap} type="button">Create roadmap</button>
      </div>
      <div className="timeline">
        {roadmap.length === 0 && <p className="muted">No roadmap saved yet.</p>}
        {roadmap.map((item, index) => (
          <article className="timeline-item" key={`${item.week}-${item.title}-${index}`}>
            <span className={item.state.toLowerCase()}>{item.state}</span>
            <strong>{item.week}: {item.title}</strong>
            <p>{item.detail}</p>
          </article>
        ))}
      </div>
    </section>
  )
}

function Classroom({
  courses,
  userRole,
  onChanged,
}: {
  courses: Course[]
  userRole: string
  onChanged: () => Promise<void>
}) {
  const [selectedCourseId, setSelectedCourseId] = useState(courses[0]?.id ?? '')
  const [assignments, setAssignments] = useState<Assignment[]>([])
  const [submissions, setSubmissions] = useState<AssignmentSubmission[]>([])
  const [courseName, setCourseName] = useState('')
  const [subject, setSubject] = useState('')
  const [joinCode, setJoinCode] = useState('')
  const [assignmentTitle, setAssignmentTitle] = useState('')
  const [assignmentInstructions, setAssignmentInstructions] = useState('')
  const [assignmentRubric, setAssignmentRubric] = useState('')
  const [submissionContent, setSubmissionContent] = useState('')
  const [gradeDrafts, setGradeDrafts] = useState<Record<string, { score: string; feedback: string }>>({})
  const [status, setStatus] = useState('')
  const selectedCourse = courses.find((course) => course.id === selectedCourseId) ?? courses[0]
  const canTeach = userRole === 'Teacher' || userRole === 'Admin'

  useEffect(() => {
    if (!selectedCourseId && courses[0]?.id) {
      setSelectedCourseId(courses[0].id)
    }
  }, [courses, selectedCourseId])

  const loadCourseData = useCallback(async (courseId: string) => {
    if (!courseId) {
      setAssignments([])
      setSubmissions([])
      return
    }

    const nextAssignments = await api<Assignment[]>(`/courses/${courseId}/assignments`)
    setAssignments(nextAssignments)
    if (canTeach) {
      setSubmissions(await api<AssignmentSubmission[]>(`/courses/${courseId}/submissions`))
    }
  }, [canTeach])

  useEffect(() => {
    if (selectedCourse?.id) {
      loadCourseData(selectedCourse.id).catch((err: Error) => setStatus(err.message))
    }
  }, [loadCourseData, selectedCourse?.id])

  async function createCourse() {
    if (!courseName.trim()) return
    await api('/courses', {
      method: 'POST',
      body: JSON.stringify({ name: courseName, subject }),
    })
    setCourseName('')
    setSubject('')
    setStatus('Course created.')
    await onChanged()
  }

  async function joinCourse() {
    if (!joinCode.trim()) return
    await api('/courses/join', {
      method: 'POST',
      body: JSON.stringify({ code: joinCode }),
    })
    setJoinCode('')
    setStatus('Course joined.')
    await onChanged()
  }

  async function createAssignment() {
    if (!selectedCourse || !assignmentTitle.trim()) return
    await api(`/courses/${selectedCourse.id}/assignments`, {
      method: 'POST',
      body: JSON.stringify({
        title: assignmentTitle,
        instructions: assignmentInstructions,
        rubric: assignmentRubric,
        dueAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
      }),
    })
    setAssignmentTitle('')
    setAssignmentInstructions('')
    setAssignmentRubric('')
    await loadCourseData(selectedCourse.id)
  }

  async function submitAssignment(assignmentId: string) {
    if (!submissionContent.trim()) return
    await api(`/assignments/${assignmentId}/submissions`, {
      method: 'POST',
      body: JSON.stringify({ content: submissionContent }),
    })
    setSubmissionContent('')
    setStatus('Assignment submitted.')
    if (selectedCourse) {
      await loadCourseData(selectedCourse.id)
    }
  }

  async function gradeSubmission(submission: AssignmentSubmission) {
    const draft = gradeDrafts[submission.id]
    if (!draft?.score) return
    await api(`/assignments/${submission.assignmentId}/submissions/${submission.id}/grade`, {
      method: 'PUT',
      body: JSON.stringify({
        score: Number(draft.score),
        feedback: draft.feedback,
      }),
    })
    setStatus('Submission graded.')
    if (selectedCourse) {
      await loadCourseData(selectedCourse.id)
    }
  }

  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Classroom</span>
            <h3>{selectedCourse?.name ?? 'Create or join a course'}</h3>
          </div>
          <BookOpen size={22} />
        </div>
        {selectedCourse ? (
          <>
            <div className="room-summary">
              <strong>Code {selectedCourse.joinCode}</strong>
              <span>{selectedCourse.subject}</span>
              <em>{selectedCourse.role}</em>
            </div>
            <div className="quiz-list">
              {assignments.length === 0 && <p className="muted">No assignments yet.</p>}
              {assignments.map((assignment) => (
                <article className="quiz-card" key={assignment.id}>
                  <span className="quiz-type">Due {new Date(assignment.dueAt).toLocaleDateString()}</span>
                  <h3>{assignment.title}</h3>
                  <p className="muted">{assignment.instructions || 'No instructions.'}</p>
                  {assignment.rubric && <p className="muted">Rubric: {assignment.rubric}</p>}
                  {!canTeach && (
                    <div className="data-form">
                      <textarea value={submissionContent} onChange={(event) => setSubmissionContent(event.target.value)} placeholder="Write your submission..." />
                      <button className="primary-button" onClick={() => submitAssignment(assignment.id)} type="button">Submit work</button>
                    </div>
                  )}
                </article>
              ))}
            </div>
            {canTeach && (
              <div className="deck-list">
                {submissions.map((submission) => (
                  <div className="document-item" key={submission.id}>
                    <strong>Submission {submission.score !== undefined && submission.score !== null ? `· ${submission.score}/100` : ''}</strong>
                    <span>{submission.content}</span>
                    <em>{submission.feedback || 'No feedback yet.'}</em>
                    <em>{new Date(submission.submittedAt).toLocaleString()}</em>
                    <div className="data-form inline-form">
                      <input
                        min="0"
                        max="100"
                        onChange={(event) => setGradeDrafts((current) => ({
                          ...current,
                          [submission.id]: {
                            score: event.target.value,
                            feedback: current[submission.id]?.feedback ?? submission.feedback,
                          },
                        }))}
                        placeholder="Score"
                        type="number"
                        value={gradeDrafts[submission.id]?.score ?? submission.score?.toString() ?? ''}
                      />
                      <button className="primary-button compact" onClick={() => gradeSubmission(submission)} type="button">Grade</button>
                    </div>
                    <textarea
                      className="feedback-input"
                      onChange={(event) => setGradeDrafts((current) => ({
                        ...current,
                        [submission.id]: {
                          score: current[submission.id]?.score ?? submission.score?.toString() ?? '',
                          feedback: event.target.value,
                        },
                      }))}
                      placeholder="Feedback"
                      value={gradeDrafts[submission.id]?.feedback ?? submission.feedback}
                    />
                  </div>
                ))}
              </div>
            )}
          </>
        ) : (
          <div className="empty-state">
            <BookOpen size={34} />
            <h3>No courses yet</h3>
            <p>Teachers can create a course; students can join with a code.</p>
          </div>
        )}
      </div>
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Course tools</span>
            <h3>Real classroom data</h3>
          </div>
          <GraduationCap size={22} />
        </div>
        {status && <div className={status.startsWith('API') ? 'error-banner' : 'success-banner'}>{status}</div>}
        {canTeach && (
          <div className="data-form">
            <input value={courseName} onChange={(event) => setCourseName(event.target.value)} placeholder="Course name" />
            <input value={subject} onChange={(event) => setSubject(event.target.value)} placeholder="Subject" />
            <button className="primary-button" onClick={createCourse} type="button">Create course</button>
          </div>
        )}
        <div className="data-form">
          <input value={joinCode} onChange={(event) => setJoinCode(event.target.value)} placeholder="Join code" />
          <button className="secondary-button" onClick={joinCourse} type="button">Join course</button>
        </div>
        {canTeach && selectedCourse && (
          <div className="data-form">
            <input value={assignmentTitle} onChange={(event) => setAssignmentTitle(event.target.value)} placeholder="Assignment title" />
            <textarea value={assignmentInstructions} onChange={(event) => setAssignmentInstructions(event.target.value)} placeholder="Instructions" />
            <textarea value={assignmentRubric} onChange={(event) => setAssignmentRubric(event.target.value)} placeholder="Rubric, e.g. Accuracy 60, clarity 40" />
            <button className="primary-button" onClick={createAssignment} type="button">Create assignment</button>
          </div>
        )}
        <div className="deck-list">
          {courses.map((course) => (
            <button
              className={selectedCourse?.id === course.id ? 'deck-item active' : 'deck-item'}
              key={course.id}
              onClick={() => setSelectedCourseId(course.id)}
              type="button"
            >
              <span>{course.name}</span>
              <em>{course.subject}</em>
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}

function Collaboration({
  rooms,
  onChanged,
}: {
  rooms: CollaborationRoom[]
  onChanged: () => Promise<void>
}) {
  const [selectedRoomId, setSelectedRoomId] = useState<string>(rooms[0]?.id ?? '')
  const [messages, setMessages] = useState<CollaborationMessage[]>([])
  const [roomName, setRoomName] = useState('')
  const [topic, setTopic] = useState('')
  const [joinCode, setJoinCode] = useState('')
  const [messageText, setMessageText] = useState('')
  const [status, setStatus] = useState('')
  const selectedRoom = rooms.find((room) => room.id === selectedRoomId) ?? rooms[0]

  useEffect(() => {
    if (!selectedRoomId && rooms[0]?.id) {
      setSelectedRoomId(rooms[0].id)
    }
  }, [rooms, selectedRoomId])

  const loadMessages = useCallback(async (roomId: string) => {
    if (!roomId) {
      setMessages([])
      return
    }

    const data = await api<CollaborationMessage[]>(`/collaboration/rooms/${roomId}/messages`)
    setMessages(data)
  }, [])

  useEffect(() => {
    if (!selectedRoom?.id) {
      setMessages([])
      return
    }

    loadMessages(selectedRoom.id).catch((err: Error) => setStatus(err.message))
  }, [loadMessages, selectedRoom?.id])

  async function createRoom() {
    if (!roomName.trim()) return
    const room = await api<CollaborationRoom>('/collaboration/rooms', {
      method: 'POST',
      body: JSON.stringify({ name: roomName, topic }),
    })
    setRoomName('')
    setTopic('')
    setSelectedRoomId(room.id)
    setStatus(`Room created. Join code: ${room.joinCode}`)
    await onChanged()
  }

  async function joinRoom() {
    if (!joinCode.trim()) return
    const room = await api<CollaborationRoom>('/collaboration/rooms/join', {
      method: 'POST',
      body: JSON.stringify({ joinCode }),
    })
    setJoinCode('')
    setSelectedRoomId(room.id)
    setStatus(`Joined ${room.name}.`)
    await onChanged()
  }

  async function sendMessage() {
    if (!selectedRoom || !messageText.trim()) return
    await api<CollaborationMessage>(`/collaboration/rooms/${selectedRoom.id}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content: messageText }),
    })
    setMessageText('')
    await loadMessages(selectedRoom.id)
    await onChanged()
  }

  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Study rooms</span>
            <h3>{selectedRoom?.name ?? 'Create or join a room'}</h3>
          </div>
          <Users size={22} />
        </div>
        {selectedRoom ? (
          <>
            <div className="room-summary">
              <span>{selectedRoom.topic}</span>
              <strong>Code {selectedRoom.joinCode}</strong>
              <em>{selectedRoom.memberCount} members</em>
            </div>
            <div className="collab-log">
              {messages.length === 0 && <p className="muted">No room messages yet.</p>}
              {messages.map((message) => (
                <article className="collab-message" key={message.id}>
                  <div>
                    <strong>{message.displayName}</strong>
                    <span>{new Date(message.createdAt).toLocaleString()}</span>
                  </div>
                  <p>{message.content}</p>
                </article>
              ))}
            </div>
            <div className="chat-input collab-input">
              <MessageSquareText size={18} />
              <input
                value={messageText}
                onChange={(event) => setMessageText(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') sendMessage()
                }}
                placeholder="Write to the study room..."
              />
              <button className="primary-button compact" onClick={sendMessage} type="button">
                Send
              </button>
            </div>
          </>
        ) : (
          <div className="empty-state">
            <Users size={34} />
            <h3>No rooms yet</h3>
            <p>Create a private room and share the join code with classmates.</p>
          </div>
        )}
      </div>

      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Collaboration</span>
            <h3>Rooms saved in SQL Server</h3>
          </div>
          <MessageSquareText size={22} />
        </div>
        {status && <div className="success-banner">{status}</div>}
        <div className="data-form">
          <input value={roomName} onChange={(event) => setRoomName(event.target.value)} placeholder="Room name" />
          <input value={topic} onChange={(event) => setTopic(event.target.value)} placeholder="Topic" />
          <button className="primary-button" onClick={createRoom} type="button">Create room</button>
        </div>
        <div className="data-form">
          <input value={joinCode} onChange={(event) => setJoinCode(event.target.value)} placeholder="Join code" />
          <button className="secondary-button" onClick={joinRoom} type="button">Join room</button>
        </div>
        <div className="deck-list">
          {rooms.length === 0 && <p className="muted">You are not in any rooms.</p>}
          {rooms.map((room) => (
            <button
              className={selectedRoom?.id === room.id ? 'deck-item active' : 'deck-item'}
              key={room.id}
              onClick={() => setSelectedRoomId(room.id)}
              type="button"
            >
              <span>{room.name}</span>
              <em>{room.messageCount}</em>
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}

function Planner({
  isOnline,
  offlineQueueCount,
  reminders,
  onChanged,
  onQueued,
}: {
  isOnline: boolean
  offlineQueueCount: number
  reminders: LearningReminder[]
  onChanged: () => Promise<void>
  onQueued: () => void
}) {
  const [title, setTitle] = useState('')
  const [note, setNote] = useState('')
  const [status, setStatus] = useState('')
  const [dueAt, setDueAt] = useState(() => {
    const date = new Date(Date.now() + 60 * 60 * 1000)
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
    return date.toISOString().slice(0, 16)
  })

  async function createReminder() {
    if (!title.trim() || !dueAt) return
    const dueDate = new Date(dueAt)
    const payload = {
      title,
      note,
      channel: 'InApp',
      dueAt: dueDate.toISOString(),
    }

    if (!isOnline) {
      queueReminder(payload)
      setTitle('')
      setNote('')
      onQueued()
      return
    }

    await api('/notifications/reminders', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    setTitle('')
    setNote('')
    await onChanged()
  }

  async function completeReminder(reminderId: string) {
    await api(`/notifications/reminders/${reminderId}/complete`, { method: 'PUT' })
    await onChanged()
  }

  async function deleteReminder(reminderId: string) {
    await api(`/notifications/reminders/${reminderId}`, { method: 'DELETE' })
    await onChanged()
  }

  async function sendReminderEmail(reminderId: string) {
    setStatus('')
    try {
      await api(`/notifications/reminders/${reminderId}/send-email`, { method: 'POST' })
      setStatus('Email notification sent.')
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Email notification failed')
    }
  }

  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Planner reminders</span>
            <h3>In-app notifications from SQL Server</h3>
          </div>
          <Bell size={22} />
        </div>
        {offlineQueueCount > 0 && (
          <div className="offline-banner">
            {offlineQueueCount} reminder{offlineQueueCount === 1 ? '' : 's'} waiting to sync.
          </div>
        )}
        {status && <div className={status.startsWith('API') ? 'error-banner' : 'success-banner'}>{status}</div>}
        <div className="reminder-list">
          {reminders.length === 0 && <p className="muted">No reminders scheduled yet.</p>}
          {reminders.map((reminder) => (
            <article className={reminder.isCompleted ? 'reminder-item completed' : 'reminder-item'} key={reminder.id}>
              <div>
                <span>{reminder.channel}</span>
                <strong>{reminder.title}</strong>
                <p>{reminder.note || 'No note.'}</p>
              </div>
              <div className="reminder-actions">
                <em>{new Date(reminder.dueAt).toLocaleString()}</em>
                {!reminder.isCompleted && (
                  <button className="primary-button compact" onClick={() => completeReminder(reminder.id)} type="button">
                    Complete
                  </button>
                )}
                <button className="secondary-button compact" onClick={() => sendReminderEmail(reminder.id)} type="button">
                  Send email
                </button>
                <button className="danger-button compact" onClick={() => deleteReminder(reminder.id)} type="button">
                  Delete
                </button>
              </div>
            </article>
          ))}
        </div>
      </div>
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Create reminder</span>
            <h3>Schedule your next session</h3>
          </div>
          <CalendarDays size={22} />
        </div>
        <div className="data-form">
          <input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Reminder title" />
          <textarea value={note} onChange={(event) => setNote(event.target.value)} placeholder="Optional note" />
          <input value={dueAt} onChange={(event) => setDueAt(event.target.value)} type="datetime-local" />
          <button className="primary-button" onClick={createReminder} type="button">
            <Bell size={18} />
            Save reminder
          </button>
        </div>
      </div>
    </section>
  )
}

function Analytics({
  studySeries,
  subjects,
}: {
  studySeries: StudyPoint[]
  subjects: SubjectProgress[]
}) {
  return (
    <section className="content-grid">
      <div className="panel wide">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Student analytics</span>
            <h3>Accuracy from quiz attempts</h3>
          </div>
          <BarChart3 size={22} />
        </div>
        <div className="chart-box">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={studySeries}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="day" />
              <YAxis />
              <Tooltip />
              <Bar dataKey="accuracy" fill="#376dcb" radius={[8, 8, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
      <SubjectPanel subjects={subjects} />
    </section>
  )
}

function GuardianInvitePanel() {
  const [inviteCode, setInviteCode] = useState('')
  const [expiresAt, setExpiresAt] = useState('')
  const [error, setError] = useState('')

  async function createInvitation() {
    setError('')
    try {
      const invitation = await api<{ code: string; expiresAt: string }>('/parent/invitations', { method: 'POST' })
      setInviteCode(invitation.code)
      setExpiresAt(invitation.expiresAt)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create guardian invite')
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Guardian access</span>
          <h3>Invite a parent</h3>
        </div>
        <ShieldCheck size={22} />
      </div>
      {error && <div className="error-banner">{error}</div>}
      {inviteCode ? (
        <div className="room-summary">
          <strong>Code {inviteCode}</strong>
          <span>Expires {new Date(expiresAt).toLocaleString()}</span>
        </div>
      ) : (
        <p className="muted">Create a code for a parent account to view your progress dashboard.</p>
      )}
      <button className="secondary-button compact" onClick={createInvitation} type="button">
        Create invite code
      </button>
    </section>
  )
}

function ParentPanel({
  students,
  dashboards,
  onChanged,
}: {
  students: ParentStudent[]
  dashboards: ParentStudentDashboard[]
  onChanged: () => Promise<void>
}) {
  const [code, setCode] = useState('')
  const [status, setStatus] = useState('')

  async function linkStudent() {
    if (!code.trim()) return
    setStatus('')
    try {
      await api('/parent/links', {
        method: 'POST',
        body: JSON.stringify({ code }),
      })
      setCode('')
      setStatus('Student linked.')
      await onChanged()
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Could not link student')
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Parent dashboard</span>
          <h3>Linked student progress</h3>
        </div>
        <ShieldCheck size={22} />
      </div>
      {status && <div className={status.startsWith('API') ? 'error-banner' : 'success-banner'}>{status}</div>}
      <div className="data-form inline-form">
        <input value={code} onChange={(event) => setCode(event.target.value)} placeholder="Student invite code" />
        <button className="primary-button" onClick={linkStudent} type="button">Link student</button>
      </div>
      {students.length === 0 && <p className="muted">No linked students yet.</p>}
      {dashboards.length > 0 && (
        <section className="metric-grid compact-grid">
          {dashboards.map((student) => (
            <Metric key={student.studentId} icon={GraduationCap} label={student.displayName} value={`${student.accuracy}%`} tone="green" />
          ))}
        </section>
      )}
      <div className="deck-list">
        {dashboards.map((student) => (
          <div className="document-item" key={student.studentId}>
            <strong>{student.displayName}</strong>
            <span>{student.studyHours}h study · {student.studyDays} study days</span>
            <em>{student.dueFlashcards} due cards · {student.quizAttempts} attempts · {student.pendingReminders} reminders</em>
          </div>
        ))}
      </div>
    </section>
  )
}

function TeacherPanel({ dashboard }: { dashboard: TeacherDashboard | null }) {
  if (!dashboard) return null

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Teacher dashboard</span>
          <h3>Class learning risk signals</h3>
        </div>
        <GraduationCap size={22} />
      </div>
      <section className="metric-grid compact-grid">
        <Metric icon={BookOpen} label="Classes" value={`${dashboard.classes}`} tone="green" />
        <Metric icon={Trophy} label="Attempts" value={`${dashboard.totalAttempts}`} tone="blue" />
        <Metric icon={CheckCircle2} label="Avg accuracy" value={`${dashboard.averageAccuracy}%`} tone="violet" />
        <Metric icon={Flame} label="At risk" value={`${dashboard.studentsAtRisk}`} tone="coral" />
      </section>
      <div className="chip-cloud">
        {dashboard.weakTopics.map((topic) => (
          <span key={topic.name}>{topic.name}: {topic.progress}%</span>
        ))}
      </div>
    </section>
  )
}

function AdminPanel({ dashboard }: { dashboard: AdminDashboard | null }) {
  if (!dashboard) return null

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Admin dashboard</span>
          <h3>Platform usage</h3>
        </div>
        <ShieldCheck size={22} />
      </div>
      <section className="metric-grid compact-grid">
        <Metric icon={GraduationCap} label="Users" value={`${dashboard.users}`} tone="green" />
        <Metric icon={Brain} label="Cards" value={`${dashboard.flashcards}`} tone="blue" />
        <Metric icon={Trophy} label="Attempts" value={`${dashboard.quizAttempts}`} tone="violet" />
        <Metric icon={Bot} label="AI msgs" value={`${dashboard.aiMessages}`} tone="coral" />
      </section>
    </section>
  )
}

function Metric({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: typeof Clock3
  label: string
  value: string
  tone: string
}) {
  return (
    <article className={`metric ${tone}`}>
      <Icon size={22} />
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  )
}

export default App
