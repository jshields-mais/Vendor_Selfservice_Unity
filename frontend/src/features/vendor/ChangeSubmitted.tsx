import { useNavigate } from "react-router-dom";
import { AppShell } from "../../layout/AppShell";
import { Button } from "../../ui";
import { useMyChangeRequests } from "../../api/vssClient";

export function ChangeSubmitted() {
  const nav = useNavigate();
  const { data: crs } = useMyChangeRequests(true);
  const latest = crs?.[0];

  return (
    <AppShell title="Change submitted" crumb="Vendor Portal">
      <div style={{ maxWidth: 560, margin: "40px auto", background: "#fff", border: "1px solid var(--border-1)", borderRadius: 12, padding: 40, textAlign: "center" }}>
        <div style={{ width: 60, height: 60, borderRadius: 14, background: "var(--colorStatusSuccessBackground1)", color: "var(--colorStatusSuccessForeground1)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 20px" }}>
          <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
        </div>
        <h2 style={{ fontSize: 24, margin: 0 }}>Changes submitted for review</h2>
        <p style={{ fontSize: 15, color: "var(--fg-2)", lineHeight: 1.6, margin: "12px 0 0" }}>
          Your updates to <b style={{ color: "var(--fg-1)" }}>{latest?.section ?? "your record"}</b> were sent to City of Bozeman staff. Once approved, they'll sync to the ERP vendor master. You'll get an email when the review is complete.
        </p>
        {latest && (
          <div style={{ display: "inline-flex", gap: 8, marginTop: 20, padding: "8px 16px", borderRadius: 999, background: "#fef7b2", color: "#817400", fontSize: 13, fontWeight: 600 }}>
            Change request #{latest.code} · {latest.status}
          </div>
        )}
        <div><Button variant="teal" style={{ marginTop: 26 }} onClick={() => nav("/console")}>Back to portal</Button></div>
      </div>
    </AppShell>
  );
}
