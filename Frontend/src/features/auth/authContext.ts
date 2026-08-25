import { createContext } from 'react'
import type { AuthenticatedUser, SocialAuthProvider } from './authTypes.ts'

export interface AuthContextValue {
  user: AuthenticatedUser | null
  isAuthenticating: boolean
  signInWithEmail(email: string): Promise<void>
  signInWithSocialProvider(provider: SocialAuthProvider): Promise<void>
  signUp(): Promise<void>
  signOut(): Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
