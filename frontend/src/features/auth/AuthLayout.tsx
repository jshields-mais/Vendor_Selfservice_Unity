import type { ReactNode } from "react";

const POINTS = [
  "Update details anytime, from anywhere",
  "Bank changes reviewed before they reach the ERP",
  "Keep W-9 and insurance certificates current",
];

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div style={{ minHeight: "100vh", display: "grid", gridTemplateColumns: "1.1fr 1fr" }}>
      {/* brand panel */}
      <div style={{ position: "relative", background: "var(--colorAppHeader)", color: "#fff", padding: "56px 64px", display: "flex", flexDirection: "column", overflow: "hidden" }}>
        <div style={{ position: "relative", display: "flex", alignItems: "center", gap: 14 }}>
          <div style={{ width: 40, height: 40, borderRadius: 6, background: "var(--color-teal)", display: "flex", alignItems: "center", justifyContent: "center", fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 20 }}>V</div>
          <div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 17 }}>Univerus VSS</div>
            <div style={{ fontSize: 11, letterSpacing: ".14em", textTransform: "uppercase", color: "var(--colorBrandStroke2)" }}>Vendor Self Service</div>
          </div>
        </div>
        <div style={{ position: "relative", marginTop: "auto" }}>
          <div style={{ fontSize: 12, letterSpacing: ".16em", textTransform: "uppercase", color: "var(--colorBrandStroke2)", fontWeight: 600 }}>City of Bozeman · Supplier Portal</div>
          <h1 style={{ fontSize: 40, lineHeight: 1.1, margin: "16px 0 0", color: "#fff", maxWidth: "15ch" }}>Manage your vendor record in one place.</h1>
          <p style={{ fontSize: 16, lineHeight: 1.6, color: "rgba(255,255,255,.82)", maxWidth: "42ch", margin: "18px 0 0" }}>
            Update your company details, banking, tax forms and compliance documents, and submit changes straight to the City's ERP for review.
          </p>
          <div style={{ display: "flex", flexDirection: "column", gap: 14, marginTop: 32 }}>
            {POINTS.map((p) => (
              <div key={p} style={{ display: "flex", alignItems: "center", gap: 12, fontSize: 14, color: "rgba(255,255,255,.9)" }}>
                <span style={{ width: 22, height: 22, borderRadius: 999, background: "rgba(15,108,189,.35)", color: "var(--colorBrandStroke2)", display: "flex", alignItems: "center", justifyContent: "center", flex: "0 0 22px" }}>
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
                </span>
                {p}
              </div>
            ))}
          </div>
        </div>
        <div style={{ position: "relative", marginTop: "auto", paddingTop: 40, fontSize: 12, color: "rgba(255,255,255,.55)" }}>Powered by Univerus · ERP-agnostic vendor management</div>
      </div>

      {/* form column */}
      <div style={{ background: "#fff", display: "flex", alignItems: "center", justifyContent: "center", padding: 48 }}>
        <div style={{ width: "100%", maxWidth: 400, animation: "vssFade .4s var(--ease-entrance)" }}>{children}</div>
      </div>
    </div>
  );
}
