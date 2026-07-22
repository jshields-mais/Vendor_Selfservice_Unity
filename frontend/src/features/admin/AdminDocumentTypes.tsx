import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AppShell } from "../../layout/AppShell";
import { Card, Button, Spinner } from "../../ui";
import { useAdminDocumentTypes, adminApi, adminQk, type DocumentTypeUpsert } from "../../api/adminClient";
import type { DocumentType } from "../../api/vssClient";

const inputStyle = { padding: "8px 10px", border: "1px solid var(--border-1)", borderRadius: 6, fontSize: 13, color: "var(--fg-1)", outline: "none", fontFamily: "var(--font-sans)", width: "100%" } as const;

export function AdminDocumentTypes() {
  const qc = useQueryClient();
  const { data: types, isLoading } = useAdminDocumentTypes();
  const invalidate = () => qc.invalidateQueries({ queryKey: adminQk.documentTypes });

  const [draft, setDraft] = useState<DocumentTypeUpsert>({ code: "", description: "", isActive: true, sortOrder: 0 });
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => adminApi.createDocType({ ...draft, code: draft.code.trim(), description: draft.description.trim() }),
    onSuccess: () => { setDraft({ code: "", description: "", isActive: true, sortOrder: 0 }); setError(null); invalidate(); },
    onError: (e: Error) => setError(e.message),
  });
  const update = useMutation({
    mutationFn: ({ id, body }: { id: string; body: DocumentTypeUpsert }) => adminApi.updateDocType(id, body),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  });
  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deleteDocType(id),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  });

  if (isLoading || !types) return <AppShell title="Document types" crumb="Administration"><Spinner /></AppShell>;

  return (
    <AppShell title="Document types" crumb="Administration">
      <p style={{ fontSize: 13, color: "var(--fg-2)", margin: "0 0 14px", maxWidth: 640 }}>
        These drive the document-type dropdown vendors choose from when uploading. Inactive
        types are hidden from vendors but kept for existing documents.
      </p>
      {error && <Card style={{ padding: "10px 14px", marginBottom: 12, color: "var(--colorStatusDangerForeground1)", fontSize: 13 }}>{error}</Card>}

      <Card>
        {/* Add new */}
        <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border-1)", background: "var(--bg-2)", display: "grid", gridTemplateColumns: "140px 1fr 100px 90px auto", gap: 12, alignItems: "end" }}>
          <div><Lbl>Code</Lbl><input style={inputStyle} placeholder="W9" value={draft.code} onChange={(e) => setDraft({ ...draft, code: e.target.value })} /></div>
          <div><Lbl>Description</Lbl><input style={inputStyle} placeholder="W-9 Tax Form" value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} /></div>
          <div><Lbl>Order</Lbl><input style={inputStyle} type="number" value={draft.sortOrder} onChange={(e) => setDraft({ ...draft, sortOrder: Number(e.target.value) })} /></div>
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 13, color: "var(--fg-2)", paddingBottom: 8 }}>
            <input type="checkbox" checked={draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} /> Active
          </label>
          <Button variant="teal" style={{ padding: "9px 16px", fontSize: 13 }} disabled={!draft.code.trim() || !draft.description.trim() || create.isPending} onClick={() => create.mutate()}>
            {create.isPending ? "Adding…" : "+ Add type"}
          </Button>
        </div>

        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead><tr style={{ background: "var(--bg-2)" }}>
            {["Code", "Description", "Order", "Active", ""].map((c) => <th key={c} style={th}>{c}</th>)}
          </tr></thead>
          <tbody>
            {types.map((t) => <Row key={t.id} type={t} onSave={(body) => update.mutate({ id: t.id, body })} onDelete={() => remove.mutate(t.id)} busy={update.isPending || remove.isPending} />)}
          </tbody>
        </table>
      </Card>
    </AppShell>
  );
}

function Row({ type, onSave, onDelete, busy }: { type: DocumentType; onSave: (b: DocumentTypeUpsert) => void; onDelete: () => void; busy: boolean }) {
  const [desc, setDesc] = useState(type.description);
  const [order, setOrder] = useState(type.sortOrder);
  const [active, setActive] = useState(type.isActive);
  const dirty = desc !== type.description || order !== type.sortOrder || active !== type.isActive;

  return (
    <tr style={{ borderBottom: "1px solid var(--colorNeutralStroke3)", opacity: type.isActive ? 1 : 0.55 }}>
      <td style={{ ...td, fontFamily: "var(--font-mono)", fontWeight: 600 }}>{type.code}</td>
      <td style={td}><input style={inputStyle} value={desc} onChange={(e) => setDesc(e.target.value)} /></td>
      <td style={{ ...td, width: 90 }}><input style={inputStyle} type="number" value={order} onChange={(e) => setOrder(Number(e.target.value))} /></td>
      <td style={{ ...td, width: 80 }}><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /></td>
      <td style={{ ...td, textAlign: "right", whiteSpace: "nowrap" }}>
        <Button variant="outline" style={{ padding: "6px 12px", fontSize: 12, marginRight: 8 }} disabled={!dirty || busy} onClick={() => onSave({ code: type.code, description: desc.trim(), isActive: active, sortOrder: order })}>Save</Button>
        <Button variant="danger" style={{ padding: "6px 12px", fontSize: 12 }} disabled={busy} onClick={onDelete}>Delete</Button>
      </td>
    </tr>
  );
}

function Lbl({ children }: { children: React.ReactNode }) {
  return <div style={{ fontSize: 11, fontWeight: 600, textTransform: "uppercase", letterSpacing: ".08em", color: "var(--fg-2)", marginBottom: 5 }}>{children}</div>;
}

const th = { padding: "10px 20px", textAlign: "left" as const, fontSize: 11, fontWeight: 600, textTransform: "uppercase" as const, letterSpacing: ".1em", color: "var(--fg-2)", borderBottom: "1px solid var(--border-1)" };
const td = { padding: "12px 20px", fontSize: 13, color: "var(--fg-1)" };
