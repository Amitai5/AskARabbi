export interface AuthenticatedUser {
  id: string
  name: string
  email: string
  initials: string
  isEmailVerified: boolean
  profileImageUrl?: string
}

export type SocialAuthProvider = 'google' | 'apple' | 'microsoft'

export interface AuthClient {
  getSession(): Promise<AuthenticatedUser | null>
  signInWithEmail(email: string): Promise<AuthenticatedUser | null>
  signInWithSocialProvider(provider: SocialAuthProvider): Promise<AuthenticatedUser | null>
  signUp(): Promise<AuthenticatedUser | null>
  requestPasswordReset(email: string): Promise<void>
  confirmPasswordReset(token: string, newPassword: string): Promise<void>
  signOut(): Promise<void>
}
