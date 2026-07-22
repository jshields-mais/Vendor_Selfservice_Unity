import { useNavigate } from "react-router-dom";
import { AppShell } from "../../layout/AppShell";
import { Button, Spinner } from "../../ui";
import { useMe, useVendor } from "../../api/vssClient";

const TILES: { title: string; desc: string; tab: string; bg: string; fg: string; icon: string }[] = [
  { title: "Company profile", desc: "Legal name, DBA, entity type", tab: "company", bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)", icon: "M3 21V7a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v14M3 21h18M7 9h2M7 13h2M7 17h2" },
  { title: "Contacts", desc: "Primary, AP and sales contacts", tab: "contacts", bg: "var(--colorBrandBackground2)", fg: "var(--color-navy)", icon: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z" },
  { title: "Addresses", desc: "Remit-to and physical addresses", tab: "addresses", bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)", icon: "M9 20 3 17V4l6 3 6-3 6 3v13l-6-3-6 3zM9 7v13M15 4v13" },
  { title: "Banking & remittance", desc: "EFT / ACH payment details", tab: "banking", bg: "#fef7b2", fg: "#817400", icon: "M3 10 12 4l9 6M4 10v8M20 10v8M8 10v8M16 10v8M3 21h18" },
  { title: "Tax & W-9", desc: "TIN, classification, W-9 form", tab: "tax", bg: "var(--colorBrandBackground2)", fg: "var(--color-navy)", icon: "M9 2h6a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zM10 7h4M10 11h4M10 15h2" },
  { title: "Documents & compliance", desc: "W-9, COI, licenses, certs", tab: "documents", bg: "#fdf6f3", fg: "var(--color-orange)", icon: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6" },
  { title: "Category codes", desc: "Commodity & NIGP codes", tab: "categories", bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)", icon: "M20.6 13.4 12 22l-9-9V4h9zM7.5 7.5h.01" },
];

export function VendorConsole() {
  const nav = useNavigate();
  const { data: me, isLoading } = useMe();
  const { data: vendor } = useVendor(true);

  if (isLoading || !me) return <AppShell title="My dashboard" crumb="Vendor Portal"><Spinner /></AppShell>;

  const stats = [
    { label: "Vendor number", value: me.vendorNumber ?? "—", note: "City of Bozeman ERP" },
    { label: "Profile complete", value: `${me.profileCompletePct}%`, note: vendor ? "W-9 & COI on file" : "" },
    { label: "Pending changes", value: String(me.pendingChangeCount), note: me.pendingChangeCount ? "In review" : "None" },
    { label: "Last payment", value: "Jun 30", note: "ACH · $12,480.00" },
  ];

  return (
    <AppShell title="My dashboard" crumb="Vendor Portal">
      <div style={{ display: "flex", alignItems: "center", gap: 14, padding: "18px 22px", borderRadius: 10, background: "var(--colorAppHeader)", color: "#fff", marginBottom: 22 }}>
        <div style={{ flex: 1 }}>
          <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 20 }}>Welcome back, {me.vendorName ?? me.user.displayName}</div>
          <div style={{ fontSize: 13, color: "rgba(255,255,255,.82)", marginTop: 3 }}>
            Vendor #{me.vendorNumber} · Linked to City of Bozeman ERP · Profile {me.profileCompletePct}% complete
          </div>
        </div>
        <Button variant="teal" onClick={() => nav("/profile/company")}>Edit my record</Button>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 16, marginBottom: 22 }}>
        {stats.map((s) => (
          <div key={s.label} style={{ background: "#fff", border: "1px solid var(--border-1)", borderRadius: 8, padding: 18 }}>
            <div style={{ fontSize: 11, textTransform: "uppercase", letterSpacing: ".12em", color: "var(--fg-2)", fontWeight: 600 }}>{s.label}</div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 28, marginTop: 6 }}>{s.value}</div>
            <div style={{ fontSize: 12, color: "var(--fg-3)", marginTop: 2 }}>{s.note}</div>
          </div>
        ))}
      </div>

      <div style={{ fontSize: 13, fontWeight: 600, textTransform: "uppercase", letterSpacing: ".1em", color: "var(--fg-2)", marginBottom: 12 }}>Manage your record</div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 16 }}>
        {TILES.map((t) => (
          <button key={t.tab} onClick={() => nav(`/profile/${t.tab}`)} style={{ textAlign: "left", background: "#fff", border: "1px solid var(--border-1)", borderRadius: 10, padding: 20, cursor: "pointer" }}>
            <div style={{ width: 42, height: 42, borderRadius: 8, background: t.bg, color: t.fg, display: "flex", alignItems: "center", justifyContent: "center", marginBottom: 14 }}>
              <svg width="21" height="21" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><path d={t.icon} /></svg>
            </div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 15 }}>{t.title}</div>
            <div style={{ fontSize: 13, color: "var(--fg-2)", marginTop: 4, lineHeight: 1.45 }}>{t.desc}</div>
          </button>
        ))}
      </div>
    </AppShell>
  );
}
