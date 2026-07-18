import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, CardHeader, Button, Label, TextField, SelectField, Banner } from "../../ui";
import { adminApi, type ErpTestResult } from "../../api/adminClient";

/**
 * ERP integration config. Presentational for now — reflects the connection the
 * backend uses (ConfigService.integrationV2ApiUrl + IErpClient). Wire to a real
 * config endpoint when the UnityErpClient lands.
 */
const FIELD_MAP = [
  { vss: "company.legalName", erp: "VENDOR.NAME", dir: "Two-way" },
  { vss: "banking.routing", erp: "VENDOR.ACH_ROUTING", dir: "VSS → ERP" },
  { vss: "banking.account", erp: "VENDOR.ACH_ACCT", dir: "VSS → ERP" },
  { vss: "tax.ein", erp: "VENDOR.TAX_ID", dir: "Two-way" },
  { vss: "address.remitTo", erp: "VENDOR.REMIT_ADDR", dir: "Two-way" },
];
const SYNC_LOG = [
  { text: "Vendor master pull — 6 records", time: "Today 09:42", color: "var(--status-success)" },
  { text: "Change pushed to ERP (V-10485)", time: "Today 08:10", color: "var(--color-teal)" },
  { text: "Token refreshed", time: "Today 06:00", color: "var(--fg-3)" },
];

export function AdminErp() {
  const [result, setResult] = useState<ErpTestResult | null>(null);
  const test = useMutation({ mutationFn: () => adminApi.testErp(), onSuccess: setResult });

  return (
    <AppShell title="ERP integration" crumb="Administration">
      <Banner tone="info">Config shown is the interface the backend uses (`IErpClient`). It's stubbed today; wire to a real config store when `UnityErpClient` is implemented.</Banner>
      <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: 20, alignItems: "start" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          <Card>
            <CardHeader title="Connection profile" />
            <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 1fr", gap: "18px 22px" }}>
              <div style={{ gridColumn: "span 2" }}><Label>ERP base URL</Label><TextField defaultValue="https://gateway.unitydev.ca/IntegrationService/api/v2" style={{ fontFamily: "var(--font-mono)" }} /></div>
              <div><Label>Authentication</Label><SelectField options={["OAuth 2.0 (client credentials)", "API key (header)", "Bearer token"]} /></div>
              <div><Label>Token endpoint</Label><TextField defaultValue="/oauth/token" style={{ fontFamily: "var(--font-mono)" }} /></div>
              <div><Label>Client ID</Label><TextField defaultValue="vss-bozeman-prod" style={{ fontFamily: "var(--font-mono)" }} /></div>
              <div><Label>Client secret</Label><TextField type="password" defaultValue="••••••••••••" style={{ fontFamily: "var(--font-mono)" }} /></div>
            </div>
          </Card>
          <Card>
            <CardHeader title="Field mapping" />
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead><tr style={{ background: "var(--bg-2)" }}>
                {["VSS field", "ERP field", "Direction"].map((c) => <th key={c} style={th}>{c}</th>)}
              </tr></thead>
              <tbody>
                {FIELD_MAP.map((m) => (
                  <tr key={m.vss} style={{ borderBottom: "1px solid #F0F1F2" }}>
                    <td style={{ ...td, fontFamily: "var(--font-mono)" }}>{m.vss}</td>
                    <td style={{ ...td, fontFamily: "var(--font-mono)" }}>{m.erp}</td>
                    <td style={{ ...td, color: "var(--fg-2)" }}>{m.dir}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 20, position: "sticky", top: 0 }}>
          <Card style={{ padding: 22 }}>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 15, marginBottom: 14 }}>Connection status</div>
            <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 14px", borderRadius: 8, background: "#DFF3E8", color: "#19663F", fontSize: 14, fontWeight: 600 }}><span style={{ width: 9, height: 9, borderRadius: 999, background: "currentColor" }} />Connected · last sync 6m ago</div>
            <div style={{ marginTop: 16, display: "flex", flexDirection: "column", gap: 10, fontSize: 13, color: "var(--fg-2)" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Environment</span><b style={{ color: "var(--fg-1)" }}>Stub (dev)</b></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>API version</span><b style={{ color: "var(--fg-1)" }}>v2</b></div>
            </div>
            <Button variant="outline" style={{ width: "100%", marginTop: 18 }} disabled={test.isPending} onClick={() => test.mutate()}>
              {test.isPending ? "Testing…" : "Test connection"}
            </Button>
            {result && (
              <div style={{ marginTop: 10, padding: "10px 12px", borderRadius: 8, fontSize: 12, fontWeight: 600, background: result.ok ? "#DFF3E8" : "#FBE3E1", color: result.ok ? "#19663F" : "#8A231E" }}>
                {result.provider} · {result.ok ? "OK" : "Failed"} · {result.latencyMs}ms
                <div style={{ fontWeight: 400, marginTop: 2 }}>{result.message}</div>
              </div>
            )}
            <Button variant="teal" style={{ width: "100%", marginTop: 10 }}>Save configuration</Button>
          </Card>
          <Card style={{ padding: 22 }}>
            <div style={{ fontFamily: "var(--font-display)", fontWeight: 600, fontSize: 15, marginBottom: 12 }}>Recent sync log</div>
            {SYNC_LOG.map((l) => (
              <div key={l.text} style={{ display: "flex", gap: 10, padding: "9px 0", borderBottom: "1px solid #F0F1F2" }}>
                <span style={{ width: 8, height: 8, borderRadius: 999, background: l.color, marginTop: 5, flex: "0 0 8px" }} />
                <div style={{ fontSize: 13, color: "var(--fg-1)", lineHeight: 1.4 }}>{l.text}<div style={{ fontSize: 11, color: "var(--fg-3)", marginTop: 1 }}>{l.time}</div></div>
              </div>
            ))}
          </Card>
        </div>
      </div>
    </AppShell>
  );
}

const th = { padding: "10px 22px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, textTransform: "uppercase" as const, letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "12px 22px", fontSize: 13, color: "var(--fg-1)" };
