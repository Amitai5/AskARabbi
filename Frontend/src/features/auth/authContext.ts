import { createContext } from 'react'
import type { AuthenticatedUser, SocialAuthProvider } from './authTypes.ts'

export interface AuthContextValue {
  user: AuthenticatedUser | null
  isInitializing: boolean
  isAuthenticating: boolean
  authenticationError: string | null
  signInWithEmail(email: string): Promise<void>
  signInWithSocialProvider(provider: SocialAuthProvider): Promise<void>
  signUp(): Promise<void>
  clearAuthenticationError(): void
  requestPasswordReset(email: string): Promise<void>
  confirmPasswordReset(token: string, newPassword: string): Promise<void>
  signOut(): Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
