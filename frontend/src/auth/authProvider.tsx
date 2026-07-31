import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { MsalProvider, useMsal, useIsAuthenticated } from "@azure/msal-react";
import { setApiAuthProvider } from "@univerus/udp-react-enterprise-component-library";
import { msalInstance, loginRequest, apiRequest } from "../config/msalConfig";

export type PortalRole = "vendor" | "admin";
export interface AuthAccount { name: string; email: string; }

interface AuthContextValue {
  mode: "dev" | "entra";
  isAuthenticated: boolean;
  account: AuthAccount | null;
  role: PortalRole;
  setRole: (r: PortalRole) => void;
  login: () => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within <AuthProvider>");
  return ctx;
}

/** Local dev: a fixed seeded identity; requests carry X-Dev-* headers. */
function DevAuthProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<PortalRole>("vendor");

  useEffect(() => {
    setApiAuthProvider(() => ({
      "X-Dev-Uuid": role === "admin" ? "dev-city-admin" : "dev-dana-northstar",
      "X-Dev-Email": role === "admin" ? "finance@unitycity.gov" : "dana@northstarsupply.com",
      "X-Dev-Name": role === "admin" ? "Finance Admin" : "Dana Whitfield",
      "X-Dev-Role": role,
    }));
  }, [role]);

  const value: AuthContextValue = {
    mode: "dev",
    isAuthenticated: true,
    account: role === "admin"
      ? { name: "Finance Admin", email: "finance@unitycity.gov" }
      : { name: "Dana Whitfield", email: "dana@northstarsupply.com" },
    role,
    setRole,
    login: () => {},
    logout: () => {},
  };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Entra: real MSAL sign-in; requests carry a bearer token. */
function EntraInner({ children }: { children: ReactNode }) {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [role, setRole] = useState<PortalRole>("vendor");

  useEffect(() => {
    setApiAuthProvider(async (): Promise<Record<string, string>> => {
      const account = accounts[0];
      if (!account) return {};
      try {
        const res = await instance.acquireTokenSilent({ ...apiRequest, account });
        return { Authorization: `Bearer ${res.accessToken}` };
      } catch {
        return {};
      }
    });
  }, [accounts, instance]);

  const a = accounts[0];
  const value: AuthContextValue = {
    mode: "entra",
    isAuthenticated,
    account: a ? { name: a.name ?? "", email: a.username } : null,
    role,
    setRole,
    login: () => void instance.loginRedirect(loginRequest),
    logout: () => void instance.logoutRedirect(),
  };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function EntraAuthProvider({ children }: { children: ReactNode }) {
  return (
    <MsalProvider instance={msalInstance}>
      <EntraInner>{children}</EntraInner>
    </MsalProvider>
  );
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const mode = import.meta.env.REACT_APP_AUTH_MODE ?? "dev";
  return mode === "entra"
    ? <EntraAuthProvider>{children}</EntraAuthProvider>
    : <DevAuthProvider>{children}</DevAuthProvider>;
}
