import { useNavigate } from "react-router-dom";
import { AppShell } from "../../layout/AppShell";
import { Card, CardHeader, StatusPill, Spinner } from "../../ui";
import { useAdminChangeRequests } from "../../api/adminClient";

export function AdminChangeRequests() {
  const nav = useNavigate();
  const { data: rows, isLoading } = useAdminChangeRequests();

  return (
    <AppShell title="Change requests" crumb="Administration">
      <Card>
        <CardHeader title="Vendor change requests" />
        {isLoading || !rows ? <Spinner /> : (
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead><tr style={{ background: "var(--bg-2)" }}>
              {["Request", "Vendor", "Section", "Submitted", "Status", ""].map((c) => (
                <th key={c} style={th}>{c}</th>
              ))}
            </tr></thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} onClick={() => nav(`/admin/change-requests/${r.id}`)} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)", cursor: "pointer" }}>
                  <td style={{ ...td, fontFamily: "var(--font-mono)", color: "var(--fg-2)" }}>{r.code}</td>
                  <td style={{ ...td, fontWeight: 600 }}>{r.vendorName}</td>
                  <td style={td}>{r.section}</td>
                  <td style={td}>{new Date(r.submittedAt).toLocaleString()}</td>
                  <td style={td}><StatusPill status={r.status} /></td>
                  <td style={{ ...td, textAlign: "right" }}><span style={{ color: "var(--color-teal)", fontWeight: 600 }}>Review →</span></td>
                </tr>
              ))}
              {rows.length === 0 && <tr><td style={td} colSpan={6}>No change requests.</td></tr>}
            </tbody>
          </table>
        )}
      </Card>
    </AppShell>
  );
}

const th = { padding: "11px 22px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, textTransform: "uppercase" as const, letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "14px 22px", fontSize: 13, color: "var(--fg-1)" };
