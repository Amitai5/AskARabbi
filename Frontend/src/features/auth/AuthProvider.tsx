import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue } from './authContext.ts'
import { demoAuthClient } from './demoAuthClient.ts'
import type { AuthClient, AuthenticatedUser, SocialAuthProvider } from './authTypes.ts'

interface AuthProviderProps {
  children: ReactNode
  client?: AuthClient
}

export function AuthProvider({ children, client = demoAuthClient }: AuthProviderProps) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [isAuthenticating, setIsAuthenticating] = useState(false)

  const signInWithEmail = useCallback(async (email: string) => {
    setIsAuthenticating(true)
    try {
      setUser(await client.signInWithEmail(email))
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const signInWithSocialProvider = useCallback(async (provider: SocialAuthProvider) => {
    setIsAuthenticating(true)
    try {
      setUser(await client.signInWithSocialProvider(provider))
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const signUp = useCallback(async () => {
    setIsAuthenticating(true)
    try {
      setUser(await client.signUp())
    } finally {
      setIsAuthenticating(false)
    }
  }, [client])

  const signOut = useCallback(async () => {
    await client.signOut()
    setUser(null)
  }, [client])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    isAuthenticating,
    signInWithEmail,
    signInWithSocialProvider,
    signUp,
    signOut,
  }), [isAuthenticating, signInWithEmail, signInWithSocialProvider, signOut, signUp, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
