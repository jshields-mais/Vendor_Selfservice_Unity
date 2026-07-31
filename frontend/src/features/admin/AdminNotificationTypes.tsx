import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, Button, Spinner } from "../../ui";
import { useAdminNotificationTypes, adminApi, adminQk, type NotificationTypeUpsert, type NotificationTypeConfig } from "../../api/adminClient";

const inputStyle = { padding: "8px 10px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 13, color: "var(--fg-1)", outline: "none", fontFamily: "var(--font-sans)", width: "100%" } as const;

export function AdminNotificationTypes() {
  const qc = useQueryClient();
  const { data: types, isLoading } = useAdminNotificationTypes();
  const invalidate = () => qc.invalidateQueries({ queryKey: adminQk.notificationTypes });

  const [draft, setDraft] = useState<NotificationTypeUpsert>({ name: "", isActive: true, sortOrder: 0, erpServiceCode: "" });
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => adminApi.createNotificationType({ ...draft, name: draft.name.trim(), erpServiceCode: draft.erpServiceCode?.trim() || null }),
    onSuccess: () => { setDraft({ name: "", isActive: true, sortOrder: 0, erpServiceCode: "" }); setError(null); invalidate(); },
    onError: (e: Error) => setError(e.message) });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: NotificationTypeUpsert }) => adminApi.updateNotificationType(id, body),
    onSuccess: invalidate, onError: (e: Error) => setError(e.message) });
  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteNotificationType(id),
    onSuccess: invalidate, onError: (e: Error) => setError(e.message) });

  if (isLoading || !types) return <AppShell title="Notification types" crumb="Administration"><Spinner /></AppShell>;

  return (
    <AppShell title="Notification types" crumb="Administration">
      <p style={{ fontSize: 13, color: "var(--fg-2)", margin: "0 0 14px", maxWidth: 680 }}>
        These are the notifications vendors can set To / CC / BCC recipients for. This list is
        maintained in VSS. Deactivate a type to hide it from vendors without losing existing
        recipients. The SAP service code is optional — set it only for types that should sync a
        recipient to the SAP communication arrangement on approval; leave it blank for portal-only.
      </p>
      {error && <Card style={{ padding: "10px 14px", marginBottom: 12, color: "var(--colorStatusDangerForeground1)", fontSize: 13 }}>{error}</Card>}

      <Card>
        <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border-1)", background: "var(--bg-2)", display: "grid", gridTemplateColumns: "1fr 160px 90px 90px auto", gap: 12, alignItems: "end" }}>
          <div><Lbl>Name</Lbl><input style={inputStyle} placeholder="Purchase Order" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} /></div>
          <div><Lbl>SAP service code</Lbl><input style={inputStyle} placeholder="optional (e.g. 11)" value={draft.erpServiceCode ?? ""} onChange={(e) => setDraft({ ...draft, erpServiceCode: e.target.value })} /></div>
          <div><Lbl>Order</Lbl><input style={inputStyle} type="number" value={draft.sortOrder} onChange={(e) => setDraft({ ...draft, sortOrder: Number(e.target.value) })} /></div>
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13, color: "var(--fg-2)", paddingBottom: 8 }}>
            <input type="checkbox" checked={draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} /> Active
          </label>
          <Button variant="teal" style={{ padding: "9px 16px", fontSize: 13 }} disabled={!draft.name.trim() || create.isPending} onClick={() => create.mutate()}>
            {create.isPending ? "Adding…" : "+ Add type"}
          </Button>
        </div>

        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead><tr style={{ background: "var(--bg-2)" }}>
            {["Name", "SAP service code", "Order", "Active", ""].map((c) => <th key={c} style={th}>{c}</th>)}
          </tr></thead>
          <tbody>
            {types.map((t) => <Row key={t.id} type={t} onSave={(body) => update.mutate({ id: t.id, body })} onDelete={() => remove.mutate(t.id)} busy={update.isPending || remove.isPending} />)}
          </tbody>
        </table>
      </Card>
    </AppShell>
  );
}

function Row({ type, onSave, onDelete, busy }: { type: NotificationTypeConfig; onSave: (b: NotificationTypeUpsert) => void; onDelete: () => void; busy: boolean }) {
  const [name, setName] = useState(type.name);
  const [code, setCode] = useState(type.erpServiceCode ?? "");
  const [order, setOrder] = useState(type.sortOrder);
  const [active, setActive] = useState(type.isActive);
  const dirty = name !== type.name || (code || "") !== (type.erpServiceCode ?? "") || order !== type.sortOrder || active !== type.isActive;

  return (
    <tr style={{ borderBottom: "1px solid var(--colorNeutralStroke3)", opacity: type.isActive ? 1 : 0.55 }}>
      <td style={{ ...td, fontWeight: 600 }}><input style={inputStyle} value={name} onChange={(e) => setName(e.target.value)} /></td>
      <td style={{ ...td, width: 160 }}><input style={{ ...inputStyle, fontFamily: "var(--font-mono)" }} value={code} placeholder="—" onChange={(e) => setCode(e.target.value)} /></td>
      <td style={{ ...td, width: 90 }}><input style={inputStyle} type="number" value={order} onChange={(e) => setOrder(Number(e.target.value))} /></td>
      <td style={{ ...td, width: 80 }}><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /></td>
      <td style={{ ...td, textAlign: "right", whiteSpace: "nowrap" }}>
        <Button variant="outline" style={{ padding: "6px 12px", fontSize: 12, marginRight: 8 }} disabled={!dirty || !name.trim() || busy}
          onClick={() => onSave({ name: name.trim(), isActive: active, sortOrder: order, erpServiceCode: code.trim() || null })}>Save</Button>
        <Button variant="danger" style={{ padding: "6px 12px", fontSize: 12 }} disabled={busy} onClick={onDelete}>Delete</Button>
      </td>
    </tr>
  );
}

function Lbl({ children }: { children: React.ReactNode }) {
  return <div style={{ fontSize: 11, fontWeight: 600, color: "var(--fg-2)", marginBottom: 5 }}>{children}</div>;
}

const th = { padding: "10px 20px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "12px 20px", fontSize: 13, color: "var(--fg-1)" };
