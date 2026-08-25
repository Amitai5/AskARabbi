import type { AuthClient, AuthenticatedUser } from './authTypes.ts'

const DemoUser: AuthenticatedUser = {
  id: 'demo-user',
  name: 'Amitai Erfanian',
  email: 'amitai@example.com',
  initials: 'AE',
}

export const demoAuthClient: AuthClient = {
  signInWithEmail(email) {
    return Promise.resolve({ ...DemoUser, email })
  },
  signInWithSocialProvider() {
    return Promise.resolve(DemoUser)
  },
  signUp() {
    return Promise.resolve(DemoUser)
  },
  signOut() {
    return Promise.resolve()
  },
}
