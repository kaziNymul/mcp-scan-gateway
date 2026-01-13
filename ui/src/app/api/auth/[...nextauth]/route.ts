import NextAuth, { NextAuthOptions } from 'next-auth';
import AzureADProvider from 'next-auth/providers/azure-ad';

export const authOptions: NextAuthOptions = {
  providers: [
    AzureADProvider({
      clientId: process.env.AZURE_AD_CLIENT_ID!,
      clientSecret: process.env.AZURE_AD_CLIENT_SECRET!,
      tenantId: process.env.AZURE_AD_TENANT_ID!,
      authorization: {
        params: {
          scope: 'openid profile email',
        },
      },
    }),
  ],
  callbacks: {
    async jwt({ token, account, profile }) {
      if (account && profile) {
        // Extract roles from Azure AD token
        const roles = (profile as any).roles || [];
        token.roles = roles;
        token.accessToken = account.access_token;
      }
      return token;
    },
    async session({ session, token }) {
      // Add roles to session
      const roles = (token.roles as string[]) || [];
      const adminRole = process.env.ADMIN_ROLE_VALUE || 'mcp.admin';
      
      session.user = {
        ...session.user,
        id: token.sub!,
        roles: roles,
        isAdmin: roles.includes(adminRole),
      };
      session.accessToken = token.accessToken as string;
      
      return session;
    },
  },
  pages: {
    signIn: '/auth/signin',
    error: '/auth/error',
  },
  session: {
    strategy: 'jwt',
  },
};

const handler = NextAuth(authOptions);
export { handler as GET, handler as POST };
