import { useNavigate } from "react-router-dom";
import { AuthLayout } from "./AuthLayout";
import { Button } from "../../ui";

const inputStyle = { width: "100%", padding: "10px 12px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 14, outline: "none" } as const;
const labelStyle = { display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6 } as const;

export function Signup() {
  const nav = useNavigate();
  return (
    <AuthLayout>
      <a href="#" onClick={(e) => { e.preventDefault(); nav("/login"); }} style={{ fontSize: 13, color: "var(--fg-2)" }}>← Back to sign in</a>
      <h2 style={{ fontSize: 28, margin: "16px 0 0" }}>Create your account</h2>
      <p style={{ margin: "8px 0 24px", fontSize: 14, color: "var(--fg-2)" }}>We'll email you a verification link before you link your company record.</p>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <div><label style={labelStyle}>First name</label><input style={inputStyle} /></div>
        <div><label style={labelStyle}>Last name</label><input style={inputStyle} /></div>
      </div>
      <div style={{ marginTop: 14 }}><label style={labelStyle}>Company name</label><input defaultValue="Northstar Supply Co." style={inputStyle} /></div>
      <div style={{ marginTop: 14 }}><label style={labelStyle}>Work email</label><input type="email" defaultValue="dana@northstarsupply.com" style={inputStyle} /></div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 14 }}>
        <div><label style={labelStyle}>Password</label><input type="password" style={inputStyle} /></div>
        <div><label style={labelStyle}>Confirm</label><input type="password" style={inputStyle} /></div>
      </div>
      <label style={{ display: "flex", gap: 10, alignItems: "flex-start", marginTop: 18, fontSize: 13, color: "var(--fg-2)", lineHeight: 1.5 }}>
        <input type="checkbox" style={{ marginTop: 2 }} /> I agree to the Unity City supplier terms and privacy policy.
      </label>
      <Button variant="teal" style={{ width: "100%", marginTop: 20 }} onClick={() => nav("/check-inbox")}>
        Create account &amp; send verification
      </Button>
    </AuthLayout>
  );
}
