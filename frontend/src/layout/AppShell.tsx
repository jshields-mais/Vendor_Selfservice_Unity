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

// Vendor-facing menu: the profile sections now live in the side sheet itself.
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

  // Collapsible side sheet — toggled from the header; when off it auto-hides. Persisted.
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
      ? { bg: "var(--bg-accent-soft)", fg: "var(--color-teal-700)", label: `Linked · ${me?.vendorNumber}` }
      : { bg: "#fef7b2", fg: "#817400", label: "Not linked" };

  return (
    <div style={{ display: "flex", minHeight: "100vh" }}>
      {!collapsed && (
      <aside style={{ width: 236, flex: "0 0 236px", background: "var(--color-navy)", color: "#fff", display: "flex", flexDirection: "column", padding: "20px 0" }}>
        <div style={{ padding: "0 20px 22px", display: "flex", alignItems: "center", gap: 11, borderBottom: "1px solid rgba(255,255,255,.09)" }}>
          <div style={{ width: 34, height: 34, borderRadius: 6, background: "var(--color-teal)", display: "flex", alignItems: "center", justifyContent: "center", fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 17 }}>V</div>
          <div>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 15 }}>Univerus VSS</div>
            <div style={{ fontSize: 10, letterSpacing: ".14em", textTransform: "uppercase", color: "var(--colorBrandStroke2)" }}>{isAdmin ? "Administration" : "Vendor portal"}</div>
          </div>
        </div>

        <nav style={{ padding: "16px 12px", flex: 1, display: "flex", flexDirection: "column", gap: 2 }}>
          {items.map((item) => {
            const active = isActive(item.to);
            return (
              <button key={item.to} onClick={() => nav(item.to)} style={{
                width: "100%", display: "flex", alignItems: "center", gap: 11, padding: "10px 13px",
                border: "none", borderLeft: active ? "3px solid var(--color-teal)" : "3px solid transparent",
                borderRadius: 4, cursor: "pointer", background: active ? "rgba(255,255,255,.10)" : "transparent",
                color: active ? "#fff" : "rgba(255,255,255,.72)", fontFamily: "var(--font-sans)", fontSize: 14, fontWeight: active ? 600 : 500, textAlign: "left",
              }}>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><path d={ICONS[item.icon]} /></svg>
                <span style={{ flex: 1 }}>{item.label}</span>
                {!!item.badge && (
                  <span style={{ minWidth: 20, height: 20, padding: "0 6px", borderRadius: 999, background: "var(--color-orange)", color: "#fff", fontSize: 11, fontWeight: 700, display: "flex", alignItems: "center", justifyContent: "center" }}>{item.badge}</span>
                )}
              </button>
            );
          })}
        </nav>

        <div style={{ padding: "0 14px" }}>
          <div style={{ padding: "12px 14px", borderRadius: 8, background: "rgba(255,255,255,.06)", display: "flex", alignItems: "center", gap: 11 }}>
            <div style={{ width: 32, height: 32, borderRadius: 999, background: "var(--color-teal)", display: "flex", alignItems: "center", justifyContent: "center", fontWeight: 700, fontSize: 12 }}>{initials(account?.name ?? "?")}</div>
            <div style={{ flex: 1, minWidth: 0, lineHeight: 1.25 }}>
              <div style={{ fontWeight: 600, fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{account?.name}</div>
              <div style={{ fontSize: 11, color: "rgba(255,255,255,.6)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{isAdmin ? "City of Bozeman" : me?.vendorName ?? "Northstar Supply Co."}</div>
            </div>
          </div>
          {mode === "dev" && (
            <button onClick={() => { setRole(isAdmin ? "vendor" : "admin"); nav("/"); }} style={{ width: "100%", marginTop: 8, padding: 9, border: "1px solid rgba(255,255,255,.16)", borderRadius: 6, background: "transparent", color: "rgba(255,255,255,.8)", fontFamily: "var(--font-sans)", fontSize: 12, fontWeight: 600, cursor: "pointer" }}>
              {isAdmin ? "Demo: view as vendor" : "Demo: view as City staff"}
            </button>
          )}
          <button onClick={() => { logout(); nav("/login"); }} style={{ width: "100%", marginTop: 8, padding: 9, border: "none", borderRadius: 6, background: "transparent", color: "rgba(255,255,255,.55)", fontFamily: "var(--font-sans)", fontSize: 12, cursor: "pointer" }}>Sign out</button>
        </div>
      </aside>
      )}

      <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column" }}>
        <header style={{ height: 62, flex: "0 0 62px", background: "#fff", borderBottom: "1px solid var(--border-1)", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 28px", gap: 16 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 14, minWidth: 0 }}>
            <button
              onClick={toggleNav}
              title={collapsed ? "Show menu" : "Hide menu"}
              aria-label={collapsed ? "Show menu" : "Hide menu"}
              style={{ flex: "0 0 auto", width: 36, height: 36, display: "flex", alignItems: "center", justifyContent: "center", border: "1px solid var(--border-1)", borderRadius: 8, background: "#fff", cursor: "pointer", color: "var(--fg-1)" }}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round"><path d="M3 6h18M3 12h18M3 18h18" /></svg>
            </button>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontSize: 11, color: "var(--fg-3)", letterSpacing: ".06em" }}>{crumb}</div>
              <h1 style={{ fontSize: 19, lineHeight: 1.1 }}>{title}</h1>
            </div>
          </div>
          <div style={{ display: "inline-flex", alignItems: "center", gap: 7, padding: "6px 12px", borderRadius: 999, background: conn.bg, color: conn.fg, fontSize: 12, fontWeight: 600 }}>
            <span style={{ width: 8, height: 8, borderRadius: 999, background: "currentColor" }} />
            {conn.label}
          </div>
        </header>
        <main style={{ flex: 1, overflow: "auto", padding: 28 }}>{children}</main>
      </div>
    </div>
  );
}
