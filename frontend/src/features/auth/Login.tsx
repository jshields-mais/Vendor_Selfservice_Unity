import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { AuthLayout } from "./AuthLayout";
import { Button } from "../../ui";
import { useAuth } from "../../auth/authProvider";

export function Login() {
  const nav = useNavigate();
  const { mode, login } = useAuth();
  const [tab, setTab] = useState<"vendor" | "admin">("vendor");

  const signIn = () => {
    if (mode === "entra") login();
    else nav("/"); // dev: already authenticated → RootRedirect decides
  };

  const tabStyle = (active: boolean) => ({
    flex: 1, padding: "9px 8px", border: "none", borderRadius: 6, cursor: "pointer",
    fontFamily: "var(--font-sans)", fontSize: 13, fontWeight: 600,
    background: active ? "#fff" : "transparent", color: active ? "var(--color-navy)" : "var(--fg-2)",
    boxShadow: active ? "var(--shadow-1)" : "none" });

  return (
    <AuthLayout>
      <h2 style={{ fontSize: 28, margin: 0 }}>Sign in</h2>
      <p style={{ margin: "8px 0 24px", fontSize: 14, color: "var(--fg-2)" }}>Access the vendor self service portal.</p>

      <div style={{ display: "flex", gap: 6, padding: 4, background: "var(--bg-2)", borderRadius: 8, marginBottom: 22 }}>
        <button style={tabStyle(tab === "vendor")} onClick={() => setTab("vendor")}>Vendor</button>
        <button style={tabStyle(tab === "admin")} onClick={() => setTab("admin")}>City staff</button>
      </div>

      <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6 }}>Work email</label>
      <input defaultValue={tab === "admin" ? "finance@bozeman.gov" : "dana@northstarsupply.com"}
        style={{ width: "100%", padding: "11px 13px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 14, outline: "none", marginBottom: 16 }} />
      <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6 }}>Password</label>
      <input type="password" defaultValue="passw0rd"
        style={{ width: "100%", padding: "11px 13px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 14, outline: "none", marginBottom: 10 }} />
      <div style={{ textAlign: "right", marginBottom: 20 }}><a href="#" onClick={(e) => e.preventDefault()} style={{ fontSize: 13 }}>Forgot password?</a></div>

      <Button variant="teal" style={{ width: "100%" }} onClick={signIn}>
        {tab === "admin" ? "Sign in to admin" : "Sign in"}
      </Button>

      {tab === "admin" && (
        <p style={{ fontSize: 12, color: "var(--fg-3)", marginTop: 12, textAlign: "center" }}>
          The City-staff admin portal ships in the next phase.
        </p>
      )}
      {tab === "vendor" && (
        <div style={{ textAlign: "center", marginTop: 20, fontSize: 14, color: "var(--fg-2)" }}>
          New supplier? <a href="#" onClick={(e) => { e.preventDefault(); nav("/signup"); }} style={{ fontWeight: 600 }}>Create a vendor account</a>
        </div>
      )}
    </AuthLayout>
  );
}
