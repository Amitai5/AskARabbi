import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './authContext.ts'
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
  }), [authenticationError, clearAuthenticationError, confirmPasswordReset, isAuthenticating, isInitializing, requestPasswordReset, signInWithEmail, signInWithSocialProvider, signOut, signUp, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function getErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error && error.message.trim().length > 0 ? error.message : fallback
}
