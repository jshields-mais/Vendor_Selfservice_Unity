import { useNavigate } from "react-router-dom";
import { AuthLayout } from "./AuthLayout";
import { Button } from "../../ui";
import { useMe } from "../../api/vssClient";

export function LinkSuccess() {
  const nav = useNavigate();
  const { data: me } = useMe();
  return (
    <AuthLayout>
      <div style={{ width: 56, height: 56, borderRadius: 12, background: "var(--colorStatusSuccessBackground1)", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--colorStatusSuccessForeground1)", marginBottom: 22 }}>
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
      </div>
      <h2 style={{ fontSize: 26, margin: 0 }}>Account linked</h2>
      <p style={{ margin: "12px 0 0", fontSize: 15, color: "var(--fg-2)", lineHeight: 1.6 }}>
        Your account is now linked to <b style={{ color: "var(--fg-1)" }}>{me?.vendorName ?? "your vendor record"} ({me?.vendorNumber})</b>. You can review and update your details, and submit changes for City review.
      </p>
      <Button variant="teal" style={{ width: "100%", marginTop: 24 }} onClick={() => nav("/console")}>
        Go to my portal →
      </Button>
    </AuthLayout>
  );
}
