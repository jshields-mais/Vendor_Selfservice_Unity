import { useNavigate } from "react-router-dom";
import { AppShell } from "../../layout/AppShell";
import { Spinner } from "../../ui";
import { useAdminStats } from "../../api/adminClient";

const ICON = {
  plug: "M9 2v6M15 2v6M7 8h10v3a5 5 0 0 1-10 0zM12 16v6",
  link: "M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1",
  changes: "M3 12a9 9 0 0 1 15-6.7L21 8M21 3v5h-5M21 12a9 9 0 0 1-15 6.7L3 16M3 21v-5h5",
  contacts: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z",
  report: "M3 3v18h18M7 15l3-4 3 3 5-7",
  settings: "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H4a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 5.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H10a1.65 1.65 0 0 0 1-1.51V4a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V10a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" };

export function AdminConsole() {
  const nav = useNavigate();
  const { data: s, isLoading } = useAdminStats();
  if (isLoading || !s) return <AppShell title="VSS admin console" crumb="Administration"><Spinner /></AppShell>;

  const stats = [
    { label: "ERP connection", value: s.erpStatus, note: "Last sync 6m ago", color: "var(--fg-3)" },
    { label: "Pending links", value: String(s.pendingLinks), note: "Awaiting approval", color: "var(--color-orange)" },
    { label: "Pending changes", value: String(s.pendingChanges), note: "Awaiting review", color: "var(--color-orange)" },
    { label: "Linked vendors", value: String(s.linkedVendors), note: "Active suppliers", color: "var(--status-success)" },
  ];
  const tiles = [
    { title: "ERP integration", desc: "Endpoints, auth, and field mapping", icon: ICON.plug, to: "/admin/erp", bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)" },
    { title: "Link requests", desc: "Approve vendors linking to records", icon: ICON.link, to: "/admin/link-requests", bg: "#fdf6f3", fg: "var(--color-orange)", badge: s.pendingLinks },
    { title: "Change requests", desc: "Review edits before ERP sync", icon: ICON.changes, to: "/admin/change-requests", bg: "#fef7b2", fg: "#817400", badge: s.pendingChanges },
    { title: "Vendors", desc: "All linked supplier records", icon: ICON.contacts, to: "/admin/vendors", bg: "var(--colorBrandBackground2)", fg: "var(--color-navy)" },
    { title: "Document types", desc: "Configure upload document types", icon: ICON.changes, to: "/admin/document-types", bg: "#fef7b2", fg: "#817400" },
    { title: "Reports", desc: "Activity, sync health, exports", icon: ICON.report, to: "/admin", bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)" },
    { title: "Settings", desc: "Roles, notifications, branding", icon: ICON.settings, to: "/admin", bg: "var(--colorBrandBackground2)", fg: "var(--color-navy)" },
  ];

  return (
    <AppShell title="VSS admin console" crumb="Administration">
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 16, marginBottom: 22 }}>
        {stats.map((x) => (
          <div key={x.label} style={{ background: "#fff", border: "1px solid var(--border-1)", borderRadius: 8, padding: 18 }}>
            <div style={{ fontSize: 11, color: "var(--fg-2)", fontWeight: 600 }}>{x.label}</div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 28, marginTop: 6 }}>{x.value}</div>
            <div style={{ fontSize: 12, color: x.color, marginTop: 2 }}>{x.note}</div>
          </div>
        ))}
      </div>
      <div style={{ fontSize: 13, fontWeight: 600, color: "var(--fg-2)", marginBottom: 12 }}>Consoles</div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3,1fr)", gap: 16 }}>
        {tiles.map((t) => (
          <button key={t.title} onClick={() => nav(t.to)} style={{ textAlign: "left", background: "#fff", border: "1px solid var(--border-1)", borderRadius: 10, padding: 22, cursor: "pointer", position: "relative" }}>
            {!!t.badge && <span style={{ position: "absolute", top: 18, right: 18, minWidth: 22, height: 22, padding: "0 7px", borderRadius: 999, background: "var(--color-orange)", color: "#fff", fontSize: 12, fontWeight: 700, display: "flex", alignItems: "center", justifyContent: "center" }}>{t.badge}</span>}
            <div style={{ width: 44, height: 44, borderRadius: 8, background: t.bg, color: t.fg, display: "flex", alignItems: "center", justifyContent: "center", marginBottom: 14 }}>
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><path d={t.icon} /></svg>
            </div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 16 }}>{t.title}</div>
            <div style={{ fontSize: 13, color: "var(--fg-2)", marginTop: 5, lineHeight: 1.45 }}>{t.desc}</div>
          </button>
        ))}
      </div>
    </AppShell>
  );
}
