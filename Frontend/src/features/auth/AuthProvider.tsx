import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './authContext.ts'
import { Toast } from '../../components/Toast.tsx'
import { publishUserDataEvent, subscribeToUserDataEvents } from '../settings/userDataEvents.ts'
import type { AuthClient, AuthenticatedUser, SocialAuthProvider } from './authTypes.ts'

interface AuthProviderProps {
  children: ReactNode
  client: AuthClient
}

export function AuthProvider({ children, client }: AuthProviderProps) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const [isAuthenticating, setIsAuthenticating] = useState(false)
  const [authenticationError, setAuthenticationError] = useState<string | null>(null)
  const [deletionStatus, setDeletionStatus] = useState<'deleted' | 'pending' | null>(null)

  useEffect(() => {
    if (!user) { return }
    return subscribeToUserDataEvents(user.id, (event) => {
      if (event.kind === 'account-deleted') {
        setUser(null)
        setDeletionStatus(event.status === 'pending' ? 'pending' : 'deleted')
      }
    })
  }, [user])

  useEffect(() => {
    let isCurrent = true

    void client.getSession()
      .then((sessionUser) => {
        if (isCurrent) {
          setUser(sessionUser)
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setAuthenticationError(getErrorMessage(error, 'The AskRabbi API is unavailable. Start the backend and try again.'))
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsInitializing(false)
        }
      })

    return () => {
      isCurrent = false
    }
  }, [client])

  const signInWithEmail = useCallback(async (email: string) => {
    setAuthenticationError(null)
    setIsAuthenticating(true)
    try {
      const authenticatedUser = await client.signInWithEmail(email)
      if (authenticatedUser !== null) {
        setUser(authenticatedUser)
      }
    } catch (error) {
      setAuthenticationError(getErrorMessage(error, 'Sign-in could not be started.'))
      throw error
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const signInWithSocialProvider = useCallback(async (provider: SocialAuthProvider) => {
    setAuthenticationError(null)
    setIsAuthenticating(true)
    try {
      const authenticatedUser = await client.signInWithSocialProvider(provider)
      if (authenticatedUser !== null) {
        setUser(authenticatedUser)
      }
    } catch (error) {
      setAuthenticationError(getErrorMessage(error, 'Sign-in could not be started.'))
      throw error
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const signUp = useCallback(async () => {
    setAuthenticationError(null)
    setIsAuthenticating(true)
    try {
      const authenticatedUser = await client.signUp()
      if (authenticatedUser !== null) {
        setUser(authenticatedUser)
      }
    } catch (error) {
      setAuthenticationError(getErrorMessage(error, 'Account creation could not be started.'))
      throw error
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const clearAuthenticationError = useCallback(() => {
    setAuthenticationError(null)
  }, [])

  const signOut = useCallback(async () => {
    await client.signOut()
    setUser(null)
  }, [client])

  const requestPasswordReset = useCallback((email: string) => client.requestPasswordReset(email), [client])
  const deleteAccount = useCallback(async () => {
    const result = await client.deleteAccount()
    if (user) { publishUserDataEvent({ userId: user.id, kind: 'account-deleted', status: result.status }) }
    setUser(null)
    setDeletionStatus(result.status)
  }, [client, user])
  const confirmPasswordReset = useCallback((token: string, newPassword: string) => client.confirmPasswordReset(token, newPassword), [client])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    isInitializing,
    isAuthenticating,
    authenticationError,
    signInWithEmail,
    signInWithSocialProvider,
    signUp,
    clearAuthenticationError,
    requestPasswordReset,
    confirmPasswordReset,
    signOut,
    deleteAccount,
  }), [authenticationError, clearAuthenticationError, confirmPasswordReset, deleteAccount, isAuthenticating, isInitializing, requestPasswordReset, signInWithEmail, signInWithSocialProvider, signOut, signUp, user])

  return <AuthContext.Provider value={value}>{children}{deletionStatus ? <Toast notificationId={1} title={deletionStatus === 'deleted' ? 'Account deleted' : 'Account deletion requested'} message={deletionStatus === 'deleted' ? 'Your AskRabbi account and its data have been deleted.' : 'Your account is disabled. We will automatically retry the remaining cleanup.'} onDismiss={() => setDeletionStatus(null)} /> : null}</AuthContext.Provider>
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message.trim().length > 0 ? error.message : fallback
}
