import { AuthProvider } from './features/auth/AuthProvider.tsx'
import { LoginPage } from './features/auth/LoginPage.tsx'
import { useAuth } from './features/auth/useAuth.ts'
import { ConversationDashboard } from './features/conversations/ConversationDashboard.tsx'

function AuthenticatedApplication() {
  const { user } = useAuth()

  return user === null ? <LoginPage /> : <ConversationDashboard user={user} />
}

function App() {
  return (
    <AuthProvider>
      <AuthenticatedApplication />
    </AuthProvider>
  )
}

export default App
