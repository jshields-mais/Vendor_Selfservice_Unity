import { AppShell } from "../../layout/AppShell";
import { Card, CardHeader, StatusPill, Spinner } from "../../ui";
import { useAdminVendors } from "../../api/adminClient";

export function AdminVendors() {
  const { data: rows, isLoading } = useAdminVendors();
  return (
    <AppShell title="Vendors" crumb="Administration">
      <Card>
        <CardHeader title="Linked vendors" />
        {isLoading || !rows ? <Spinner /> : (
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead><tr style={{ background: "var(--bg-2)" }}>
              {["Vendor #", "Name", "Category", "Last sync", "Status"].map((c) => <th key={c} style={th}>{c}</th>)}
            </tr></thead>
            <tbody>
              {rows.map((v) => (
                <tr key={v.number} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)" }}>
                  <td style={{ ...td, fontFamily: "var(--font-mono)", color: "var(--fg-2)" }}>{v.number}</td>
                  <td style={{ ...td, fontWeight: 600 }}>{v.name}</td>
                  <td style={td}>{v.category}</td>
                  <td style={td}>{v.lastSync ? new Date(v.lastSync).toLocaleString() : "—"}</td>
                  <td style={td}><StatusPill status={v.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>
    </AppShell>
  );
}

const th = { padding: "11px 22px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, textTransform: "uppercase" as const, letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "14px 22px", fontSize: 13, color: "var(--fg-1)" };
