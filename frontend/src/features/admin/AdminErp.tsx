import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, CardHeader, Button, Label, TextField, Spinner, Banner } from "../../ui";
import { adminApi, adminQk, useErpConfig, type ErpConfig, type ErpConfigUpdate, type ErpTestResult } from "../../api/adminClient";

// How the connector maps portal fields to the ERP — fixed by the connector implementation.
const FIELD_MAP = [
  { vss: "company.legalName", erp: "Supplier/FirstLineName", dir: "Two-way" },
  { vss: "address.remitTo", erp: "Supplier/Address/PostalAddress", dir: "Two-way" },
  { vss: "banking.routing", erp: "BankDetails/BankRoutingID", dir: "VSS → ERP" },
  { vss: "banking.account", erp: "BankDetails/BankAccountID", dir: "VSS → ERP" },
  { vss: "documents.*", erp: "AttachmentFolder/Document", dir: "VSS → ERP" },
];

export function AdminErp() {
  const qc = useQueryClient();
  const { data: cfg, isLoading } = useErpConfig();
  const [result, setResult] = useState<ErpTestResult | null>(null);
  const [form, setForm] = useState<ErpConfig | null>(null);

  useEffect(() => { if (cfg) setForm(cfg); }, [cfg]);

  const test = useMutation({ mutationFn: () => adminApi.testErp(), onSuccess: setResult });
  const save = useMutation({
    mutationFn: (body: ErpConfigUpdate) => adminApi.saveErpConfig(body),
    onSuccess: (updated) => { setForm(updated); qc.invalidateQueries({ queryKey: adminQk.erpConfig }); } });

  if (isLoading || !form) return <AppShell title="ERP integration" crumb="Administration"><Spinner /></AppShell>;

  const isSap = form.provider === "SapByDesign";
  const isBc = form.provider === "BusinessCentral";
  const isStub = form.provider === "Stub";
  const set = (k: keyof ErpConfig) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value });
  const onSave = () => save.mutate({
    baseUrl: form.baseUrl, principalId: form.principalId, querySupplierPath: form.querySupplierPath,
    manageSupplierPath: form.manageSupplierPath, sampleId: form.sampleId, tenantId: form.tenantId, scope: form.scope, companyId: form.companyId });

  return (
    <AppShell title="ERP integration" crumb="Administration">
      <Banner tone="info">Connection settings are stored in the ERP config table and read by <code>IErpClient</code> at request time. Secrets are not shown or stored here — they come from the deployment's secret store (user-secrets / env).</Banner>
      <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: 20, alignItems: "start" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          <Card>
            <CardHeader title="Connection profile" />
            <div style={{ padding: 22, display: "grid", gridTemplateColumns: "1fr 1fr", gap: "18px 22px" }}>
              <div><Label>Provider</Label><TextField value={form.provider} disabled style={{ fontFamily: "var(--font-mono)" }} /></div>
              <div><Label>Authentication</Label><TextField value={form.authMode} disabled /></div>
              <div style={{ gridColumn: "span 2" }}><Label>ERP base URL</Label><TextField value={form.baseUrl} onChange={set("baseUrl")} disabled={isStub} style={{ fontFamily: "var(--font-mono)" }} /></div>

              {isSap && <>
                <div><Label>Technical user</Label><TextField value={form.principalId} onChange={set("principalId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div><Label>Sample supplier ID</Label><TextField value={form.sampleId} onChange={set("sampleId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div style={{ gridColumn: "span 2" }}><Label>QuerySupplierIn path</Label><TextField value={form.querySupplierPath} onChange={set("querySupplierPath")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div style={{ gridColumn: "span 2" }}><Label>ManageSupplierIn path</Label><TextField value={form.manageSupplierPath} onChange={set("manageSupplierPath")} style={{ fontFamily: "var(--font-mono)" }} /></div>
              </>}

              {isBc && <>
                <div><Label>Client ID</Label><TextField value={form.principalId} onChange={set("principalId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div><Label>Company ID</Label><TextField value={form.companyId} onChange={set("companyId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div><Label>Tenant ID</Label><TextField value={form.tenantId} onChange={set("tenantId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div><Label>Sample vendor no.</Label><TextField value={form.sampleId} onChange={set("sampleId")} style={{ fontFamily: "var(--font-mono)" }} /></div>
                <div style={{ gridColumn: "span 2" }}><Label>Scope</Label><TextField value={form.scope} onChange={set("scope")} style={{ fontFamily: "var(--font-mono)" }} /></div>
              </>}

              <div style={{ gridColumn: "span 2" }}>
                <Label>Secret ({isBc ? "client secret" : "password"})</Label>
                <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "9px 12px", borderRadius: 6, background: "var(--bg-2)", fontSize: 13, color: form.secretConfigured ? "var(--colorStatusSuccessForeground1)" : "#817400" }}>
                  <span style={{ width: 8, height: 8, borderRadius: 999, background: "currentColor" }} />
                  {form.secretConfigured ? "Configured via secret store" : "Not configured — set via user-secrets / env"}
                </div>
              </div>
            </div>
          </Card>
          <Card>
            <CardHeader title="Field mapping (connector reference)" />
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead><tr style={{ background: "var(--bg-2)" }}>
                {["VSS field", "ERP field", "Direction"].map((c) => <th key={c} style={th}>{c}</th>)}
              </tr></thead>
              <tbody>
                {FIELD_MAP.map((m) => (
                  <tr key={m.vss} style={{ borderBottom: "1px solid var(--colorNeutralStroke3)" }}>
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
            <div style={{ marginTop: 2, display: "flex", flexDirection: "column", gap: 10, fontSize: 13, color: "var(--fg-2)" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Provider</span><b style={{ color: "var(--fg-1)" }}>{form.provider}</b></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Auth</span><b style={{ color: "var(--fg-1)" }}>{form.authMode}</b></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Secret</span><b style={{ color: "var(--fg-1)" }}>{form.secretConfigured ? "Configured" : "Missing"}</b></div>
              <div style={{ display: "flex", justifyContent: "space-between" }}><span>Last updated</span><b style={{ color: "var(--fg-1)" }}>{form.updatedAt ? new Date(form.updatedAt).toLocaleString() : "—"}</b></div>
            </div>
            <Button variant="outline" style={{ width: "100%", marginTop: 18 }} disabled={test.isPending} onClick={() => test.mutate()}>
              {test.isPending ? "Testing…" : "Test connection"}
            </Button>
            {result && (
              <div style={{ marginTop: 10, padding: "10px 12px", borderRadius: 8, fontSize: 12, fontWeight: 600, background: result.ok ? "var(--colorStatusSuccessBackground1)" : "var(--colorStatusDangerBackground1)", color: result.ok ? "var(--colorStatusSuccessForeground1)" : "var(--colorStatusDangerForeground1)" }}>
                {result.provider} · {result.ok ? "OK" : "Failed"} · {result.latencyMs}ms
                <div style={{ fontWeight: 400, marginTop: 2 }}>{result.message}</div>
              </div>
            )}
            <Button variant="teal" style={{ width: "100%", marginTop: 10 }} disabled={isStub || save.isPending} onClick={onSave}>
              {save.isPending ? "Saving…" : "Save configuration"}
            </Button>
            {save.isSuccess && <div style={{ marginTop: 8, fontSize: 12, color: "var(--colorStatusSuccessForeground1)", textAlign: "center" }}>Saved — applied on the next ERP call.</div>}
            {save.isError && <div style={{ marginTop: 8, fontSize: 12, color: "var(--colorStatusDangerForeground1)", textAlign: "center" }}>{(save.error as Error).message}</div>}
          </Card>
        </div>
      </div>
    </AppShell>
  );
}

const th = { padding: "10px 22px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "12px 22px", fontSize: 13, color: "var(--fg-1)" };
