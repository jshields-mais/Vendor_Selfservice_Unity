import { useState, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useApiQuery } from "@univerus/udp-react-enterprise-component-library";
import { useAuth } from "../auth/authProvider";
import { useMe, VSS_BASE } from "../api/vssClient";
import type { AdminStats } from "../api/adminClient";

const ICONS: Record<string, string> = {
  home: "M3 9.5 12 3l9 6.5V21a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z",
  company: "M3 21V7a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v14M3 21h18M7 9h2M7 13h2M7 17h2M12 9h1M12 13h1",
  docs: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M9 13h6M9 17h4",
  tag: "M20.6 13.4 12 22l-9-9V4h9zM7.5 7.5h.01",
  contacts: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75",
  link: "M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1",
  changes: "M3 12a9 9 0 0 1 15-6.7L21 8M21 3v5h-5M21 12a9 9 0 0 1-15 6.7L3 16M3 21v-5h5",
  plug: "M9 2v6M15 2v6M7 8h10v3a5 5 0 0 1-10 0zM12 16v6",
  pin: "M12 21s-7-6.3-7-11a7 7 0 0 1 14 0c0 4.7-7 11-7 11zM12 12a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z",
  bank: "M3 10 12 4l9 6M4 10v8M20 10v8M9 10v8M15 10v8M3 21h18",
  receipt: "M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4",
};

interface NavItem { label: string; icon: keyof typeof ICONS; to: string; badge?: number; }

const VENDOR_NAV: NavItem[] = [
  { label: "Home", icon: "home", to: "/console" },
  { label: "Company profile", icon: "company", to: "/profile/company" },
  { label: "Contacts", icon: "contacts", to: "/profile/contacts" },
  { label: "Addresses", icon: "pin", to: "/profile/addresses" },
  { label: "Banking & remittance", icon: "bank", to: "/profile/banking" },
  { label: "Tax & W-9", icon: "receipt", to: "/profile/tax" },
  { label: "Documents", icon: "docs", to: "/profile/documents" },
  { label: "Category codes", icon: "tag", to: "/profile/categories" },
];

function initials(name: string) {
  return name.split(" ").map((p) => p[0]).slice(0, 2).join("").toUpperCase();
}

export function AppShell({ title, crumb, children }: { title: string; crumb: string; children: ReactNode }) {
  const nav = useNavigate();
  const loc = useLocation();
  const { account, role, setRole, mode, logout } = useAuth();
  const { data: me } = useMe();
  const isAdmin = role === "admin";

  const { data: stats } = useApiQuery<AdminStats>(VSS_BASE, "api/v1/admin/stats", undefined, { enabled: isAdmin });

  // Inner-menu is collapsible from the app-bar hamburger; when off the grid collapses to 0.
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem("vss.nav.collapsed") === "1");
  const toggleNav = () => setCollapsed((c) => { const n = !c; localStorage.setItem("vss.nav.collapsed", n ? "1" : "0"); return n; });

  const adminNav: NavItem[] = [
    { label: "Home", icon: "home", to: "/admin" },
    { label: "Vendors", icon: "contacts", to: "/admin/vendors" },
    { label: "Link requests", icon: "link", to: "/admin/link-requests", badge: stats?.pendingLinks },
    { label: "Change requests", icon: "changes", to: "/admin/change-requests", badge: stats?.pendingChanges },
    { label: "ERP integration", icon: "plug", to: "/admin/erp" },
    { label: "Document types", icon: "changes", to: "/admin/document-types" },
  ];
  const items = isAdmin ? adminNav : VENDOR_NAV;

  const isActive = (to: string) =>
    loc.pathname === to || (to !== "/admin" && to !== "/console" && loc.pathname.startsWith(to));

  const conn = isAdmin
    ? { bg: "var(--colorStatusSuccessBackground1)", fg: "var(--colorStatusSuccessForeground1)", label: "ERP connected" }
    : me?.linkState === "Linked"
      ? { bg: "var(--colorBrandBackground2)", fg: "var(--colorBrandForeground2)", label: `Linked · ${me?.vendorNumber}` }
      : { bg: "var(--colorStatusWarningBackground1)", fg: "var(--colorStatusWarningForeground1)", label: "Not linked" };

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100vh" }}>
      {/* ---- Top app bar (navy chrome): waffle + product + breadcrumb + status + user ---- */}
      <header style={{ flex: "0 0 48px", height: 48, background: "var(--colorAppHeader)", color: "var(--colorAppHeaderForeground)", display: "flex", alignItems: "center", padding: "0 12px", gap: 12 }}>
        <button onClick={toggleNav} title={collapsed ? "Show menu" : "Hide menu"} aria-label={collapsed ? "Show menu" : "Hide menu"}
          style={{ display: "flex", padding: 8, border: "none", background: "transparent", borderRadius: 4, cursor: "pointer", color: "#fff" }}>
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><path d="M3 6h18M3 12h18M3 18h18" /></svg>
        </button>
        {/* App-switcher waffle (9 dots) — text-only header per the design standard, no logo image. */}
        <div title="Univerus apps" style={{ display: "grid", gridTemplateColumns: "repeat(3,4px)", gridTemplateRows: "repeat(3,4px)", gap: 3, padding: 8, borderRadius: 4, cursor: "pointer" }}>
          {Array.from({ length: 9 }).map((_, i) => <span key={i} style={{ width: 4, height: 4, background: "#fff", borderRadius: "50%" }} />)}
        </div>
        <span style={{ font: "600 16px/22px var(--font-sans)" }}>Univerus VSS</span>
        <span style={{ color: "rgba(255,255,255,.7)", font: "400 13px/18px var(--font-sans)", display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}>
          <span style={{ opacity: .5 }}>/</span>
          <span style={{ color: "#fff", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{crumb}</span>
        </span>
        <span style={{ flex: 1 }} />
        <span style={{ display: "inline-flex", alignItems: "center", gap: 7, padding: "4px 10px", borderRadius: 999, background: conn.bg, color: conn.fg, fontSize: 12, fontWeight: 600, whiteSpace: "nowrap" }}>
          <span style={{ width: 7, height: 7, borderRadius: 999, background: "currentColor" }} />
          {conn.label}
        </span>
        <span title={account?.name} style={{ width: 28, height: 28, borderRadius: "50%", background: "#5b8bc0", color: "#fff", display: "inline-flex", alignItems: "center", justifyContent: "center", font: "600 12px/16px var(--font-sans)" }}>
          {initials(account?.name ?? "?")}
        </span>
      </header>

      {/* ---- Body: light inner-menu (224px) + content ---- */}
      <div style={{ display: "grid", gridTemplateColumns: collapsed ? "0 1fr" : "224px 1fr", flex: 1, minHeight: 0 }}>
        <nav style={{ background: "var(--colorNeutralBackground1)", borderRight: "1px solid var(--colorNeutralStroke2)", display: collapsed ? "none" : "flex", flexDirection: "column", overflow: "hidden" }}>
          <div style={{ padding: "8px 0", flex: 1, overflowY: "auto" }}>
            {items.map((item) => {
              const active = isActive(item.to);
              return (
                <button key={item.to} onClick={() => nav(item.to)} style={{
                  display: "flex", alignItems: "center", gap: 12, padding: "8px 16px", width: "100%", textAlign: "left",
                  border: "none", borderLeft: `2px solid ${active ? "var(--colorBrandStroke1)" : "transparent"}`,
                  cursor: "pointer", background: active ? "var(--colorBrandBackground2)" : "transparent",
                  color: active ? "var(--colorBrandForeground2)" : "var(--colorNeutralForeground1)",
                  font: `${active ? 600 : 400} 14px/20px var(--font-sans)`,
                }}>
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><path d={ICONS[item.icon]} /></svg>
                  <span style={{ flex: 1 }}>{item.label}</span>
                  {!!item.badge && (
                    <span style={{ minWidth: 18, height: 18, padding: "0 5px", borderRadius: 999, background: "var(--colorBrandBackground)", color: "#fff", fontSize: 11, fontWeight: 700, display: "flex", alignItems: "center", justifyContent: "center" }}>{item.badge}</span>
                  )}
                </button>
              );
            })}
          </div>

          {/* footer: identity + demo role toggle + sign out */}
          <div style={{ borderTop: "1px solid var(--colorNeutralStroke2)", padding: 12 }}>
            <div style={{ lineHeight: 1.3, marginBottom: mode === "dev" ? 8 : 4 }}>
              <div style={{ fontWeight: 600, fontSize: 13, color: "var(--colorNeutralForeground1)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{account?.name}</div>
              <div style={{ fontSize: 12, color: "var(--colorNeutralForeground3)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{isAdmin ? "City of Bozeman" : me?.vendorName ?? ""}</div>
            </div>
            {mode === "dev" && (
              <button onClick={() => { setRole(isAdmin ? "vendor" : "admin"); nav("/"); }} style={{ width: "100%", padding: "7px 10px", border: "1px solid var(--colorNeutralStroke1)", borderRadius: "var(--radius-md)", background: "var(--colorNeutralBackground1)", color: "var(--colorNeutralForeground2)", font: "600 12px/16px var(--font-sans)", cursor: "pointer" }}>
                {isAdmin ? "Demo: view as vendor" : "Demo: view as City staff"}
              </button>
            )}
            <button onClick={() => { logout(); nav("/login"); }} style={{ width: "100%", marginTop: 6, padding: "7px 10px", border: "none", borderRadius: "var(--radius-md)", background: "transparent", color: "var(--colorNeutralForeground3)", font: "400 12px/16px var(--font-sans)", cursor: "pointer" }}>Sign out</button>
          </div>
        </nav>

        {/* content column */}
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0, overflow: "hidden" }}>
          <div style={{ padding: "16px 24px", background: "var(--colorNeutralBackground1)", borderBottom: "1px solid var(--colorNeutralStroke2)" }}>
            <h1 style={{ font: "600 28px/36px var(--font-display)", color: "var(--colorNeutralForeground1)" }}>{title}</h1>
          </div>
          <main style={{ flex: 1, overflow: "auto", padding: 24, background: "var(--colorNeutralBackground2)" }}>{children}</main>
        </div>
      </div>
    </div>
  );
}
