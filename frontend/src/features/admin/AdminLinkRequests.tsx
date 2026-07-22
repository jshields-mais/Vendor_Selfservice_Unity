import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, CardHeader, Button, StatusPill, Spinner } from "../../ui";
import { useAdminLinkRequests, adminApi, adminQk } from "../../api/adminClient";

export function AdminLinkRequests() {
  const qc = useQueryClient();
  const { data: rows, isLoading } = useAdminLinkRequests();

  const refresh = () => Promise.all([
    qc.invalidateQueries({ queryKey: adminQk.linkRequests }),
    qc.invalidateQueries({ queryKey: adminQk.stats }),
    qc.invalidateQueries({ queryKey: adminQk.vendors }),
  ]);
  const approve = useMutation({ mutationFn: (id: string) => adminApi.approveLink(id), onSuccess: refresh });
  const reject = useMutation({ mutationFn: (id: string) => adminApi.rejectLink(id), onSuccess: refresh });
  const busy = approve.isPending || reject.isPending;

  return (
    <AppShell title="Link requests" crumb="Administration">
      <Card>
        <CardHeader title="Pending link requests" />
        {isLoading || !rows ? <Spinner /> : (
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead><tr style={{ background: "var(--bg-2)" }}>
              {["Company", "Match method", "Matched record", "Submitted", "Status", ""].map((c) => <th key={c} style={th}>{c}</th>)}
            </tr></thead>
            <tbody>
              {rows.map((r) => {
                const pending = r.status === "Pending" || r.status === "Matched";
                return (
                  <tr key={r.id} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)" }}>
                    <td style={{ ...td, fontWeight: 600 }}>{r.company}<div style={{ fontSize: 12, color: "var(--fg-3)", fontWeight: 400 }}>{r.email}</div></td>
                    <td style={td}>{r.method}</td>
                    <td style={{ ...td, fontFamily: "var(--font-mono)" }}>{r.matchedVendorNumber ?? "—"}</td>
                    <td style={td}>{new Date(r.createdAt).toLocaleString()}</td>
                    <td style={td}><StatusPill status={r.status} /></td>
                    <td style={{ ...td, textAlign: "right" }}>
                      {pending && (
                        <div style={{ display: "inline-flex", gap: 8 }}>
                          <Button variant="teal" style={{ padding: "7px 14px", fontSize: 13 }} disabled={busy} onClick={() => approve.mutate(r.id)}>Approve</Button>
                          <Button variant="danger" style={{ padding: "7px 12px", fontSize: 13 }} disabled={busy} onClick={() => reject.mutate(r.id)}>Reject</Button>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 && <tr><td style={td} colSpan={6}>No link requests.</td></tr>}
            </tbody>
          </table>
        )}
      </Card>
    </AppShell>
  );
}

const th = { padding: "11px 22px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, textTransform: "uppercase" as const, letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "14px 22px", fontSize: 13, color: "var(--fg-1)", verticalAlign: "middle" as const };
