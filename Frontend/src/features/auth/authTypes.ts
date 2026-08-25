export interface AuthenticatedUser {
  id: string
  name: string
  email: string
  initials: string
}

export type SocialAuthProvider = 'google' | 'apple' | 'microsoft'

export interface AuthClient {
  signInWithEmail(email: string): Promise<AuthenticatedUser>
  signInWithSocialProvider(provider: SocialAuthProvider): Promise<AuthenticatedUser>
  signUp(): Promise<AuthenticatedUser>
  signOut(): Promise<void>
}
